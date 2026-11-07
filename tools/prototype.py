"""Generate the bounded P1 prototype from the frozen catalog; no game binaries or artwork."""
import json
from pathlib import Path

PACK = Path("content/XiangyeShiling.Content")
RUNTIME = Path("src/XiangyeShiling/assets/machine-recipes.json")


def generate(catalog):
    active = [i for i in catalog["items"] if i["implementation"] == "prototype"]
    recipes = [r for r in catalog["recipes"] if r["implementation"] == "prototype"]
    by_id = {i["qualifiedId"]: i for i in active}
    changes, objects, machines, cooking, crafting, runtime = [], {}, {}, {}, {}, []
    for item in active:
        entry = {"Name": item.get("internalName", item["id"]),
                 "DisplayName": "{{i18n: " + item["nameKey"] + "}}",
                 "Description": "{{i18n: " + item["descriptionKey"] + "}}",
                 "Price": item["balance"]["price"], **item["prototypeArt"]}
        if item["group"] == "machine":
            entry.update({"Fragility": 0, "CanBePlacedOutdoors": True, "CanBePlacedIndoors": True})
            machines[item["id"]] = entry
        else:
            entry.update({"Type": item["type"], "Category": item["category"],
                          "Edibility": item["balance"]["edibility"], "CanBeGivenAsGift": True,
                          "ExcludeFromShippingCollection": True, "ExcludeFromRandomSale": True})
            objects[item["id"]] = entry
    for recipe in recipes:
        if recipe["machine"]:
            runtime.append({"Id": recipe["id"], "Machine": "BeanWorkshop" if recipe["station"] == "bean_workshop" else "FermentationCrock",
                            "Inputs": [{"ItemId": i["item"], "Quantity": i["quantity"]} for i in recipe["inputs"]],
                            "Output": {"ItemId": recipe["output"]["item"], "Quantity": recipe["output"]["quantity"], "Quality": 0, "MaxStack": 999},
                            "Minutes": recipe["minutes"]})
        else:
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
    for target, entries in (("Data/Objects", objects), ("Data/BigCraftables", machines),
                            ("Data/CraftingRecipes", crafting), ("Data/CookingRecipes", cooking)):
        changes.append({"Action": "EditData", "Target": target, "Entries": entries,
                        "LogName": "P1 prototype - " + target})
    for shop, stock in catalog["prototypeShops"].items():
        entries = {}
        for index, value in enumerate(stock):
            stock_id = f"{catalog['namespace']}_P1_{shop}_{index}"
            # Price 是清单承诺的最终原型价格，不能再次应用 SeedShop 等商店的全局倍率。
            entries[stock_id] = {"Id": stock_id, **value, "IgnoreShopPriceModifiers": True}
        changes.append({"Action": "EditData", "Target": "Data/Shops", "TargetField": [shop, "Items"],
                        "Entries": entries, "LogName": "P1 prototype stock - " + shop})
    dump = lambda data: json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    return {PACK / "content.json": dump({"Format": "2.9.0", "Changes": changes}), RUNTIME: dump(runtime)}
