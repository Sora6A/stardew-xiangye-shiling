"""Validate the frozen scope and generate its reviewable inventory (Python stdlib only)."""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys
from prototype import generate as generate_prototype

ROOT = Path(__file__).resolve().parents[1]
PACK = Path("content/XiangyeShiling.Content")
NAMESPACE = "Sora6A.XiangyeShiling"
GROUP_COUNTS = {
    "forage": 4, "crop": 2, "shop_ingredient": 2, "bean_product": 6,
    "prepared_soy": 3, "fermented": 4, "smoked": 2, "dish": 13, "seed": 2, "machine": 2,
}
STATION_COUNTS = {"bean_workshop": 6, "fermentation_crock": 4, "fish_smoker": 2, "kitchen": 16, "crafting": 2}


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)


def validate(catalog: dict) -> list[str]:
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    require(catalog["schemaVersion"] == 1, "Unsupported schemaVersion")
    require(catalog["designVersion"] == "0.4", "Design baseline must be 0.4")
    require(catalog["namespace"] == NAMESPACE, "Stable namespace changed")
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
    consumed = {entry["item"] for r in recipes for entry in r["inputs"]}
    for item in items:
        label = item["key"]
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
        if item["implementation"] == "prototype":
            balance = item["balance"]
            require(type(balance["price"]) is int and balance["price"] >= 0, f"Missing prototype price: {label}")
            require(item["group"] == "machine" or type(balance["edibility"]) is int, f"Missing prototype edibility: {label}")
            require("prototypeArt" in item, f"Missing prototype placeholder: {label}")
        require(art["asset"].startswith(f"Mods/{NAMESPACE}/"), f"Art namespace mismatch: {label}")
        require(art["spriteIndex"] >= 0, f"Invalid sprite: {label}")
        require(art["size"] == ([16, 32] if prefix == "(BC)" else [16, 16]), f"Invalid sprite size: {label}")
        for source in item["sources"]:
            kind = source["kind"]
            require(kind in {"forage", "crop", "shop", "recipe"}, f"Unknown source kind: {label}")
            if kind == "recipe":
                recipe = by_recipe.get(source["recipe"])
                require(recipe is not None and recipe["output"]["item"] == item["qualifiedId"], f"Broken producer: {label}")
            elif kind == "crop":
                seed = by_id.get(source["seed"])
                require(seed is not None and seed["group"] == "seed", f"Broken seed: {label}")
                require(bool(source["seasons"]), f"Missing crop seasons: {label}")
            elif kind == "shop":
                require(source["shop"] in {"SeedShop", "Saloon"}, f"Wrong ingredient shop: {label}")
            elif kind == "forage":
                require(source["seasons"] == ["spring"], f"Forage outside spring: {label}")
                require(bool(source["locations"]), f"Missing forage whitelist: {label}")
        if item["group"] in {"forage", "crop", "shop_ingredient", "bean_product", "fermented", "smoked"}:
            require(item["qualifiedId"] in consumed or "eat" in item["terminalUses"], f"No non-sale use: {label}")
    sprites = [(i["art"]["asset"], i["art"]["spriteIndex"]) for i in items]
    require(len(sprites) == len(set(sprites)), "Conflicting reserved sprites")
    for recipe in recipes:
        label = recipe["id"]
        require(label.startswith(NAMESPACE + "_Recipe_"), f"Recipe namespace mismatch: {label}")
        require(recipe["output"]["item"] in by_id, f"Unknown output: {label}")
        require(bool(recipe["inputs"]), f"Recipe without inputs: {label}")
        input_ids = [e["item"] for e in recipe["inputs"]]
        require(len(input_ids) == len(set(input_ids)), f"Duplicate input: {label}")
        for entry in recipe["inputs"]:
            require(entry["item"] in known, f"Unknown input {entry['item']}: {label}")
        for value in [e["quantity"] for e in recipe["inputs"]] + [recipe["output"]["quantity"]]:
            require(value is None or (type(value) is int and value > 0), f"Invalid quantity: {label}")
            if recipe["implementation"] != "planned":
                require(value is not None, f"Active recipe has unset quantity: {label}")
        if recipe["station"] in {"bean_workshop", "fermentation_crock"}:
            expected = "BeanWorkshop" if recipe["station"] == "bean_workshop" else "FermentationCrock"
            require(recipe["machine"] == f"(BC){NAMESPACE}_{expected}", f"Wrong machine: {label}")
        if recipe["station"] in {"kitchen", "crafting"}:
            shop = "Saloon" if recipe["station"] == "kitchen" else "Carpenter"
            require(recipe["unlock"]["kind"] == "shop-recipe" and recipe["unlock"]["shop"] == shop, f"Wrong recipe unlock: {label}")
        minutes = recipe["minutes"]
        require(minutes is None or (type(minutes) is int and minutes >= 0), f"Invalid duration: {label}")
        if recipe["implementation"] != "planned" and recipe["machine"]:
            require(minutes is not None and minutes > 0, f"Active machine has no duration: {label}")
    # Detect closed production loops with no obtainable starting materials.
    reachable = set(vanilla)
    reachable.update(i["qualifiedId"] for i in items if any(s["kind"] in {"shop", "forage"} for s in i["sources"]))
    while True:
        before = len(reachable)
        for item in items:
            if any(s["kind"] == "crop" and s["seed"] in reachable for s in item["sources"]):
                reachable.add(item["qualifiedId"])
        for recipe in recipes:
            machine = recipe["machine"]
            if all(e["item"] in reachable for e in recipe["inputs"]) and (not machine or machine == "(BC)FishSmoker" or machine in reachable):
                reachable.add(recipe["output"]["item"])
        if len(reachable) == before:
            break
    require(set(by_id) <= reachable, f"Unreachable items: {sorted(set(by_id) - reachable)}")
    expected_items = {"Soybean", "ChineseToon", "Tofu", "SoySauce", "ToonTofu", "BeanWorkshop", "FermentationCrock"}
    require({i["key"] for i in items if i["implementation"] == "prototype"} == expected_items, "P1 prototype item scope changed")
    expected_recipes = {f"{NAMESPACE}_Recipe_{k}" for k in ("Tofu", "SoySauce", "ToonTofu", "BeanWorkshop", "FermentationCrock")}
    require({r["id"] for r in recipes if r["implementation"] == "prototype"} == expected_recipes, "P1 prototype recipe scope changed")
    for recipe in recipes:
        if recipe["implementation"] == "prototype" and recipe["station"] in {"crafting", "kitchen"}:
            require(by_id[recipe["output"]["item"]].get("internalName") == recipe["id"], f"Recipe purchase name mismatch: {recipe['id']}")
    for shop, stock in catalog.get("prototypeShops", {}).items():
        require(shop in {"SeedShop", "Saloon", "Carpenter"}, "Unknown prototype shop")
        for row in stock:
            require(row["ItemId"] in known, f"Unknown shop item: {row['ItemId']}")
            require(type(row["Price"]) is int and row["Price"] > 0, "Invalid prototype shop price")
            if row.get("IsRecipe"):
                matching = [r for r in recipes if r["output"]["item"] == row["ItemId"] and r["id"] in expected_recipes]
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
    lines = ["# 内容登记表", "", "由 `data/catalog.json` 生成；修改清单后运行 `python tools/catalog.py --write`。",
             "", "设计基线 0.4；40 个物品 ID、30 个配方 ID。清单是预留范围，不代表已实现。",
             "P1 激活 7 个物品与 5 个配方，仅用于两条单人原型流程；其他 planned 项不注入游戏。`null` 表示待定，不能作为运行时数据。",
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
                source_text.append(name(source["seed"]) + "；" + ", ".join(source["seasons"]))
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
        lines.append(f"| `{item['key']}` | {name(item['qualifiedId'])} | {item['group']} | {'；'.join(source_text)} | {'、'.join(uses)} | {art['asset'].rsplit('/', 1)[1]} #{art['spriteIndex']}，待绘制 | {item['implementation']} |")
    lines += ["", "## 配方", "", "prototype 项已有暂定数量与工时（见 JSON）；其余 planned 项待平衡。机器菜单配方随设备可用，图纸/菜谱由商店学习。",
              "", "| 配方 ID 后缀 | 设施 | 输入 | 输出 | 解锁 |", "|---|---|---|---|---|"]
    for recipe in recipes:
        unlock = recipe["unlock"]
        lines.append(f"| `{recipe['id'].split('_Recipe_')[1]}` | {recipe['station']} | {'＋'.join(name(e['item']) for e in recipe['inputs'])} | {name(recipe['output']['item'])} | {unlock.get('shop', '随设备可用')} |")
    lines += ["", "## 未锁定项", "", "- 新美术、作物阶段、机器状态图；P0 黄豆暂用原版绿豆图标（仅资源引用，不分发原版图片）。",
              "- 分类/职业加成、赠礼、收集与完成度行为须在 P3 核查；P0 黄豆暂不计入出货收集。",
              "- 生长天数、补货数量、价格、食用恢复量、每批产出与耗时。",
              "- 鸡蛋暂以原版普通白鸡蛋 ID 登记，P1 核查大小/颜色替代规则。",
              "- 熏鱼机煤炭耗用、正常鱼类加工回归；实际补丁只能追加规则。", ""]
    lines += ["## P1 暂定供给", "", "皮埃尔全年限量供应黄豆/小麦，格斯限量供应香椿并售香椿拌豆腐菜谱，罗宾售两台设备图纸。",
              "这些库存为原型补给，不代表 P2 的作物、春季采集和季外补货规则已经实现。数值见 catalog.prototypeShops。", ""]
    files[Path("docs/CONTENT_CATALOG.md")] = "\n".join(lines)
    files.update(generate_prototype(catalog))
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
    # The generated-file comparison above validates every prototype patch and runtime recipe,
    # including shop/crafting identity; don't maintain a second hand-written content source.
    strings = read_json(root / PACK / "i18n/default.json")
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
