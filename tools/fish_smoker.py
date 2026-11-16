"""Generate additive native Fish Smoker rules for enabled catalog recipes."""

FISH_SMOKER = "(BC)FishSmoker"
COAL = "(O)382"


def fish_smoker_patch(recipes):
    """Return a nested Content Patcher edit, or None when no smoker recipes are enabled."""
    rules = {}
    for recipe in recipes:
        if recipe["station"] != "fish_smoker":
            continue
        recipe_id = recipe["id"]
        ingredients = [entry for entry in recipe["inputs"] if entry["role"] == "ingredient"]
        fuels = [entry for entry in recipe["inputs"] if entry["role"] == "fuel"]
        if (recipe["machine"] != FISH_SMOKER or len(recipe["inputs"]) != 2
                or len(ingredients) != 1 or len(fuels) != 1 or fuels[0]["item"] != COAL
                or type(fuels[0]["quantity"]) is not int or fuels[0]["quantity"] != 1):
            raise ValueError(f"Fish Smoker requires one drop-in ingredient and one native coal: {recipe_id}")

        ingredient, output = ingredients[0], recipe["output"]
        for entry in (ingredient, output):
            item, quantity = entry["item"], entry["quantity"]
            if not item.startswith("(O)") or len(item) <= 3 or type(quantity) is not int or quantity <= 0:
                raise ValueError(f"Fish Smoker requires an exact qualified object ID and positive quantity: {recipe_id}")
        minutes = recipe["minutes"]
        if type(minutes) is not int or minutes <= 0:
            raise ValueError(f"Fish Smoker requires positive processing minutes: {recipe_id}")

        rules[recipe_id] = {
            "Id": recipe_id,
            "Triggers": [{
                "Id": "ItemPlacedInMachine",
                "Trigger": "ItemPlacedInMachine",
                "RequiredItemId": ingredient["item"],
                "RequiredCount": ingredient["quantity"],
            }],
            "OutputItem": [{
                "Id": output["item"],
                "ItemId": output["item"],
                "MinStack": output["quantity"],
                "MaxStack": output["quantity"],
                "Quality": 0,
            }],
            "MinutesUntilReady": minutes,
        }

    if not rules:
        return None
    # CP appends these namespaced list entries after the native fish rule. The machine's
    # AdditionalConsumedItems already checks and debits a single coal on a successful load.
    return {"Action": "EditData", "Target": "Data/Machines", "TargetField": [FISH_SMOKER, "OutputRules"],
            "Entries": rules}
