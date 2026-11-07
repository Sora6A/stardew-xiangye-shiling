"""Plan or bind a dedicated NET-02 save identity while keeping both diagnostics disabled.

Inputs are re-read on every call; an exported plan is evidence, never an executable authority.
Only four caller-owned config files may change. Saves, packages and profiles.json are read-only.
Applications require stopped game instances. Per-file replacement is atomic, the batch is not:
backups and a failed audit remain if a later write fails; no concurrent editor is rolled back.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import sys
import tempfile
from xml.etree import ElementTree as ET
from xml.parsers import expat

from catalog import ROOT
import prepare_multiplayer as packages

MAX_XML_BYTES = 64 * 1024 * 1024
MAX_JSON_BYTES = 2 * 1024 * 1024
# Known source/old P0/UI/Stage3 identities from HANDOFF; this is not a scan of personal saves.
PROTECTED_IDS = {449937634, 8624092401, 8624092801, 8624092802}
HARNESS_ID = "Sora6A.XiangyeShiling.GameHarness"
CONFIG_NAMES = ("XiangyeShiling/config.json", "GameHarness/config.json")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _sha(data):
    return hashlib.sha256(data).hexdigest()


def _plain_path(path):
    """Check lexical ancestors before resolve, so a junction is not hidden by normalization."""
    path = Path(os.path.abspath(path))
    for entry in (path, *path.parents):
        if entry.exists() or entry.is_symlink():
            _require(not entry.is_symlink() and not getattr(entry.lstat(), "st_file_attributes", 0) & 0x400,
                     "Linked/reparse input or output path is not supported.")
    return path


def _bytes(path, maximum):
    path = _plain_path(path)
    _require(path.is_file() and path.stat().st_nlink == 1, "Expected a regular, unlinked input file.")
    with path.open("rb") as stream:
        data = stream.read(maximum + 1)
    _require(len(data) <= maximum, "Input exceeds the bounded read limit.")
    return data


def _object(pairs):
    value, seen = {}, set()
    for key, item in pairs:
        # SMAPI config properties are case-insensitive; reject hidden competing switch values.
        _require(key.casefold() not in seen, "Duplicate or case-aliased JSON property.")
        seen.add(key.casefold())
        value[key] = item
    return value


def _float(value):
    parsed = float(value)
    _require(math.isfinite(parsed), "Non-finite JSON value.")
    return parsed


def _json(path):
    data = _bytes(path, MAX_JSON_BYTES)
    value = json.loads(data.decode("utf-8-sig"), object_pairs_hook=_object, parse_float=_float,
                       parse_constant=lambda _: _require(False, "Non-finite JSON value."))
    _require(isinstance(value, dict), "Expected a JSON object.")
    return value, data


def _serialized(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n").encode("utf-8")


def _farm_name(value):
    _require(isinstance(value, str) and value.startswith("XYSNET")
             and 6 <= len(value.encode("utf-16-le")) // 2 <= 20
             and all(ord(c) >= 32 for c in value), "Expected a dedicated XYSNET farm name (6-20 UTF-16 units).")
    return value


def _child(element, tag):
    matches = [e for e in element if e.tag == tag]
    _require(len(matches) == 1, f"Expected exactly one direct {tag} element.")
    return matches[0]


def _text(element):
    _require(len(element) == 0 and element.text is not None, "Identity must contain plain text.")
    return element.text


def _xml(path, root_tag):
    data = _bytes(path, MAX_XML_BYTES)
    parser = expat.ParserCreate()

    def reject_declaration(*_):
        raise ValueError("DTD and entity declarations are not supported in a save fixture.")

    # Expat detects declarations for UTF-8 and UTF-16 before expanding any entity.
    parser.StartDoctypeDeclHandler = reject_declaration
    parser.EntityDeclHandler = reject_declaration
    parser.ExternalEntityRefHandler = reject_declaration
    try:
        parser.Parse(data, True)
        root = ET.fromstring(data)
    except (expat.ExpatError, ET.ParseError) as error:
        raise ValueError("Malformed save XML.") from error
    _require(root.tag == root_tag, f"Expected {root_tag} save root.")
    return root, _sha(data)


def read_fixture(save_dir, expected_farm_name):
    """Read two exact save files, not arbitrary descendant tags or the save-folder label.

    SaveGameInfo has no authoritative world ID. Freshness and available cabins still need
    operator/game verification; refusing the known legacy IDs prevents their accidental reuse.
    """
    save = _plain_path(save_dir)
    expected = _farm_name(expected_farm_name)
    main, main_sha = _xml(save / save.name, "SaveGame")
    info, info_sha = _xml(save / "SaveGameInfo", "Farmer")
    name = _farm_name(_text(_child(_child(main, "player"), "farmName")))
    _require(name == expected == _text(_child(info, "farmName")), "Save identity/farm names do not match.")
    raw_id = _text(_child(main, "uniqueIDForThisGame"))
    _require(re.fullmatch(r"[1-9][0-9]{0,19}", raw_id) is not None, "World ID must be a nonzero unsigned integer.")
    identity = int(raw_id)
    _require(identity < 2**64 and identity not in PROTECTED_IDS, "World ID is out of range or belongs to a protected old fixture.")
    return {"farmName": name, "saveFixtureId": identity,
            "saveHashes": {save.name: main_sha, "SaveGameInfo": info_sha}}


def _relative(value):
    _require(isinstance(value, str) and value and "\\" not in value and ":" not in value,
             "Package path must be a plain relative POSIX path.")
    path = PurePosixPath(value)
    _require(not path.is_absolute() and path.as_posix() == value and ".." not in path.parts
             and len(path.parts) >= 2 and path.parts[0] in (*packages.MODS, "GameHarness")
             and value.casefold() not in {f"{m}/config.json".casefold() for m in (*packages.MODS, "GameHarness")},
             "Invalid package path or configuration disguised as a package file.")
    return value


def inspect_profiles(profiles_dir):
    """Re-hash current targets; the old record's config hashes describe its preparation time.

    Neither recorded absolute role paths nor runtime paths select a config write target.
    Declared compatibility and matching hashes are not SDK/runtime version verification.
    """
    profiles = _plain_path(profiles_dir)
    record, raw = _json(profiles / "profiles.json")
    _require(type(record.get("schemaVersion")) is int and record["schemaVersion"] == 2,
             "Expected profiles schemaVersion 2.")
    expected = record.get("files")
    _require(isinstance(expected, dict) and expected, "Missing package hash map.")
    seen = set()
    for relative, digest in expected.items():
        _relative(relative)
        _require(relative.casefold() not in seen and isinstance(digest, str)
                 and re.fullmatch(r"[0-9a-f]{64}", digest), "Ambiguous package path or invalid SHA-256.")
        seen.add(relative.casefold())
    required = {f"{m}/manifest.json" for m in (*packages.MODS, "GameHarness")} | {
        "ContentPatcher/ContentPatcher.dll", "XiangyeShiling/XiangyeShiling.dll",
        "XiangyeShiling.Content/content.json", "GameHarness/GameHarness.dll"}
    _require(required <= set(expected), "Profile record omits a required package file.")
    harness_files = {p for p in expected if p.startswith("GameHarness/")}
    _require(harness_files <= {"GameHarness/manifest.json", "GameHarness/GameHarness.dll",
                              "GameHarness/GameHarness.deps.json", "GameHarness/GameHarness.runtimeconfig.json"},
             "Unexpected file in the curated GameHarness package.")
    roles, configs = record.get("roles"), {}
    _require(isinstance(roles, dict) and set(roles) == set(packages.ROLES), "Expected exactly host/client profiles.")
    for role in packages.ROLES:
        declared = roles[role]
        _require(isinstance(declared, dict) and declared.get("files") == expected
                 and declared.get("versions") == record.get("versions"), "Role package declarations do not match.")
        mods = profiles / role / "Mods"
        actual = {}
        for path in packages._tree(mods):
            relative = path.relative_to(mods).as_posix()
            # BUG-018: Windows casing does not turn the same writable config into a package.
            if relative.casefold() in {f"{m}/config.json".casefold() for m in (*packages.MODS, "GameHarness")}:
                continue
            _require(relative in expected, "Unexpected package/report file; preserve and archive it before preparation.")
            _require(path.stat().st_nlink == 1, "Hard-linked package file is not supported.")
            actual[relative] = _sha(path.read_bytes())
        _require(actual == expected, f"Current {role} package hashes do not match the recorded build.")
        manifests = {m: _json(mods / m / "manifest.json")[0] for m in packages.MODS}
        versions = packages._validate_manifests(manifests)
        harness = _json(mods / "GameHarness/manifest.json")[0]
        _require(harness.get("UniqueID") == HARNESS_ID and harness.get("EntryDll") == "GameHarness.dll"
                 and packages._at_least(harness.get("MinimumApiVersion"), "4.4.0"), "Expected this project's NET-02 GameHarness.")
        packages._version(harness.get("Version"))
        packages._dependencies(harness, versions)
        packages._require_dependency(harness, packages.IDS["XiangyeShiling"], "0.1.0-alpha.2")
        versions[HARNESS_ID] = harness["Version"]
        _require(versions == record.get("versions"), "Actual manifest versions do not match the profile record.")
        _require("GameHarness/GameHarness.dll" in actual, "Missing NET-02 harness DLL.")
        for name in CONFIG_NAMES:
            value, config_raw = _json(mods / name)
            if name.startswith("XiangyeShiling/"):
                _require(value.get("EnableReadOnlyNetworkProbes") is False
                         and isinstance(value.get("NetworkProbeFarmName"), str)
                         and type(value.get("NetworkProbeSaveFixtureId")) is int
                         and 0 <= value["NetworkProbeSaveFixtureId"] < 2**64, "Main probe config must be explicitly disabled and typed.")
            else:
                _require(value.get("Enabled") is False and value.get("OpenPreview") is False
                         and value.get("Scenario") == "Network" and value.get("NetworkRole") == role.title()
                         and isinstance(value.get("FarmName"), str) and type(value.get("SaveFixtureId")) is int
                         and 0 <= value["SaveFixtureId"] < 2**64, "Harness must be disabled in Network mode with the correct role.")
            configs[f"{role}/Mods/{name}"] = {"beforeSha256": _sha(config_raw), "configuration": value}
    return {"profilesSha256": _sha(raw), "packageFiles": expected, "versions": record["versions"], "configs": configs}


def _plan(save_dir, profiles_dir, expected_farm_name):
    fixture = read_fixture(save_dir, expected_farm_name)
    checked = inspect_profiles(profiles_dir)
    for relative, entry in checked["configs"].items():
        value = entry["configuration"].copy()
        if "/XiangyeShiling/" in relative:
            value.update(NetworkProbeFarmName=fixture["farmName"], NetworkProbeSaveFixtureId=fixture["saveFixtureId"])
        else:
            value.update(FarmName=fixture["farmName"], SaveFixtureId=fixture["saveFixtureId"])
        entry.update(configuration=value, afterSha256=_sha(_serialized(value)))
    return {"schemaVersion": 1, **fixture, **checked,
            "productionDllSha256": checked["packageFiles"]["XiangyeShiling/XiangyeShiling.dll"],
            "harnessDllSha256": checked["packageFiles"]["GameHarness/GameHarness.dll"],
            "configurationApplied": False, "networkStarted": False, "fullNet02Accepted": False,
            "runtimeBinaryVersionVerified": False, "cabinAvailabilityVerified": False,
            "independentWorldCreationVerified": False}


def _output(output, profiles, save):
    destination = _plain_path(output)
    _require(not destination.exists(), "Evidence output already exists; choose a new directory.")
    record, _ = _json(profiles / "profiles.json")
    runtime = record.get("runtime")
    _require(isinstance(runtime, str) and runtime, "Missing recorded runtime path.")
    for source in (profiles, save, _plain_path(runtime)):
        _require(not packages._overlaps(destination, source), "Evidence output and inputs/runtime must not contain one another.")
    return destination


def _replace_config(path, data):
    """Replace only the checked file; never truncate its original bytes on write failure."""
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(dir=path.parent, prefix=".net02-config-", delete=False) as stream:
            temporary = Path(stream.name)
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def configure_fixture(save_dir, profiles_dir, *, expected_farm_name, output=None, apply=False):
    """Preflight all inputs before writes; optionally export backups/candidates and apply.

    No save-folder name or declared role.mods path is used as authority. Required disabled
    switches prevent silently taking over an active test. Caller stops all profile users;
    rechecks detect ordinary drift, not an adversarial filesystem race. A failed batch must
    be inspected against before/prepared hashes, never treated as applied or auto-replayed.
    """
    save, profiles = _plain_path(save_dir), _plain_path(profiles_dir)
    _require(type(apply) is bool, "Apply must be an explicit boolean.")
    _require(not packages._overlaps(save, profiles), "Save and profile trees must be separate.")
    _require(output is not None or not apply, "Applying requires a new evidence output directory.")
    plan = _plan(save, profiles, expected_farm_name)
    if output is None:
        return plan
    destination = _output(output, profiles, save)
    originals = {p: _bytes(profiles / p, MAX_JSON_BYTES) for p in plan["configs"]}
    _require(all(_sha(data) == plan["configs"][p]["beforeSha256"] for p, data in originals.items()),
             "Configuration changed during preflight.")
    destination.mkdir(parents=True)
    for relative, data in originals.items():
        for tree, contents in (("before", data), ("prepared", _serialized(plan["configs"][relative]["configuration"]))):
            target = destination / tree / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(contents)
    (destination / "fixture-plan.json").write_bytes(_serialized(plan))
    if not apply:
        return plan
    audit = {**plan, "replacedConfigs": [], "failure": None}
    try:
        _require(_plan(save, profiles, expected_farm_name) == plan, "Inputs changed before applying; inspect the retained plan.")
        for relative, entry in plan["configs"].items():
            _require(_sha(_bytes(profiles / relative, MAX_JSON_BYTES)) == entry["beforeSha256"],
                     "Configuration changed before replacement.")
            _replace_config(profiles / relative, _serialized(entry["configuration"]))
            audit["replacedConfigs"].append(relative)
        verified = _plan(save, profiles, expected_farm_name)
        _require(all(verified[k] == plan[k] for k in ("saveHashes", "profilesSha256", "packageFiles", "versions"))
                 and all(verified["configs"][p]["beforeSha256"] == e["afterSha256"] for p, e in plan["configs"].items()),
                 "Post-write identity/package/config verification failed.")
        audit["configurationApplied"] = True
    except (OSError, ValueError) as error:
        audit["failure"] = str(error)
        try:
            (destination / "fixture-apply.json").write_bytes(_serialized(audit))
        except OSError:
            pass  # Original failure stays visible; backups are the recovery source if audit I/O also fails.
        raise
    (destination / "fixture-apply.json").write_bytes(_serialized(audit))
    return audit


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--save", type=Path, required=True, help="Dedicated host save folder; source is never modified")
    parser.add_argument("--profiles", type=Path, required=True, help="Prepared schema2 profiles under workspace .local")
    parser.add_argument("--farm-name", required=True, help="Expected real XYSNET farm name, not the folder/player label")
    parser.add_argument("--output", type=Path, help="New workspace .local directory for backups, candidates and audit")
    parser.add_argument("--apply", action="store_true", help="Bind four configs, keeping all diagnostics disabled; stop both games first")
    args = parser.parse_args()
    profiles = packages.validate_output(_plain_path(args.profiles), ROOT)
    output = packages.validate_output(_plain_path(args.output), ROOT) if args.output else None
    result = configure_fixture(args.save, profiles, expected_farm_name=args.farm_name, output=output, apply=args.apply)
    print(json.dumps({k: result[k] for k in ("farmName", "saveFixtureId", "configurationApplied", "networkStarted", "fullNet02Accepted")}))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"FIXTURE BINDING FAILED: {error}", file=sys.stderr)
        raise SystemExit(1)
