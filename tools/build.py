"""Build the local development bundle; never deploy into the game directory."""
import argparse
import os
from pathlib import Path
import subprocess
import sys
from zipfile import ZipFile, ZIP_DEFLATED

from catalog import ROOT, PACK, check_repository, read_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-path", required=True, type=Path)
    parser.add_argument("--smapi-path", type=Path, help="Optional directory with SMAPI 4.4.0+ references")
    args = parser.parse_args()
    errors = check_repository()
    if errors:
        raise ValueError("\n".join(errors))
    game = args.game_path.resolve()
    smapi = (args.smapi_path or game).resolve()
    project = ROOT / "src/XiangyeShiling"
    env = os.environ.copy()
    env.update({"DOTNET_CLI_HOME": str(ROOT / ".local/dotnet"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "0"})
    subprocess.run(["dotnet", "build", str(project / "XiangyeShiling.csproj"), "--configuration", "Release",
                    f"-p:GamePath={game}", f"-p:SmapiPath={smapi}", "--nologo"], check=True, cwd=ROOT, env=env)
    binary = project / "bin/Release/net6.0/XiangyeShiling.dll"
    if not binary.is_file():
        raise FileNotFoundError(binary)
    version = read_json(project / "manifest.json")["Version"]
    output = ROOT / "artifacts" / f"XiangyeShiling-{version}-dev.zip"
    output.parent.mkdir(exist_ok=True)
    with ZipFile(output, "w", compression=ZIP_DEFLATED) as bundle:
        bundle.write(binary, "XiangyeShiling/XiangyeShiling.dll")
        bundle.write(project / "manifest.json", "XiangyeShiling/manifest.json")
        for path in sorted((project / "i18n").glob("*.json")):
            bundle.write(path, f"XiangyeShiling/i18n/{path.name}")
        for path in sorted((project / "assets").glob("*.json")):
            bundle.write(path, f"XiangyeShiling/assets/{path.name}")
        content_pack = ROOT / PACK
        changes = read_json(content_pack / "content.json")["Changes"]
        art = {content_pack / change["FromFile"] for change in changes if change["Action"] == "Load"}
        for path in sorted(set(content_pack.rglob("*.json")) | art):
            bundle.write(path, (Path("XiangyeShiling.Content") / path.relative_to(content_pack)).as_posix())
        bundle.writestr("DEVELOPMENT.txt", "Xiangye Shiling P2 development build.\n"
                        "Requires Stardew Valley 1.6.15, SMAPI 4.4.0+, Content Patcher 2.9.0+.\n"
                        "P1 machine transactions passed single-player and opt-in Windows native direct-IP loopback testing.\n"
                        "See docs/CONTENT_CATALOG.md for enabled data and docs/DEVELOPMENT_PLAN.md for acceptance.\n"
                        "Back up saves and use a dedicated test profile. See docs/DEVELOPMENT.md in the repository.\n")
    print(f"Development bundle: {output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, subprocess.CalledProcessError) as ex:
        print(f"BUILD FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
