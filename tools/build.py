"""Build an installable development or stable bundle without deploying it."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
from tempfile import TemporaryDirectory
from zipfile import ZipFile, ZIP_DEFLATED

from catalog import ROOT, PACK, check_repository, read_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-path", required=True, type=Path)
    parser.add_argument("--smapi-path", type=Path, help="Optional directory with SMAPI 4.4.0+ references")
    parser.add_argument("--release", action="store_true", help="Package a stable manifest version for players")
    args = parser.parse_args()
    errors = check_repository()
    if errors:
        raise ValueError("\n".join(errors))
    game = args.game_path.resolve()
    smapi = (args.smapi_path or game).resolve()
    project = ROOT / "src/XiangyeShiling"
    version = read_json(project / "manifest.json")["Version"]
    if args.release and not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise ValueError("A release bundle requires a stable manifest version.")
    env = os.environ.copy()
    env.update({"DOTNET_CLI_HOME": str(ROOT / ".local/dotnet"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
                "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1", "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "0"})
    subprocess.run(["dotnet", "build", str(project / "XiangyeShiling.csproj"), "--configuration", "Release",
                    f"-p:GamePath={game}", f"-p:SmapiPath={smapi}", "--nologo"], check=True, cwd=ROOT, env=env)
    binary = project / "bin/Release/net6.0/XiangyeShiling.dll"
    if not binary.is_file():
        raise FileNotFoundError(binary)
    suffix = "" if args.release else "-dev"
    output = ROOT / "artifacts" / f"XiangyeShiling-{version}{suffix}.zip"
    output.parent.mkdir(exist_ok=True)
    content_pack = ROOT / PACK
    changes = read_json(content_pack / "content.json")["Changes"]
    art = {content_pack / change["FromFile"] for change in changes if change["Action"] == "Load"}
    # Archive only runtime files. Local configs and observations may contain fixture identities.
    files = {
        "XiangyeShiling/XiangyeShiling.dll": binary,
        "XiangyeShiling/manifest.json": project / "manifest.json",
        "XiangyeShiling/assets/machine-recipes.json": project / "assets/machine-recipes.json",
        "XiangyeShiling.Content/manifest.json": content_pack / "manifest.json",
        "XiangyeShiling.Content/content.json": content_pack / "content.json",
    }
    for folder, source in (("XiangyeShiling", project), ("XiangyeShiling.Content", content_pack)):
        for locale in ("default", "zh"):
            relative = f"i18n/{locale}.json"
            files[f"{folder}/{relative}"] = source / relative
    for path in art:
        files[(Path("XiangyeShiling.Content") / path.relative_to(content_pack)).as_posix()] = path
    if args.release:
        files["INSTALL.md"] = ROOT / "docs/INSTALL.md"

    with TemporaryDirectory(dir=output.parent) as scratch:
        packaged = Path(scratch) / output.name
        with ZipFile(packaged, "w", compression=ZIP_DEFLATED) as bundle:
            for name, path in sorted(files.items()):
                bundle.write(path, name)
            if not args.release:
                bundle.writestr("DEVELOPMENT.txt", "Xiangye Shiling development build.\n"
                                "Requires Stardew Valley 1.6.15, SMAPI 4.4.0+, Content Patcher 2.9.0+.\n"
                                "See docs/DEVELOPMENT.md and docs/CONTENT_CATALOG.md in the repository.\n")
        packaged.replace(output)
    print(f"{'Release' if args.release else 'Development'} bundle: {output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (ValueError, OSError, subprocess.CalledProcessError) as ex:
        print(f"BUILD FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
