"""Validate the frozen scope and generate its reviewable inventory (Python stdlib only)."""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys
from prototype import CUSTOM_MACHINES, LIVE_IMPLEMENTATIONS, generate as generate_content
from fish_smoker import FISH_SMOKER, fish_smoker_patch

ROOT = Path(__file__).resolve().parents[1]
PACK = Path("content/XiangyeShiling.Content")
NAMESPACE = "Sora6A.XiangyeShiling"
GROUP_COUNTS = {
    "forage": 4, "crop": 2, "shop_ingredient": 2, "bean_product": 6,
    "prepared_soy": 3, "fermented": 4, "smoked": 2, "dish": 13, "seed": 2, "machine": 2,
}
STATION_COUNTS = {"bean_workshop": 6, "fermentation_crock": 4, "fish_smoker": 2, "kitchen": 16, "crafting": 2}
P1_ITEMS = {"Soybean", "ChineseToon", "Tofu", "SoySauce", "ToonTofu", "BeanWorkshop", "FermentationCrock"}
P1_RECIPES = {f"{NAMESPACE}_Recipe_{key}" for key in ("Tofu", "SoySauce", "ToonTofu", "BeanWorkshop", "FermentationCrock")}
SEASONS = {"spring", "summer", "fall", "winter"}
FORAGE_MAPS = {"Forest", "Mountain", "BusStop"}
EGG_CATEGORY = "-5"


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)


def obtainable_items(items, recipes, roots, *, active_sources_only=False):
    sources = [(item["qualifiedId"], source) for item in items for source in item["sources"]
               if not active_sources_only or source.get("implementation") == "active"]
    reachable = roots | {item_id for item_id, source in sources if source["kind"] in {"shop", "forage"}}
    while True:
        before = len(reachable)
        for item_id, source in sources:
            if source["kind"] == "crop" and source["seed"] in reachable:
                reachable.add(item_id)
        for recipe in recipes:
            machine = recipe["machine"]
            if (not machine or machine == FISH_SMOKER or machine in reachable) and all(
                    entry["item"] in reachable for entry in recipe["inputs"]):
                reachable.add(recipe["output"]["item"])
        if len(reachable) == before:
            return reachable


