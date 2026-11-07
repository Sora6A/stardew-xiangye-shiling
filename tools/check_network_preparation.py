"""Read-only NET02 persisted-farmhand check against native 1.6 save structure.

This proves a named, customized farmer is in the authoritative direct farmhands list,
not that two game processes connected, accepted input, saved together, or exited.
Archive separate native logs, process evidence and before/after save hashes for those claims.
Never changes a save/config or starts a game; JSON summary is written only to stdout.
"""
import argparse
import json
from pathlib import Path
import re
import sys

import configure_network_fixture as fixture


def _text(element, tag):
    return fixture._text(fixture._child(element, tag))


def _player_id(element):
    # Farmer.UniqueMultiplayerID is a signed Int64; do not reject legitimate negative IDs.
    raw = _text(element, "UniqueMultiplayerID")
    fixture._require(re.fullmatch(r"-?(?:0|[1-9][0-9]*)", raw) is not None, "Invalid native farmer ID.")
    value = int(raw)
    fixture._require(-(2**63) <= value < 2**63 and value != 0, "Farmer ID must be a nonzero Int64.")
    return value


def _plain_expected(value, label):
    fixture._require(isinstance(value, str) and value and len(value) <= 64
                     and all(ord(c) >= 32 for c in value), f"Invalid expected {label}.")
    return value


def _farmhand_name(farmer):
    element = fixture._child(farmer, "name")
    fixture._require(len(element) == 0, "Native farmhand name must be plain text.")
    customized = _text(farmer, "isCustomized")
    fixture._require(customized in ("true", "false"), "Invalid native character creation flag.")
    name = element.text or ""
    # Existing native saves contain unnamed/uncreated vacant-cabin placeholders.
    # Permit their empty name, but never count them or a customized nameless record as the client.
    fixture._require(bool(name) or customized == "false", "Customized farmhand has no name.")
    return name


def check_preparation(save_dir, *, farm_name, expected_world_id, client_name, favorite_thing=None):
    """Check the exact native roster; cabin/default/host/nested names never count as the client.

    SaveGame.farmhands contains serialized Farmer entries in 1.6, restored into
    netWorldState.farmhandData on load. A default uncustomized cabin farmer is not a
    prepared client. No assumptions about client's farmName are made: native client
    identity belongs to this world's roster, and a farmhand's own farmName may be empty.
    Re-reading both files detects observed concurrent changes; it is not a game lock
    or proof of the native save sequence, so use this after both processes have exited.
    """
    client_name = _plain_expected(client_name, "client name")
    if favorite_thing is not None:
        favorite_thing = _plain_expected(favorite_thing, "favorite thing")
    fixture._require(type(expected_world_id) is int and 0 < expected_world_id < 2**64,
                     "Expected world ID must be a nonzero UInt64.")
    identity = fixture.read_fixture(save_dir, farm_name)
    fixture._require(identity["saveFixtureId"] == expected_world_id, "Save belongs to a different world.")
    save = fixture._plain_path(save_dir)
    world, main_sha = fixture._xml(save / save.name, "SaveGame")
    info, info_sha = fixture._xml(save / "SaveGameInfo", "Farmer")
    fixture._require({save.name: main_sha, "SaveGameInfo": info_sha} == identity["saveHashes"],
                     "Save changed while reading; retry only after native saving and game exit.")
    host = fixture._child(world, "player")
    host_id = _player_id(host)
    fixture._require(_player_id(info) == host_id and _text(host, "name") == _text(info, "name"),
                     "SaveGameInfo does not describe the same host farmer.")
    fixture._require(_text(host, "name") != client_name, "Host name cannot stand in for a persisted client.")
    roster = fixture._child(world, "farmhands")
    fixture._require(all(child.tag == "Farmer" for child in roster), "Unsupported native farmhand record.")
    matches = [farmer for farmer in roster if _farmhand_name(farmer) == client_name]
    fixture._require(len(matches) == 1, "Expected exactly one named client in direct farmhands/Farmer.")
    client = matches[0]
    fixture._require(_text(client, "isCustomized") == "true", "Client farmer character creation is not complete.")
    client_id = _player_id(client)
    fixture._require(client_id != host_id, "Client farmer cannot reuse host identity.")
    # Other uncustomized cabin placeholders may contain ID0; only reject aliasing this real client.
    for farmer in roster:
        if farmer is not client:
            raw = _text(farmer, "UniqueMultiplayerID")
            fixture._require(re.fullmatch(r"-?(?:0|[1-9][0-9]*)", raw) is not None
                             and -(2**63) <= int(raw) < 2**63, "Invalid other native farmhand ID.")
            fixture._require(int(raw) != client_id, "Another farmhand aliases the named client ID.")
    if favorite_thing is not None:
        fixture._require(_text(client, "favoriteThing") == favorite_thing, "Client favorite thing differs from preparation instructions.")
    year_raw, day_raw = _text(world, "year"), _text(world, "dayOfMonth")
    fixture._require(re.fullmatch(r"[1-9][0-9]*", year_raw) is not None
                     and re.fullmatch(r"[1-9][0-9]*", day_raw) is not None, "Invalid native save date.")
    day, year = int(day_raw), int(year_raw)
    season = _text(world, "currentSeason")
    fixture._require(day <= 28 and year <= 2**31 - 1 and season in ("spring", "summer", "fall", "winter"),
                     "Invalid native save date.")
    # Bound re-read uses the same no-DTD, unlinked file parser. Do not silently validate an earlier version.
    _, final_main = fixture._xml(save / save.name, "SaveGame")
    _, final_info = fixture._xml(save / "SaveGameInfo", "Farmer")
    fixture._require(main_sha == final_main and info_sha == final_info,
                     "Save changed during validation; no stable preparation result.")
    return {"schemaVersion": 1, "evidenceKind": "NativeSaveXmlOnly", **identity,
            "year": year, "season": season, "day": day, "hostPlayerId": host_id,
            "clientPlayerId": client_id, "clientName": client_name, "clientFarmerPersisted": True,
            "clientCustomized": True, "favoriteThingChecked": favorite_thing is not None,
            "nativeConnectionVerified": False, "clientInputVerified": False,
            "nativeSaveSequenceVerified": False, "gameExitVerified": False, "fullNet02Accepted": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--save", type=Path, required=True)
    parser.add_argument("--farm-name", required=True)
    parser.add_argument("--expected-world-id", type=int, required=True)
    parser.add_argument("--client-name", required=True)
    parser.add_argument("--favorite-thing")
    args = parser.parse_args()
    result = check_preparation(args.save, farm_name=args.farm_name, expected_world_id=args.expected_world_id,
                               client_name=args.client_name, favorite_thing=args.favorite_thing)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError) as error:
        print(f"PREPARATION CHECK FAILED: {error}", file=sys.stderr)
        raise SystemExit(1)
