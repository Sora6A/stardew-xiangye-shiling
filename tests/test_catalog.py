import copy
import sys
from pathlib import Path
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import catalog


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.data = copy.deepcopy(catalog.read_json(catalog.ROOT / "data/catalog.json"))

    def test_frozen_catalog_and_generated_files(self):
        self.assertEqual([], catalog.check_repository())

    def test_content_pack_requires_current_machine_module(self):
        """BUG-2026-005: alpha.1 cannot safely operate alpha.2 machine objects."""
        original_read = catalog.read_json
        def old_module_dependency(path):
            data = original_read(path)
            if Path(path).resolve() == (catalog.ROOT / catalog.PACK / "manifest.json").resolve():
                data["Dependencies"][0]["MinimumVersion"] = "0.1.0-alpha.1"
            return data
        with patch.object(catalog, "read_json", side_effect=old_module_dependency):
            self.assertIn("Content pack must require the matching machine module version", catalog.check_repository())

    def test_shop_entries_preserve_catalog_prices(self):
        """BUG-2026-006: Pierre's general price multiplier must not double explicit prototype prices."""
        generated = catalog.json.loads(catalog.generated_files(self.data)[catalog.PACK / "content.json"])
        for patch_data in generated["Changes"]:
            if patch_data["Target"] == "Data/Shops":
                for row in patch_data["Entries"].values():
                    self.assertTrue(row.get("IgnoreShopPriceModifiers"), row["Id"])

    def test_smapi_baseline_cannot_regress_in_either_manifest(self):
        """BUG-2026-001: checking our DLL alone doesn't validate CP's API requirements."""
        original_read = catalog.read_json
        for relative in ("src/XiangyeShiling/manifest.json", "content/XiangyeShiling.Content/manifest.json"):
            with self.subTest(manifest=relative):
                def read_with_old_baseline(path):
                    data = original_read(path)
                    if Path(path).resolve() == (catalog.ROOT / relative).resolve():
                        data["MinimumApiVersion"] = "4.1.10"
                    return data

                # Patch reads only, leaving both tracked and untracked files untouched.
                with patch.object(catalog, "read_json", side_effect=read_with_old_baseline):
                    self.assertIn("CP 2.9 requires the SMAPI 4.4.0 baseline", catalog.check_repository())

    def test_duplicate_json_keys_fail_instead_of_silently_overwriting(self):
        with self.assertRaises(ValueError):
            catalog.json.loads('{"id": 1, "id": 2}', object_pairs_hook=catalog.unique_object)

    def test_missing_ingredient_is_rejected(self):
        self.data["recipes"][0]["inputs"][0]["item"] = "(O)missing"
        self.assertTrue(any("Unknown input" in e for e in catalog.validate(self.data)))

    def test_expanding_scope_is_rejected(self):
        self.data["items"].append(copy.deepcopy(self.data["items"][0]))
        errors = catalog.validate(self.data)
        self.assertIn("Frozen item counts changed", errors)
        self.assertIn("Duplicate item ID", errors)

    def test_recipe_output_must_match_source(self):
        self.data["recipes"][0]["output"]["item"] = "(O)" + catalog.NAMESPACE + "_Tofu"
        self.assertTrue(any("Broken producer" in e for e in catalog.validate(self.data)))

    def test_incomplete_balance_cannot_be_activated(self):
        self.data["recipes"][0]["implementation"] = "active"
        errors = catalog.validate(self.data)
        self.assertTrue(any("unset quantity" in e for e in errors))
        self.assertTrue(any("no duration" in e for e in errors))

    def test_zero_input_quantity_is_rejected(self):
        self.data["recipes"][0]["inputs"][0]["quantity"] = 0
        self.assertTrue(any("Invalid quantity" in e for e in catalog.validate(self.data)))

    def test_unobtainable_recipe_cycle_is_rejected(self):
        recipe = next(r for r in self.data["recipes"] if r["id"].endswith("_SoySauce"))
        recipe["inputs"] = [{"item": recipe["output"]["item"], "quantity": 1, "role": "ingredient"}]
        self.assertTrue(any("Unreachable items" in e for e in catalog.validate(self.data)))

    def test_recipe_shop_cannot_drift(self):
        recipe = next(r for r in self.data["recipes"] if r["station"] == "kitchen")
        recipe["unlock"]["shop"] = "TravelingMerchant"
        self.assertTrue(any("Wrong recipe unlock" in e for e in catalog.validate(self.data)))

    def test_prototype_blueprint_name_matches_learned_recipe_key(self):
        item = next(i for i in self.data["items"] if i["key"] == "BeanWorkshop")
        item["internalName"] = item["id"]
        self.assertTrue(any("Recipe purchase name mismatch" in e for e in catalog.validate(self.data)))

    def test_prototype_supplies_require_positive_limited_stock(self):
        self.data["prototypeShops"]["SeedShop"][0]["AvailableStock"] = -1
        self.assertIn("Prototype ingredients need limited stock", catalog.validate(self.data))

    def test_prototype_scope_does_not_activate_p2_content(self):
        item = next(i for i in self.data["items"] if i["key"] == "SoyMilk")
        item["implementation"] = "prototype"
        self.assertIn("P1 prototype item scope changed", catalog.validate(self.data))

    def test_generated_machine_recipe_matches_catalog_quantities(self):
        files = catalog.generated_files(self.data)
        runtime = catalog.json.loads(files[Path("src/XiangyeShiling/assets/machine-recipes.json")])
        recipe = next(r for r in self.data["recipes"] if r["id"].endswith("_SoySauce"))
        generated = next(r for r in runtime if r["Id"] == recipe["id"])
        self.assertEqual([{"ItemId": i["item"], "Quantity": i["quantity"]} for i in recipe["inputs"]], generated["Inputs"])
        self.assertEqual(recipe["minutes"], generated["Minutes"])
        self.assertEqual(recipe["output"]["quantity"], generated["Output"]["Quantity"])


if __name__ == "__main__":
    unittest.main()