def validate(catalog: dict) -> list[str]:
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    require(catalog["schemaVersion"] == 1, "Unsupported schemaVersion")
    require(catalog["designVersion"] == "0.4", "Design baseline must be 0.4")
    require(catalog["namespace"] == NAMESPACE, "Stable namespace changed")
    require(catalog["phase"] in {"P1", "P2"}, "Unsupported catalog phase")
    items, recipes = catalog["items"], catalog["recipes"]
    require(Counter(i["group"] for i in items) == GROUP_COUNTS, "Frozen item counts changed")
    require(Counter(r["station"] for r in recipes) == STATION_COUNTS, "Frozen recipe counts changed")
    for label, values in (("item ID", [i["id"] for i in items]), ("item key", [i["key"] for i in items]),
                          ("recipe ID", [r["id"] for r in recipes])):
        require(len(values) == len(set(values)), f"Duplicate {label}")
    by_id = {i["qualifiedId"]: i for i in items}
    by_recipe = {r["id"]: r for r in recipes}
    vanilla = {i["item"] for i in catalog["vanillaItems"]}
    known = set(by_id) | vanilla
    live_items = {i["qualifiedId"] for i in items if i["implementation"] in LIVE_IMPLEMENTATIONS}
    live_recipes = {r["id"] for r in recipes if r["implementation"] in LIVE_IMPLEMENTATIONS}
    textures = {}
    crop_seeds = set()
    forage_maps = set()

    def check_implementation(implementation, key, prototype_keys, kind):
        require(implementation in LIVE_IMPLEMENTATIONS | {"planned"}, f"Unknown implementation: {key}")
        if implementation == "prototype":
            require(key in prototype_keys, f"P1 prototype {kind} scope changed")
        if key in prototype_keys:
            require(implementation in LIVE_IMPLEMENTATIONS, f"P1 entry cannot be disabled: {key}")
        if implementation == "active":
            require(catalog["phase"] == "P2", f"P2 content enabled in P1: {key}")

    def check_texture(art, label):
        asset, path = art["asset"], art.get("file", "")
        require(asset.startswith(f"Mods/{NAMESPACE}/"), f"Art namespace mismatch: {label}")
        require(re.fullmatch(r"assets/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.png", path) is not None,
                f"Invalid content art file: {label}")
        require(textures.setdefault(asset, path) == path, f"Conflicting texture files: {label}")

    consumed = {entry["item"] for r in recipes for entry in r["inputs"]}
    for item in items:
        label = item["key"]
        check_implementation(item["implementation"], label, P1_ITEMS, "item")
        require(re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", label) is not None, f"Invalid item key: {label}")
        require(item["id"] == f"{NAMESPACE}_{label}", f"ID/namespace mismatch: {label}")
        prefix = "(BC)" if item["group"] == "machine" else "(O)"
        require(item["qualifiedId"] == prefix + item["id"], f"Qualified ID mismatch: {label}")
        for field in ("name", "description"):
            require(item[f"{field}Key"] == f"item.{label}.{field}", f"Invalid translation key: {label}")
            for locale in ("en", "zh"):
                require(bool(item["text"][locale][field].strip()), f"Missing {locale} {field}: {label}")
        require(bool(item["sources"]), f"Missing source: {label}")
        art = item["art"]
        if item["qualifiedId"] in live_items:
            balance = item["balance"]
            require(type(balance["price"]) is int and balance["price"] >= 0, f"Missing active item price: {label}")
            require(item["group"] == "machine" or type(balance["edibility"]) is int, f"Missing active item edibility: {label}")
            if art["status"] == "ready":
                check_texture(art, label)
            else:
                placeholder = item.get("prototypeArt", {})
                require(bool(placeholder.get("Texture")) and type(placeholder.get("SpriteIndex")) is int
                        and placeholder["SpriteIndex"] >= 0, f"Missing temporary item art: {label}")
        require(art["asset"].startswith(f"Mods/{NAMESPACE}/"), f"Art namespace mismatch: {label}")
        require(art["spriteIndex"] >= 0, f"Invalid sprite: {label}")
        require(art["size"] == ([16, 32] if prefix == "(BC)" else [16, 16]), f"Invalid sprite size: {label}")
        for source in item["sources"]:
            kind = source["kind"]
            is_live = source.get("implementation") == "active"
            require(kind in {"forage", "crop", "shop", "recipe"}, f"Unknown source kind: {label}")
            if is_live:
                require(catalog["phase"] == "P2" and item["qualifiedId"] in live_items,
                        f"Active source needs an enabled P2 item: {label}")
                seasons = source.get("seasons", [])
                require(bool(seasons) and len(seasons) == len(set(seasons)) and set(seasons) <= SEASONS,
                        f"Invalid source seasons: {label}")
            if kind == "recipe":
                recipe = by_recipe.get(source["recipe"])
                require(recipe is not None and recipe["output"]["item"] == item["qualifiedId"], f"Broken producer: {label}")
            elif kind == "crop":
                seed = by_id.get(source["seed"])
                require(seed is not None and seed["group"] == "seed", f"Broken seed: {label}")
                require(bool(source["seasons"]), f"Missing crop seasons: {label}")
                if is_live:
                    require(source["seed"] in live_items and source["seed"] not in crop_seeds,
                            f"Disabled or shared crop seed: {label}")
                    crop_seeds.add(source["seed"])
                    seed_seasons = [s["seasons"] for s in (seed or {}).get("sources", [])
                                    if s["kind"] == "shop" and s.get("implementation") == "active"]
                    require(seed_seasons == [source["seasons"]], f"Seed sale seasons differ from crop seasons: {label}")
                    days = source.get("daysInPhase", [])
                    require(len(days) == 5 and all(type(day) is int and day > 0 for day in days),
                            f"Invalid crop growth phases: {label}")
                    crop_art = source["art"]
                    check_texture(crop_art, label)
                    require(type(crop_art["spriteIndex"]) is int and crop_art["spriteIndex"] >= 0,
                            f"Invalid crop sprite index: {label}")
            elif kind == "shop":
                require(source["shop"] in {"SeedShop", "Saloon"}, f"Wrong ingredient shop: {label}")
                if is_live:
                    price, stock = source.get("price"), source.get("dailyStock")
                    require(type(price) is int and price > 0, f"Invalid active stock price: {label}")
                    require(type(stock) is int and (stock == -1 or stock > 0), f"Invalid active daily stock: {label}")
                    if source.get("role") == "limited-off-season-restock":
                        growth_seasons = {s for crop in item["sources"] if crop["kind"] == "crop" for s in crop["seasons"]}
                        require(set(source["seasons"]) == SEASONS - growth_seasons,
                                f"Off-season stock overlaps crop availability: {label}")
                        require(type(stock) is int and stock > 0, f"Off-season stock must be finite: {label}")
            elif kind == "forage":
                require(source["seasons"] == ["spring"], f"Forage outside spring: {label}")
                require(bool(source["locations"]) and len(source["locations"]) == len(set(source["locations"]))
                        and set(source["locations"]) <= FORAGE_MAPS, f"Invalid forage whitelist: {label}")
                if is_live:
                    forage_maps.update(source["locations"])
        if item["group"] in {"forage", "crop", "shop_ingredient", "bean_product", "fermented", "smoked"}:
            require(item["qualifiedId"] in consumed or "eat" in item["terminalUses"], f"No non-sale use: {label}")
    sprites = [(i["art"]["asset"], i["art"]["spriteIndex"]) for i in items]
    require(len(sprites) == len(set(sprites)), "Conflicting reserved sprites")
    forage_chances = catalog.get("springForageChance", {})
    require(forage_maps == set(forage_chances) and all(type(v) in {int, float} and 0 < v <= 1 for v in forage_chances.values()),
            "Active spring forage maps need one valid chance each")
    for recipe in recipes:
        label = recipe["id"]
        check_implementation(recipe["implementation"], label, P1_RECIPES, "recipe")
        is_live = label in live_recipes
        require(label.startswith(NAMESPACE + "_Recipe_"), f"Recipe namespace mismatch: {label}")
        require(recipe["output"]["item"] in by_id, f"Unknown output: {label}")
        require(bool(recipe["inputs"]), f"Recipe without inputs: {label}")
        input_ids = [e["item"] for e in recipe["inputs"]]
        require(len(input_ids) == len(set(input_ids)), f"Duplicate input: {label}")
        for entry in recipe["inputs"]:
            require(entry["item"] in known, f"Unknown input {entry['item']}: {label}")
            require(entry["item"] != EGG_CATEGORY or recipe["station"] == "kitchen", f"Egg category outside cooking: {label}")
        for value in [e["quantity"] for e in recipe["inputs"]] + [recipe["output"]["quantity"]]:
            require(value is None or (type(value) is int and value > 0), f"Invalid quantity: {label}")
            if is_live:
                require(value is not None, f"Active recipe has unset quantity: {label}")
        if recipe["station"] in CUSTOM_MACHINES:
            expected = CUSTOM_MACHINES[recipe["station"]]
            require(recipe["machine"] == f"(BC){NAMESPACE}_{expected}", f"Wrong machine: {label}")
        if recipe["station"] == "fish_smoker":
            require(recipe["machine"] == FISH_SMOKER, f"Wrong machine: {label}")
        if recipe["station"] in {"kitchen", "crafting"}:
            shop = "Saloon" if recipe["station"] == "kitchen" else "Carpenter"
            require(recipe["unlock"]["kind"] == "shop-recipe" and recipe["unlock"]["shop"] == shop, f"Wrong recipe unlock: {label}")
            require(recipe["machine"] is None, f"Non-machine recipe has a machine: {label}")
            if is_live:
                output = by_id.get(recipe["output"]["item"], {})
                require(output.get("internalName") == label, f"Recipe purchase name mismatch: {label}")
                price = recipe["unlock"].get("price")
                require(type(price) is int and price > 0, f"Active recipe has no shop price: {label}")
        minutes = recipe["minutes"]
        require(minutes is None or (type(minutes) is int and minutes >= 0), f"Invalid duration: {label}")
        if is_live:
            require(recipe["output"]["item"] in live_items, f"Active recipe output is disabled: {label}")
            require(set(input_ids) <= live_items | vanilla, f"Active recipe input is disabled: {label}")
            if recipe["machine"]:
                require(type(minutes) is int and minutes > 0, f"Active machine has no duration: {label}")
                require(recipe["machine"] in live_items or recipe["machine"] == FISH_SMOKER, f"Active machine is disabled: {label}")
    try:
        fish_smoker_patch(r for r in recipes if r["id"] in live_recipes)
    except ValueError as error:
        errors.append(str(error))
    # Check the frozen graph and the actually installed subset, including legacy supplies until superseded.
    reachable = obtainable_items(items, recipes, vanilla)
    require(set(by_id) <= reachable, f"Unreachable items: {sorted(set(by_id) - reachable)}")
    legacy_supplies = {row["ItemId"] for stock in catalog.get("prototypeShops", {}).values() for row in stock if not row.get("IsRecipe")}
    reachable = obtainable_items([item for item in items if item["qualifiedId"] in live_items],
                                 [recipe for recipe in recipes if recipe["id"] in live_recipes],
                                 vanilla | legacy_supplies, active_sources_only=True)
    require(live_items <= reachable, f"Unreachable active items: {sorted(live_items - reachable)}")
    for shop, stock in catalog.get("prototypeShops", {}).items():
        require(shop in {"SeedShop", "Saloon", "Carpenter"}, "Unknown prototype shop")
        for row in stock:
            require(row["ItemId"] in live_items | vanilla, f"Unavailable prototype shop item: {row['ItemId']}")
            require(type(row["Price"]) is int and row["Price"] > 0, "Invalid prototype shop price")
            if row.get("IsRecipe"):
                matching = [r for r in recipes if r["output"]["item"] == row["ItemId"] and r["id"] in P1_RECIPES]
                require(len(matching) == 1 and matching[0]["unlock"].get("shop") == shop
                        and matching[0]["unlock"].get("price") == row["Price"], "Prototype recipe stock differs from unlock")
            else:
                require(type(row.get("AvailableStock")) is int and row["AvailableStock"] > 0, "Prototype ingredients need limited stock")
    return errors


def generated_files(catalog: dict) -> dict[Path, str]:
    items, recipes = catalog["items"], catalog["recipes"]
    by_id = {i["qualifiedId"]: i for i in items}
    by_id.update({v["item"]: {"text": {"zh": {"name": v["key"]}}} for v in catalog["vanillaItems"]})

    def name(item_id):
        return by_id[item_id]["text"]["zh"]["name"]

    files = {}
    for locale, filename in (("en", "default"), ("zh", "zh")):
        strings = {i[f"{field}Key"]: i["text"][locale][field] for i in items for field in ("name", "description")}
        files[PACK / "i18n" / f"{filename}.json"] = json.dumps(strings, ensure_ascii=False, indent=2) + "\n"
    enabled_items = sum(i["implementation"] in LIVE_IMPLEMENTATIONS for i in items)
    enabled_recipes = sum(r["implementation"] in LIVE_IMPLEMENTATIONS for r in recipes)
    lines = ["# 内容登记表", "", "由 `data/catalog.json` 生成；修改清单后运行 `python tools/catalog.py --write`。",
             "", "设计基线 0.4；40 个物品 ID、30 个配方 ID。清单是预留范围，不代表已实现。",
             f"当前阶段 {catalog['phase']}，游戏数据已启用 {enabled_items} 个物品和 {enabled_recipes} 个配方。`prototype` 为沿用原型条目，`active` 为已启用的 P2 条目，`planned` 仅登记。",
             "料理和熟食可作为终点。煤炭与设备制作材料为待实机核对的暂定方案。", "",
             "## ID 与本地化", "", f"物品：`{NAMESPACE}_<Key>`；配方：`{NAMESPACE}_Recipe_<Key>`。",
             "普通物品使用 `(O)` 前缀，设备使用 `(BC)`；数据表键不带此前缀。显示名不参与 ID。",
             "文本键为 `item.<Key>.name` / `item.<Key>.description`，英文默认与中文已登记。", "",
             "## 物品", "", "| Key | 中文 | 类别 | 来源 | 去向 | 图像位置 | 状态 |", "|---|---|---|---|---|---|---|"]
    for item in items:
        source_text = []
        for source in item["sources"]:
            kind = source["kind"]
            if kind == "forage":
                source_text.append("春季 " + ", ".join(source["locations"]) + "（地图待验）")
            elif kind == "crop":
                days = source.get("daysInPhase", [])
                growth = f"，{sum(days)} 天" if days else ""
                source_text.append(name(source["seed"]) + "；" + ", ".join(source["seasons"]) + growth)
            elif kind == "shop":
                source_text.append(source["shop"] + (" 季外限量" if "role" in source else " 商店"))
            else:
                source_text.append("Recipe_" + item["key"])
        uses = [name(r["output"]["item"]) for r in recipes if any(e["item"] == item["qualifiedId"] for e in r["inputs"]) ]
        if item["group"] == "seed":
            uses += ["种植 " + name(i["qualifiedId"]) for i in items for s in i["sources"] if s.get("seed") == item["qualifiedId"]]
        if item["group"] == "machine":
            uses += ["加工 " + name(r["output"]["item"]) for r in recipes if r["machine"] == item["qualifiedId"]]
        uses += item["terminalUses"]
        art = item["art"]
        art_status = "已绘制" if art["status"] == "ready" else "待绘制"
        lines.append(f"| `{item['key']}` | {name(item['qualifiedId'])} | {item['group']} | {'；'.join(source_text)} | {'、'.join(uses)} | {art['asset'].rsplit('/', 1)[1]} #{art['spriteIndex']}，{art_status} | {item['implementation']} |")
    lines += ["", "## 配方", "", "数量和工时见清单 JSON。机器菜单配方随设备可用，图纸和菜谱由商店学习。",
              "", "| 配方 ID 后缀 | 设施 | 输入 | 输出 | 解锁 | 状态 |", "|---|---|---|---|---|---|"]
    for recipe in recipes:
        unlock = recipe["unlock"]
        lines.append(f"| `{recipe['id'].split('_Recipe_')[1]}` | {recipe['station']} | {'＋'.join(name(e['item']) for e in recipe['inputs'])} | {name(recipe['output']['item'])} | {unlock.get('shop', '随设备可用')} | {recipe['implementation']} |")
    lines += ["", "## 发布前核查", "", "- 图像在地图、商店、料理和设备界面的实际显示。",
              "- 分类、职业加成、赠礼、收集与完成度行为。",
              "- 生长天数、补货数量、价格、食用恢复量、每批产出与耗时。",
              "- 厨房的原版蛋类用料规则和熏鱼机追加规则，核对原版鱼类加工。", ""]
    lines += ["## 商店与地图数据", "", "正式来源按各条目的季节和库存生成。启用作物季外供给和野菜拾取后，重叠的原型库存退出。",
              "小麦沿用原版获取。既有图纸和菜谱的购买身份保持稳定。", ""]
    files[Path("docs/CONTENT_CATALOG.md")] = "\n".join(lines)
    files.update(generate_content(catalog))
    return files


def check_repository(root: Path = ROOT) -> list[str]:
    errors = []
    # Parse every maintained JSON file, including duplicate-key checks.
    for folder in ("data", "content", "src"):
        for path in (root / folder).rglob("*.json"):
            if not {"obj", "bin"}.intersection(path.parts):
                read_json(path)
    catalog = read_json(root / "data/catalog.json")
    errors.extend(validate(catalog))
    if errors:
        return errors
    for relative, expected in generated_files(catalog).items():
        path = root / relative
        if not path.exists() or path.read_text(encoding="utf-8") != expected:
            errors.append(f"Generated file is stale: {relative}; run tools/catalog.py --write")
    code = read_json(root / "src/XiangyeShiling/manifest.json")
    pack = read_json(root / PACK / "manifest.json")
    if code["UniqueID"] != NAMESPACE or pack["UniqueID"] != NAMESPACE + ".Content":
        errors.append("Manifest namespace mismatch")
    if code["Version"] != pack["Version"]:
        errors.append("Code/content version mismatch")
    module_dependencies = [d for d in pack.get("Dependencies", []) if d.get("UniqueID") == NAMESPACE]
    if module_dependencies != [{"UniqueID": NAMESPACE, "MinimumVersion": code["Version"], "IsRequired": True}]:
        errors.append("Content pack must require the matching machine module version")
    if code["MinimumApiVersion"] != "4.4.0" or pack["MinimumApiVersion"] != "4.4.0":
        errors.append("CP 2.9 requires the SMAPI 4.4.0 baseline")
    if pack["ContentPackFor"] != {"UniqueID": "Pathoschild.ContentPatcher", "MinimumVersion": "2.9.0"}:
        errors.append("Content Patcher dependency differs from the tested baseline")
    project = (root / "src/XiangyeShiling/XiangyeShiling.csproj").read_text(encoding="utf-8")
    if f"<Version>{code['Version']}</Version>" not in project:
        errors.append("Project/manifest version mismatch")
    changes = read_json(root / PACK / "content.json")["Changes"]
    # The generated-file comparison above validates every active patch and runtime recipe,
    # including shop/crafting identity; don't maintain a second hand-written content source.
    strings = read_json(root / PACK / "i18n/default.json")
    for change in changes:
        if change["Action"] == "Load" and not (root / PACK / change["FromFile"]).is_file():
            errors.append(f"Missing generated content asset: {change['FromFile']}")
    for key in re.findall(r"\{\{i18n:\s*([^} ]+)\s*\}\}", json.dumps(changes)):
        if key not in strings:
            errors.append(f"Missing CP translation: {key}")
    code_en = read_json(root / "src/XiangyeShiling/i18n/default.json")
    code_zh = read_json(root / "src/XiangyeShiling/i18n/zh.json")
    if code_en.keys() != code_zh.keys():
        errors.append("Code translation keys differ by locale")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Regenerate translations and the Markdown inventory")
    args = parser.parse_args()
    try:
        catalog = read_json(ROOT / "data/catalog.json")
        errors = validate(catalog)
        if not errors and args.write:
            for relative, content in generated_files(catalog).items():
                path = ROOT / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8", newline="\n")
        errors.extend(check_repository())
        if errors:
            for error in dict.fromkeys(errors):
                print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print("PASS: 40 items, 30 recipes, closed source/use graph, manifests and generated translations.")
        return 0
    except (KeyError, TypeError, ValueError, OSError) as ex:
        print(f"ERROR: invalid catalog or file: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
