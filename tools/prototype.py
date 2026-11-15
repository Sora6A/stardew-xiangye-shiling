"""Generate installed game data from the enabled entries in the frozen catalog."""
import json
from pathlib import Path

PACK = Path("content/XiangyeShiling.Content")
RUNTIME = Path("src/XiangyeShiling/assets/machine-recipes.json")
LIVE_IMPLEMENTATIONS = {"prototype", "active"}
CUSTOM_MACHINES = {"bean_workshop": "BeanWorkshop", "fermentation_crock": "FermentationCrock"}


def generate(catalog):
    active = [i for i in catalog["items"] if i["implementation"] in LIVE_IMPLEMENTATIONS]
    recipes = [r for r in catalog["recipes"] if r["implementation"] in LIVE_IMPLEMENTATIONS]
    by_id = {i["qualifiedId"]: i for i in active}
    changes, objects, machines, cooking, crafting, runtime = [], {}, {}, {}, {}, []
    art_loads = {}
    for item in active:
        art = item["art"]
        if art["status"] == "ready":
            texture = {"Texture": art["asset"], "SpriteIndex": art["spriteIndex"]}
            art_loads[art["asset"]] = art["file"]
        else:
            texture = item["prototypeArt"]
        entry = {"Name": item.get("internalName", item["id"]),
                 "DisplayName": "{{i18n: " + item["nameKey"] + "}}",
                 "Description": "{{i18n: " + item["descriptionKey"] + "}}",
                 "Price": item["balance"]["price"], **texture}
        if item["group"] == "machine":
            entry.update({"Fragility": 0, "CanBePlacedOutdoors": True, "CanBePlacedIndoors": True})
            machines[item["id"]] = entry
        else:
            entry.update({"Type": item["type"], "Category": item["category"],
                          "Edibility": item["balance"]["edibility"], "CanBeGivenAsGift": True,
                          "ExcludeFromShippingCollection": True, "ExcludeFromRandomSale": True})
            objects[item["id"]] = entry
    for recipe in recipes:
        if recipe["station"] in CUSTOM_MACHINES:
            runtime.append({"Id": recipe["id"], "Machine": CUSTOM_MACHINES[recipe["station"]],
                            "Inputs": [{"ItemId": i["item"], "Quantity": i["quantity"]} for i in recipe["inputs"]],
                            "Output": {"ItemId": recipe["output"]["item"], "Quantity": recipe["output"]["quantity"], "Quality": 0, "MaxStack": 999},
                            "Minutes": recipe["minutes"]})
        elif recipe["station"] in {"kitchen", "crafting"}:
            output = by_id[recipe["output"]["item"]]
            inputs = " ".join(f"{i['item']} {i['quantity']}" for i in recipe["inputs"])
            result = f"{recipe['output']['item']} {recipe['output']['quantity']}"
            title = "{{i18n: " + output["nameKey"] + "}}"
            # Vanilla recipe purchases learn the output item's internal Name.
            assert output["internalName"] == recipe["id"]
            if recipe["station"] == "kitchen":
                cooking[recipe["id"]] = f"{inputs}/1 10/{result}/null/{title}"
            else:
                crafting[recipe["id"]] = f"{inputs}/Home/{result}/true/null/{title}"
        else:
            raise ValueError(f"Unsupported active recipe station: {recipe['station']}")
    for target, path in art_loads.items():
        changes.append({"Action": "Load", "Target": target, "FromFile": path})
    for target, entries in (("Data/Objects", objects), ("Data/BigCraftables", machines),
                            ("Data/CraftingRecipes", crafting), ("Data/CookingRecipes", cooking)):
        changes.append({"Action": "EditData", "Target": target, "Entries": entries,
                        "LogName": "P1 prototype - " + target})
    legacy_unlocks = set()
    for shop, stock in catalog["prototypeShops"].items():
        entries = {}
        for index, value in enumerate(stock):
            stock_id = f"{catalog['namespace']}_P1_{shop}_{index}"
            # Price 是清单承诺的最终原型价格，不能再次应用 SeedShop 等商店的全局倍率。
            entries[stock_id] = {"Id": stock_id, **value, "IgnoreShopPriceModifiers": True}
            if value.get("IsRecipe"):
                legacy_unlocks.add(value["ItemId"])
        changes.append({"Action": "EditData", "Target": "Data/Shops", "TargetField": [shop, "Items"],
                        "Entries": entries, "LogName": "P1 prototype stock - " + shop})
    for recipe in recipes:
        if recipe["implementation"] != "active" or recipe["station"] not in {"crafting", "kitchen"}:
            continue
        if recipe["output"]["item"] in legacy_unlocks:
            continue
        unlock = recipe["unlock"]
        stock_id = recipe["id"]
        stock = {"Id": stock_id, "ItemId": recipe["output"]["item"], "IsRecipe": True,
                 "Price": unlock["price"], "IgnoreShopPriceModifiers": True}
        changes.append({"Action": "EditData", "Target": "Data/Shops", "TargetField": [unlock["shop"], "Items"],
                        "Entries": {stock_id: stock}, "LogName": stock_id})
    dump = lambda data: json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    return {PACK / "content.json": dump({"Format": "2.9.0", "Changes": changes}), RUNTIME: dump(runtime)}
