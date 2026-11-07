"""Preflight both workspace-local Mods profiles before copying; never start a network session."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys

from catalog import ROOT, read_json


MODS = ("ContentPatcher", "XiangyeShiling", "XiangyeShiling.Content")
ROLES = ("host", "client")
IDS = {"ContentPatcher": "Pathoschild.ContentPatcher", "XiangyeShiling": "Sora6A.XiangyeShiling",
       "XiangyeShiling.Content": "Sora6A.XiangyeShiling.Content"}
VERSION = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
                     r"(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
                     r"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?")


def _version(value):
    match = VERSION.fullmatch(value) if isinstance(value, str) else None
    if not match:
        raise ValueError(f"Invalid semantic version: {value!r}")
    suffix = match[4].split(".") if match[4] else None
    if suffix and any(p.isdigit() and len(p) > 1 and p.startswith("0") for p in suffix):
        raise ValueError(f"Invalid numeric prerelease identifier: {value!r}")
    return tuple(int(match[i]) for i in (1, 2, 3)), suffix


def _at_least(value, minimum):
    core, suffix = _version(value)
    required, required_suffix = _version(minimum)
    if core != required:
        return core > required
    if suffix is None or required_suffix is None:
        return suffix is None
    for left, right in zip(suffix, required_suffix):
        if left == right:
            continue
        if left.isdigit() and right.isdigit():
            return int(left) > int(right)
        if left.isdigit() != right.isdigit():
            return not left.isdigit()
        return left > right
    return len(suffix) >= len(required_suffix)


def _dependencies(manifest, versions):
    dependencies = manifest.get("Dependencies", [])
    if not isinstance(dependencies, list):
        raise ValueError("Manifest Dependencies must be an array.")
    seen = set()
    for dependency in dependencies:
        if not isinstance(dependency, dict) or not isinstance(dependency.get("UniqueID"), str):
            raise ValueError("Invalid manifest dependency.")
        identity = dependency["UniqueID"]
        required = dependency.get("IsRequired", True)
        if not identity or identity in seen or not isinstance(required, bool):
            raise ValueError("Ambiguous manifest dependency.")
        seen.add(identity)
        minimum = dependency.get("MinimumVersion")
        if minimum is not None:
            _version(minimum)
        if identity not in versions:
            if required:
                raise ValueError(f"Missing required profile dependency: {identity}")
        elif minimum is not None and not _at_least(versions[identity], minimum):
            raise ValueError(f"Unsatisfied profile dependency: {identity} >= {minimum}")
    return dependencies


def _require_dependency(manifest, identity, baseline):
    candidates = [d for d in manifest.get("Dependencies", []) if d.get("UniqueID") == identity]
    if len(candidates) != 1 or candidates[0].get("IsRequired", True) is not True \
            or not _at_least(candidates[0].get("MinimumVersion"), baseline):
        raise ValueError(f"Profile must require {identity} >= {baseline}")


def _validate_manifests(manifests):
    versions = {}
    for name, manifest in manifests.items():
        if not isinstance(manifest, dict) or manifest.get("UniqueID") != IDS[name]:
            raise ValueError(f"Unexpected mod identity in {name}")
        _version(manifest.get("Version"))
        versions[manifest["UniqueID"]] = manifest["Version"]
    if manifests["XiangyeShiling"]["Version"] != manifests["XiangyeShiling.Content"]["Version"]:
        raise ValueError("Machine module and content pack versions must match exactly.")
    if not _at_least(versions[IDS["XiangyeShiling"]], "0.1.0-alpha.2") \
            or not _at_least(versions[IDS["ContentPatcher"]], "2.9.0"):
        raise ValueError("Profiles are below the current P1 module/Content Patcher baseline.")
    for name in MODS:
        _dependencies(manifests[name], versions)
    for name in ("XiangyeShiling", "XiangyeShiling.Content"):
        if not _at_least(manifests[name].get("MinimumApiVersion"), "4.4.0"):
            raise ValueError(f"{name} must declare the SMAPI 4.4.0 baseline.")
    for name, binary in (("ContentPatcher", "ContentPatcher.dll"), ("XiangyeShiling", "XiangyeShiling.dll")):
        if manifests[name].get("EntryDll") != binary:
            raise ValueError(f"Unexpected EntryDll in {name}")
    owner = manifests["XiangyeShiling.Content"].get("ContentPackFor")
    if not isinstance(owner, dict) or owner.get("UniqueID") != IDS["ContentPatcher"] \
            or not _at_least(owner.get("MinimumVersion"), "2.9.0") \
            or not _at_least(versions[IDS["ContentPatcher"]], owner["MinimumVersion"]):
        raise ValueError("Content pack requires an unavailable or obsolete Content Patcher version.")
    _require_dependency(manifests["XiangyeShiling"], IDS["ContentPatcher"], "2.9.0")
    _require_dependency(manifests["XiangyeShiling.Content"], IDS["XiangyeShiling"], "0.1.0-alpha.2")
    return versions


def _overlaps(first, second):
    return first.is_relative_to(second) or second.is_relative_to(first)


def _tree(directory):
    """Reject links/reparse escapes before reading packages or writing existing destinations."""
    if not directory.is_dir():
        raise ValueError(f"Missing directory: {directory}")
    boundary = directory.resolve()
    result = []
    for path in (directory, *sorted(directory.rglob("*"))):
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
        if path.is_symlink() or attributes & 0x400 or not path.resolve().is_relative_to(boundary):
            raise ValueError(f"Linked or escaping profile path: {path}")
        if path.is_file():
            result.append(path)
    return result


def validate_output(output, workspace_root):
    """CLI ownership boundary; the library API instead accepts caller-owned temporary sandboxes."""
    workspace = Path(workspace_root).resolve()
    local = (workspace / ".local").resolve()
    destination = Path(output)
    if not destination.is_absolute():
        destination = workspace / destination
    destination = destination.resolve()
    if not local.is_relative_to(workspace) or destination == local or not destination.is_relative_to(local):
        raise ValueError("Profile output must be a strict descendant of this workspace's .local directory.")
    return destination


def prepare_profiles(source, runtime, destination, *, harness_dir=None):
    """Prepare caller-owned profiles after complete read-only preflight; return the last-written audit.

    No ROOT lookup occurs here. The CLI separately checks workspace ownership. Both roles and all
    inputs are checked before any mkdir/copy. This is not a transaction: later I/O failure or source
    changes can leave partial copies; profiles.json is written only after both roles verify.
    Source config.json files are excluded; existing per-mod root configs stay byte-for-byte intact.
    An optional curated harness accepts its manifest/EntryDll and optional matching .deps/.runtimeconfig
    only. New harness configs default disabled; existing configs are preserved and reported, not enabled.
    Manifest checks establish declared compatibility, not DLL identity or runtime SDK verification.
    """
    source, runtime, destination = (Path(p).resolve() for p in (source, runtime, destination))
    if _overlaps(source, destination) or _overlaps(runtime, destination):
        raise ValueError("Source/runtime and destination must not contain one another.")
    if not (runtime / "StardewModdingAPI.exe").is_file():
        raise ValueError("Prepare the isolated Windows test runtime first.")
    manifests = {name: read_json(source / name / "manifest.json") for name in MODS}
    versions = _validate_manifests(manifests)
    files = {}
    for name in MODS:
        for path in _tree(source / name):
            if path.name.casefold() != "config.json":
                files[Path(name) / path.relative_to(source / name)] = path
    for name, binary in (("ContentPatcher", "ContentPatcher.dll"), ("XiangyeShiling", "XiangyeShiling.dll")):
        if Path(name) / binary not in files:
            raise ValueError(f"Missing {name}/{binary}")
    if Path("XiangyeShiling.Content/content.json") not in files:
        raise ValueError("Missing XiangyeShiling.Content/content.json")
    names = list(MODS)
    if harness_dir is not None:
        harness = Path(harness_dir).resolve()
        if _overlaps(harness, destination):
            raise ValueError("Harness source and destination must not contain one another.")
        manifest = read_json(harness / "manifest.json")
        identity = manifest.get("UniqueID") if isinstance(manifest, dict) else None
        entry = manifest.get("EntryDll") if isinstance(manifest, dict) else None
        if not isinstance(identity, str) or not identity.strip() or identity in versions \
                or not isinstance(entry, str) or not re.fullmatch(r"[A-Za-z0-9_.-]+\.dll", entry):
            raise ValueError("Harness requires a unique identity and a plain DLL filename.")
        _version(manifest.get("Version"))
        if not _at_least(manifest.get("MinimumApiVersion"), "4.4.0"):
            raise ValueError("Harness must declare the SMAPI 4.4.0 baseline.")
        _dependencies(manifest, versions)
        whitelist = {"manifest.json", entry, f"{Path(entry).stem}.deps.json", f"{Path(entry).stem}.runtimeconfig.json"}
        for path in _tree(harness):
            relative = path.relative_to(harness).as_posix()
            if path.name.casefold() == "config.json":
                continue
            if relative not in whitelist:
                raise ValueError(f"Unexpected harness package file: {relative}")
            files[Path("GameHarness") / relative] = path
        if Path("GameHarness") / entry not in files:
            raise ValueError(f"Missing harness DLL: {entry}")
        names.append("GameHarness")
        versions[identity] = manifest["Version"]

    expected = {relative.as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                for relative, path in files.items()}
    allowed = {"profiles.json"}
    for role in ROLES:
        allowed.add(f"{role}/start.ps1")
        allowed.update(f"{role}/Mods/{relative}" for relative in expected)
        allowed.update(f"{role}/Mods/{name}/config.json" for name in names)
    if destination.exists():
        extra = []
        for path in _tree(destination):
            relative = path.relative_to(destination).as_posix()
            if relative not in allowed:
                extra.append(relative)
            if path.stat().st_nlink > 1:
                raise ValueError(f"Hard-linked destination file is not safe to update: {relative}")
        if extra:
            raise ValueError(f"Unexpected files in profiles; inspect them before preparing: {sorted(extra)}")
    # Parse an existing optional harness config before changing either role; never inherit its source config.
    if harness_dir is not None:
        for role in ROLES:
            config = destination / role / "Mods/GameHarness/config.json"
            if config.exists():
                value = read_json(config)
                if not isinstance(value, dict) or ("Enabled" in value and not isinstance(value["Enabled"], bool)):
                    raise ValueError(f"Invalid existing harness configuration: {role}")

    role_records = {}
    for role in ROLES:
        mods = destination / role / "Mods"
        for relative, path in files.items():
            target = mods / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
        generated_config = False
        if harness_dir is not None and not (mods / "GameHarness/config.json").exists():
            (mods / "GameHarness/config.json").write_text(
                json.dumps({"Enabled": False, "OpenPreview": False}, indent=2) + "\n", encoding="utf-8")
            generated_config = True
        actual = {relative: hashlib.sha256((mods / relative).read_bytes()).hexdigest() for relative in expected}
        if actual != expected:
            raise ValueError(f"Copy verification failed: {role}")
        quote = lambda value: "'" + str(value).replace("'", "''") + "'"
        (destination / role / "start.ps1").write_text(
            "$ErrorActionPreference = 'Stop'\n"
            f"Set-Location -LiteralPath {quote(runtime)}\n"
            f"& {quote(runtime / 'StardewModdingAPI.exe')} --mods-path {quote(mods)}\n", encoding="utf-8")
        configs = {f"{name}/config.json": hashlib.sha256((mods / name / "config.json").read_bytes()).hexdigest()
                   for name in names if (mods / name / "config.json").exists()}
        actual_versions = {}
        for name in names:
            manifest = read_json(mods / name / "manifest.json")
            actual_versions[manifest["UniqueID"]] = manifest["Version"]
        role_records[role] = {"mods": str(mods), "files": actual, "configs": configs,
                              "versions": actual_versions, "generatedDisabledHarnessConfig": generated_config}
        if harness_dir is not None:
            role_records[role]["harnessEnabled"] = read_json(mods / "GameHarness/config.json").get("Enabled")
    record = {"schemaVersion": 2, "runtime": str(runtime), "versions": versions, "files": expected,
              "roles": role_records, "networkStarted": False, "runtimeBinaryVersionVerified": False,
              "verified": "Both roles match the package SHA-256 hashes; existing configs are preserved separately.",
              "saveIsolation": "Instances use the normal user profile. Only the host opens a dedicated test farm; the client joins it."}
    (destination / "profiles.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-mods", type=Path, default=ROOT / ".local/test-mods")
    parser.add_argument("--runtime", type=Path, default=ROOT / ".local/validation-game")
    parser.add_argument("--output", type=Path, default=ROOT / ".local/multiplayer",
                        help="Strict workspace .local descendant; use a new path to keep older profiles intact")
    parser.add_argument("--harness-dir", type=Path,
                        help="Optional curated manifest/DLL package; new configs remain disabled")
    args = parser.parse_args()
    destination = validate_output(args.output, ROOT)
    prepare_profiles(args.source_mods, args.runtime, destination, harness_dir=args.harness_dir)
    print(f"Prepared matching host/client profiles: {destination}")
    print("LAN connection and simultaneous machine operations still require in-game validation.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as ex:
        print(f"PREPARATION FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
