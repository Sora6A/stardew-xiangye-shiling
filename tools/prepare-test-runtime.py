"""Prepare a workspace-only game copy with a supplied official SMAPI installer and CP folder."""
import argparse
from io import BytesIO
import json
from pathlib import Path
import shutil
import sys
from zipfile import ZipFile

from catalog import ROOT, read_json


def extract_checked(archive: ZipFile, destination: Path):
    for entry in archive.infolist():
        if not (destination / entry.filename).resolve().is_relative_to(destination.resolve()):
            raise ValueError(f"Archive entry escapes destination: {entry.filename}")
    archive.extractall(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-path", required=True, type=Path)
    parser.add_argument("--smapi-installer", required=True, type=Path)
    parser.add_argument("--content-patcher", required=True, type=Path,
                        help="Ready-to-load CP folder containing manifest.json and ContentPatcher.dll")
    parser.add_argument("--bundle", required=True, type=Path, help="This project's development ZIP")
    args = parser.parse_args()
    game = args.game_path.resolve()
    cp = args.content_patcher.resolve()
    runtime = ROOT / ".local/validation-game"
    mods = ROOT / ".local/test-mods"
    for target in (runtime, mods):
        if not target.resolve().is_relative_to((ROOT / ".local").resolve()):
            raise ValueError("Test destinations must stay within the workspace .local directory.")
        if target.resolve() == game or game.is_relative_to(target.resolve()):
            raise ValueError("The test destination must not contain the source installation.")
    if not (game / "Stardew Valley.dll").is_file() or not (game / "Content").is_dir():
        raise ValueError("Source game directory is incomplete.")
    manifest = read_json(cp / "manifest.json")
    if manifest.get("UniqueID") != "Pathoschild.ContentPatcher" or not (cp / "ContentPatcher.dll").is_file():
        raise ValueError("A complete Content Patcher folder is required.")
    # Validate all inputs before starting the copy. Never install into the source game folder.
    with ZipFile(args.smapi_installer) as installer:
        entries = [n for n in installer.namelist() if n.endswith("internal/windows/install.dat")]
        if len(entries) != 1:
            raise ValueError("Expected one Windows payload in the official SMAPI installer.")
        smapi_bytes = installer.read(entries[0])
    with ZipFile(args.bundle) as bundle:
        if "XiangyeShiling/XiangyeShiling.dll" not in bundle.namelist():
            raise ValueError("Not a Xiangye Shiling development bundle.")
    runtime.mkdir(parents=True, exist_ok=True)
    mods.mkdir(parents=True, exist_ok=True)
    for source in game.iterdir():
        if source.is_file() and not source.name.startswith("StardewModdingAPI"):
            shutil.copy2(source, runtime / source.name)
    shutil.copytree(game / "Content", runtime / "Content", dirs_exist_ok=True)
    with ZipFile(BytesIO(smapi_bytes)) as payload:
        extract_checked(payload, runtime)
    for suffix in ("deps.json", "runtimeconfig.json"):
        shutil.copy2(runtime / f"Stardew Valley.{suffix}", runtime / f"StardewModdingAPI.{suffix}")
    if cp != (mods / "ContentPatcher").resolve():
        shutil.copytree(cp, mods / "ContentPatcher", dirs_exist_ok=True)
    with ZipFile(args.bundle) as bundle:
        extract_checked(bundle, mods)
    (ROOT / ".local/test-runtime.json").write_text(json.dumps({
        "gameSource": str(game), "runtime": str(runtime), "mods": str(mods),
        "smapiInstaller": str(args.smapi_installer.resolve()),
        "contentPatcherVersion": manifest["Version"],
        "saveIsolation": "Game and Mods are copied; save/log paths remain the normal user profile. Use test saves only.",
    }, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared runtime: {runtime}\nTest Mods: {mods}")
    print("Run tools/check-environment.ps1 with both paths before launching. Only use test saves.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as ex:
        print(f"PREPARATION FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
