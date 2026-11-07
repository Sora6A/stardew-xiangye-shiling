"""Create a distinct, local-only legacy save fixture without modifying the source farm."""
import argparse
from pathlib import Path
import re
import secrets
import sys
from xml.dom import minidom

from catalog import ROOT


def child(element, name):
    matches = [n for n in element.childNodes if n.nodeType == n.ELEMENT_NODE and n.tagName == name]
    if len(matches) != 1:
        raise ValueError(f"Expected one {name} element in {element.tagName}")
    return matches[0]


def replace_text(element, value):
    for node in list(element.childNodes):
        element.removeChild(node)
    element.appendChild(element.ownerDocument.createTextNode(str(value)))


def prepare_copy(source: Path, output: Path, label: str, save_id: int) -> Path:
    source, output = source.resolve(), output.resolve()
    if source == output or source.is_relative_to(output) or output.is_relative_to(source):
        raise ValueError("Source save and output directory must be separate.")
    if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{0,19}", label):
        raise ValueError("Test label must be 1-20 ASCII letters, digits or underscores, starting with a letter.")
    if not 0 < save_id < 2**63:
        raise ValueError("Test save ID must be a positive 63-bit integer.")
    main_path = source / source.name
    main = minidom.parse(str(main_path))
    info = minidom.parse(str(source / "SaveGameInfo"))
    if main.documentElement.tagName != "SaveGame" or info.documentElement.tagName != "Farmer":
        raise ValueError("Unrecognized Stardew Valley save structure.")
    original_id = child(main.documentElement, "uniqueIDForThisGame")
    if original_id.firstChild is None or int(original_id.firstChild.nodeValue) == save_id:
        raise ValueError("The fixture must use a distinct game ID.")
    player = child(main.documentElement, "player")
    for farmer in (player, info.documentElement):
        replace_text(child(farmer, "name"), label)
        replace_text(child(farmer, "farmName"), label)
    replace_text(original_id, save_id)
    folder_name = f"{label}_{save_id}"
    destination = output / folder_name
    if destination.exists():
        raise ValueError("Fixture already exists; choose a new name/ID.")
    # Serialize and parse both outputs before creating the destination.
    serialized = [(folder_name, main.toxml(encoding="utf-8")), ("SaveGameInfo", info.toxml(encoding="utf-8"))]
    for _, data in serialized:
        minidom.parseString(data)
    destination.mkdir(parents=True)
    for filename, data in serialized:
        (destination / filename).write_bytes(data)
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="One existing save folder, not the parent Saves directory")
    parser.add_argument("--label", default="XYS_P0_Old")
    args = parser.parse_args()
    fixture = prepare_copy(args.source, ROOT / ".local/save-fixtures", args.label, secrets.randbits(62) + 1)
    print(f"Prepared distinct legacy fixture: {fixture}")
    print("Source save was not changed. Not installed into the game's Saves directory and not yet game-validated.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as ex:
        print(f"FIXTURE FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
