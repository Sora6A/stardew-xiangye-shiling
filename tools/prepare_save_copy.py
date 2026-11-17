"""Create a distinct, local-only save fixture without modifying the source farm."""
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


def scalar(element, name):
    nodes = child(element, name).childNodes
    if any(node.nodeType != node.TEXT_NODE for node in nodes):
        raise ValueError(f"Expected plain text in {name}.")
    return "".join(node.nodeValue for node in nodes)


def cooperative_farmhands(world, host, info, label):
    """Bind native 1.6 roster entries to the new test world without changing their Farmer IDs."""
    source_farm = scalar(host, "farmName")
    pattern = r"XYSNET[A-Za-z0-9]{1,14}"
    if not re.fullmatch(pattern, source_farm) or not re.fullmatch(pattern, label) or label == source_farm:
        raise ValueError("Cooperative copies require distinct XYSNET source and destination test farm names.")

    def farmer_id(farmer):
        raw = scalar(farmer, "UniqueMultiplayerID")
        if not re.fullmatch(r"-?(?:0|[1-9][0-9]*)", raw) or not -(2**63) <= int(raw) < 2**63:
            raise ValueError("Invalid signed native Farmer ID.")
        return int(raw)

    host_id = farmer_id(host)
    if host_id == 0 or farmer_id(info) != host_id or scalar(info, "name") != scalar(host, "name") \
            or scalar(info, "farmName") != source_farm:
        raise ValueError("SaveGameInfo does not describe the source host.")

    roster = child(world, "farmhands")
    farmers = [node for node in roster.childNodes if node.nodeType == node.ELEMENT_NODE]
    if any(node.tagName != "Farmer" for node in farmers):
        raise ValueError("Unsupported native farmhand record.")
    ids = {host_id}
    farm_names = []
    customized_count = 0
    for farmer in farmers:
        actor_id = farmer_id(farmer)
        name = scalar(farmer, "name")
        customized = scalar(farmer, "isCustomized")
        farm_name = scalar(farmer, "farmName")
        if actor_id in ids or name == label or customized not in ("true", "false") \
                or farm_name not in ("", source_farm):
            raise ValueError("Invalid or aliased cooperative farmhand identity.")
        if actor_id != 0:
            ids.add(actor_id)
        if customized == "true":
            if not name or actor_id == 0:
                raise ValueError("Customized native farmhand needs a name and a nonzero ID.")
            customized_count += 1
        if customized == "true" or farm_name:
            farm_names.append(child(farmer, "farmName"))
    if not customized_count:
        raise ValueError("Cooperative copy needs a persisted, customized farmhand.")
    return farm_names


def prepare_copy(source: Path, output: Path, label: str, save_id: int, *, cooperative: bool = False) -> Path:
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
    if cooperative:
        for farm_name in cooperative_farmhands(main.documentElement, player, info.documentElement, label):
            replace_text(farm_name, label)
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
    parser.add_argument("--save-id", type=int, help="Explicit new test world ID; omitted uses a random ID")
    parser.add_argument("--cooperative", action="store_true", help="Copy a dedicated native XYSNET test world with its farmhands")
    args = parser.parse_args()
    save_id = args.save_id if args.save_id is not None else secrets.randbits(62) + 1
    fixture = prepare_copy(args.source, ROOT / ".local/save-fixtures", args.label, save_id, cooperative=args.cooperative)
    print(f"Prepared distinct {'cooperative' if args.cooperative else 'legacy'} fixture: {fixture}")
    print("Source save was not changed. Not installed into the game's Saves directory and not yet game-validated.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as ex:
        print(f"FIXTURE FAILED: {ex}", file=sys.stderr)
        raise SystemExit(1)
