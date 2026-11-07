"""人工构造身份/包/配置的离线回归；不接触真实游戏、个人存档或现有 profiles。"""

import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import configure_network_fixture as fixture
import prepare_multiplayer as packages


def sha(data):
    return hashlib.sha256(data).hexdigest()


class NetworkFixtureTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.root = Path(self.sandbox.name)
        self.farm = "XYSNETFresh"
        self.save_id = 900001
        # 玩家/目录名不等于农场名，目录末尾也不是本 fixture 的世界 ID。
        self.save = self.root / "ArtificialPlayer_42"
        self.save.mkdir()
        self.write_save()
        self.source = self.root / "synthetic-packages"
        self.runtime = self.root / "synthetic-runtime"
        self.runtime.mkdir()
        (self.runtime / "StardewModdingAPI.exe").write_bytes(b"Artificial fixture, not an executable")
        manifests = {
            "ContentPatcher": {"UniqueID": "Pathoschild.ContentPatcher", "Version": "2.9.0",
                               "MinimumApiVersion": "4.4.0", "EntryDll": "ContentPatcher.dll"},
            "XiangyeShiling": {"UniqueID": "Sora6A.XiangyeShiling", "Version": "0.1.0-alpha.2",
                               "MinimumApiVersion": "4.4.0", "EntryDll": "XiangyeShiling.dll",
                               "Dependencies": [{"UniqueID": "Pathoschild.ContentPatcher",
                                                 "MinimumVersion": "2.9.0", "IsRequired": True}]},
            "XiangyeShiling.Content": {"UniqueID": "Sora6A.XiangyeShiling.Content", "Version": "0.1.0-alpha.2",
                                       "MinimumApiVersion": "4.4.0",
                                       "ContentPackFor": {"UniqueID": "Pathoschild.ContentPatcher", "MinimumVersion": "2.9.0"},
                                       "Dependencies": [{"UniqueID": "Sora6A.XiangyeShiling",
                                                         "MinimumVersion": "0.1.0-alpha.2", "IsRequired": True}]},
        }
        for name, manifest in manifests.items():
            directory = self.source / name
            directory.mkdir(parents=True)
            self.write_json(directory / "manifest.json", manifest)
        (self.source / "ContentPatcher/ContentPatcher.dll").write_bytes(b"Artificial ContentPatcher package")
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"Artificial production package")
        (self.source / "XiangyeShiling.Content/content.json").write_bytes(b"{}\n")
        harness = self.root / "synthetic-harness"
        harness.mkdir()
        self.write_json(harness / "manifest.json", {
            "UniqueID": "Sora6A.XiangyeShiling.GameHarness", "Version": "0.1.0",
            "MinimumApiVersion": "4.4.0", "EntryDll": "GameHarness.dll",
            "Dependencies": [{"UniqueID": "Sora6A.XiangyeShiling",
                              "MinimumVersion": "0.1.0-alpha.2", "IsRequired": True}],
        })
        (harness / "GameHarness.dll").write_bytes(b"Artificial harness package, not a game DLL")
        self.profiles = self.root / ".local/profiles"
        packages.prepare_profiles(self.source, self.runtime, self.profiles, harness_dir=harness)
        for role in packages.ROLES:
            self.write_json(self.config_path(role, "XiangyeShiling"), {
                "EnableReadOnlyNetworkProbes": False, "NetworkProbeFarmName": "XYSNET02",
                "NetworkProbeSaveFixtureId": 0, "UserOption": {"Preserve": [role, 3]},
            })
            self.write_json(self.config_path(role, "GameHarness"), {
                "Enabled": False, "OpenPreview": False, "Scenario": "Network", "NetworkRole": role.title(),
                "FarmName": "XYSNET02", "SaveFixtureId": 0, "UserOption": {"Preserve": [role, 7]},
            })
        self.output = self.root / ".local/fixture-evidence"

    def write_json(self, path, value):
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def write_save(self, farm=None, identity=None, encoding="utf-8"):
        farm = self.farm if farm is None else farm
        identity = self.save_id if identity is None else identity
        self.main_xml = ("<SaveGame><player><name>ArtificialPlayer</name><farmName>" + escape(farm) +
                         "</farmName></player><uniqueIDForThisGame>" + str(identity) +
                         "</uniqueIDForThisGame><locations><farmName>Unrelated nested name</farmName>"
                         "<uniqueIDForThisGame>123</uniqueIDForThisGame></locations></SaveGame>")
        self.info_xml = "<Farmer><name>ArtificialPlayer</name><farmName>" + escape(farm) + "</farmName></Farmer>"
        (self.save / self.save.name).write_bytes(self.main_xml.encode(encoding))
        (self.save / "SaveGameInfo").write_bytes(self.info_xml.encode(encoding))

    def config_path(self, role, name):
        return self.profiles / role / "Mods" / name / "config.json"

    def snapshot(self, root=None):
        root = self.root if root is None else root
        return {path.relative_to(root).as_posix(): path.read_bytes()
                for path in root.rglob("*") if path.is_file()}

    def configure(self, **kwargs):
        return fixture.configure_fixture(self.save, self.profiles, expected_farm_name=self.farm, **kwargs)

    def rejected_before_writes(self, *, output_absent=True):
        before = self.snapshot()
        with self.assertRaises((OSError, ValueError)):
            self.configure(output=self.output, apply=True)
        self.assertEqual(before, self.snapshot())
        if output_absent:
            self.assertFalse(self.output.exists())

    def revise_record(self, change):
        path = self.profiles / "profiles.json"
        record = json.loads(path.read_text(encoding="utf-8"))
        change(record)
        self.write_json(path, record)

    def rewrite_manifest_and_update_claimed_hashes(self, name, change):
        relative = name + "/manifest.json"
        manifest = json.loads((self.profiles / "host/Mods" / relative).read_text(encoding="utf-8"))
        change(manifest)
        for role in packages.ROLES:
            self.write_json(self.profiles / role / "Mods" / relative, manifest)
        digest = sha((self.profiles / "host/Mods" / relative).read_bytes())
        def update(record):
            record["files"][relative] = digest
            for role in packages.ROLES:
                record["roles"][role]["files"][relative] = digest
        self.revise_record(update)

    def test_default_plan_is_completely_readonly_and_does_not_claim_runtime_or_network_acceptance(self):
        before = self.snapshot()
        plan = self.configure()
        self.assertEqual(before, self.snapshot())
        self.assertFalse(self.output.exists())
        self.assertEqual((1, self.farm, self.save_id), (plan["schemaVersion"], plan["farmName"], plan["saveFixtureId"]))
        for field in ("configurationApplied", "networkStarted", "fullNet02Accepted",
                      "runtimeBinaryVersionVerified", "cabinAvailabilityVerified"):
            self.assertIs(plan[field], False)
        self.assertEqual({self.save.name: sha((self.save / self.save.name).read_bytes()),
                          "SaveGameInfo": sha((self.save / "SaveGameInfo").read_bytes())}, plan["saveHashes"])
        self.assertEqual(sha((self.profiles / "profiles.json").read_bytes()), plan["profilesSha256"])
        self.assertEqual(4, len(plan["configs"]))
        for relative, entry in plan["configs"].items():
            self.assertEqual(sha((self.profiles / relative).read_bytes()), entry["beforeSha256"])
            self.assertEqual(64, len(entry["afterSha256"]))
            value = entry["configuration"]
            old = json.loads((self.profiles / relative).read_text(encoding="utf-8"))
            self.assertEqual(old["UserOption"], value["UserOption"])
            if "/GameHarness/" in relative:
                self.assertEqual((self.farm, self.save_id, "Network"),
                                 (value["FarmName"], value["SaveFixtureId"], value["Scenario"]))
                self.assertIs(value["Enabled"], False)
                self.assertIs(value["OpenPreview"], False)
            else:
                self.assertEqual((self.farm, self.save_id),
                                 (value["NetworkProbeFarmName"], value["NetworkProbeSaveFixtureId"]))
                self.assertIs(value["EnableReadOnlyNetworkProbes"], False)

    def test_exported_plan_has_exact_backups_candidates_and_no_input_changes(self):
        before = self.snapshot()
        plan = self.configure(output=self.output)
        self.assertIs(plan["configurationApplied"], False)
        self.assertEqual(plan, json.loads((self.output / "fixture-plan.json").read_text(encoding="utf-8")))
        self.assertFalse((self.output / "fixture-apply.json").exists())
        for relative, entry in plan["configs"].items():
            self.assertEqual((self.profiles / relative).read_bytes(), (self.output / "before" / relative).read_bytes())
            candidate = (self.output / "prepared" / relative).read_bytes()
            self.assertEqual(entry["afterSha256"], sha(candidate))
            self.assertEqual(entry["configuration"], json.loads(candidate))
        for relative, data in before.items():
            self.assertEqual(data, (self.root / relative).read_bytes())

    def test_apply_changes_only_four_configs_and_keeps_old_profiles_and_save_bytes(self):
        before = self.snapshot()
        audit = self.configure(output=self.output, apply=True)
        self.assertIs(audit["configurationApplied"], True)
        self.assertIs(audit["networkStarted"], False)
        self.assertIs(audit["fullNet02Accepted"], False)
        self.assertIsNone(audit["failure"])
        self.assertEqual(set(audit["configs"]), set(audit["replacedConfigs"]))
        self.assertEqual(audit, json.loads((self.output / "fixture-apply.json").read_text(encoding="utf-8")))
        changed = {relative for relative, data in before.items() if data != (self.root / relative).read_bytes()}
        expected = {".local/profiles/" + relative for relative in audit["configs"]}
        self.assertEqual(expected, changed)
        for relative, entry in audit["configs"].items():
            current = (self.profiles / relative).read_bytes()
            self.assertEqual(entry["afterSha256"], sha(current))
            self.assertEqual(entry["configuration"], json.loads(current))
        inspection = fixture.inspect_profiles(self.profiles)
        for relative, entry in inspection["configs"].items():
            self.assertEqual(audit["configs"][relative]["afterSha256"], entry["beforeSha256"])

    def test_apply_requires_output_and_existing_evidence_is_not_overwritten(self):
        before = self.snapshot()
        with self.assertRaises(ValueError):
            self.configure(apply=True)
        self.assertEqual(before, self.snapshot())
        self.output.mkdir()
        (self.output / "retain.txt").write_bytes(b"Artificial evidence retained")
        self.rejected_before_writes(output_absent=False)

    def test_apply_parameter_cannot_treat_integer_or_string_as_explicit_authorization(self):
        for value in (1, 0, "true", None):
            with self.subTest(apply=value):
                before = self.snapshot()
                with self.assertRaises(ValueError):
                    self.configure(output=self.output, apply=value)
                self.assertEqual(before, self.snapshot())

    def test_inspection_rehashes_configs_and_does_not_trust_stale_hashes_or_absolute_role_paths(self):
        trap = self.root / "wrong-profile-target"
        trap.mkdir()
        def stale(record):
            for role in packages.ROLES:
                record["roles"][role]["mods"] = str(trap)
                record["roles"][role]["configs"] = {"GameHarness/config.json": "0" * 64}
        self.revise_record(stale)
        before = self.snapshot()
        inspected = fixture.inspect_profiles(self.profiles)
        self.assertEqual(before, self.snapshot())
        self.assertEqual(sha((self.profiles / "profiles.json").read_bytes()), inspected["profilesSha256"])
        for relative, entry in inspected["configs"].items():
            data = (self.profiles / relative).read_bytes()
            self.assertEqual(sha(data), entry["beforeSha256"])
            self.assertEqual(json.loads(data), entry["configuration"])
        self.configure(output=self.output, apply=True)
        self.assertEqual([], list(trap.iterdir()))
        self.assertEqual(before[".local/profiles/profiles.json"], (self.profiles / "profiles.json").read_bytes())

    def test_damaged_or_mismatched_xml_is_rejected_before_any_configuration_changes(self):
        main = self.save / self.save.name
        info = self.save / "SaveGameInfo"
        for target, text in ((main, "<SaveGame>"), (main, self.main_xml.replace("<SaveGame>", "<Other>").replace("</SaveGame>", "</Other>")),
                             (info, self.info_xml.replace("Farmer", "SaveGame")),
                             (info, self.info_xml.replace(self.farm, "XYSNETWrong"))):
            with self.subTest(target=target.name, text=text[:30]):
                self.write_save()
                target.write_text(text, encoding="utf-8")
                self.rejected_before_writes()

    def test_missing_duplicate_or_nested_identity_does_not_use_descendant_matches(self):
        main = self.save / self.save.name
        original = self.main_xml
        identity = "<uniqueIDForThisGame>900001</uniqueIDForThisGame>"
        name = "<farmName>" + self.farm + "</farmName>"
        cases = (original.replace(identity, "", 1), original.replace(identity, identity * 2, 1),
                 original.replace(identity, "<wrapper>" + identity + "</wrapper>", 1),
                 original.replace(name, "", 1), original.replace(name, name * 2, 1),
                 original.replace(name, "<wrapper>" + name + "</wrapper>", 1),
                 original.replace(name, "<farmName><value>" + self.farm + "</value></farmName>", 1))
        for text in cases:
            with self.subTest(text=text[:70]):
                self.write_save()
                main.write_text(text, encoding="utf-8")
                self.rejected_before_writes()

    def test_dtd_and_entity_declarations_are_rejected_even_with_utf16_encoding(self):
        for encoding in ("utf-8", "utf-16"):
            for declaration in ('<!DOCTYPE SaveGame [<!ENTITY farm "' + self.farm + '">]>',
                                '<!DOCTYPE SaveGame SYSTEM "file:///never-read-fixture.dtd">',
                                '<!DOCTYPE SaveGame []>'):
                with self.subTest(encoding=encoding, declaration=declaration[:45]):
                    self.write_save()
                    text = declaration + self.main_xml
                    (self.save / self.save.name).write_bytes(text.encode(encoding))
                    self.rejected_before_writes()

    def test_plain_utf16_save_is_valid_and_utf16_name_length_matches_game_rules(self):
        self.write_save(encoding="utf-16")
        self.assertEqual(self.save_id, self.configure()["saveFixtureId"])
        self.farm = "XYSNET" + "🌾" * 7  # 20个UTF-16单位，不是Python的13个code point。
        self.write_save()
        self.assertEqual(self.farm, self.configure()["farmName"])
        self.farm = "XYSNET" + "🌾" * 8
        self.write_save()
        self.rejected_before_writes()

    def test_ordinary_old_farm_names_and_protected_or_invalid_world_ids_are_rejected(self):
        for farm in ("Home", "XYSP1UI", "XYSP1Save", "xysnetFarm", "XYSNET" + "x" * 15):
            with self.subTest(farm=farm):
                self.farm = farm
                self.write_save()
                self.rejected_before_writes()
        self.farm = "XYSNETFresh"
        for identity in (0, -1, 2**64, "true", "900001.0", 449937634, 8624092401, 8624092801, 8624092802):
            with self.subTest(identity=identity):
                self.write_save(identity=identity)
                self.rejected_before_writes()
        self.write_save(identity=2**64 - 1)
        self.assertEqual(2**64 - 1, self.configure()["saveFixtureId"])

    def test_expected_name_mismatch_does_not_bind_a_different_named_save(self):
        before = self.snapshot()
        with self.assertRaises(ValueError):
            fixture.configure_fixture(self.save, self.profiles, expected_farm_name="XYSNETWrong", output=self.output, apply=True)
        self.assertEqual(before, self.snapshot())

    def test_xml_reads_are_bounded_and_oversized_input_is_not_parsed_or_written(self):
        main = self.save / self.save.name
        # 稀疏临时文件触发生产64MiB上限；无需提交大附件或伪造真实存档。
        with main.open("wb") as stream:
            stream.truncate(64 * 1024 * 1024 + 1)
        before_configs = self.snapshot(self.profiles)
        with self.assertRaises(ValueError):
            self.configure(output=self.output, apply=True)
        self.assertEqual(before_configs, self.snapshot(self.profiles))
        self.assertEqual(64 * 1024 * 1024 + 1, main.stat().st_size)
        self.assertFalse(self.output.exists())

    def test_invalid_client_config_is_rejected_before_host_changes_and_not_silently_disabled(self):
        config = self.config_path("client", "GameHarness")
        original = json.loads(config.read_text(encoding="utf-8"))
        changes = (("Enabled", True), ("OpenPreview", True), ("Scenario", "Memory"), ("NetworkRole", "Host"),
                   ("Enabled", 0), ("SaveFixtureId", True), ("FarmName", 1))
        for field, value in changes:
            with self.subTest(field=field, value=value):
                self.write_json(config, {**original, field: value})
                self.rejected_before_writes()
        for field in ("Enabled", "Scenario", "NetworkRole", "FarmName", "SaveFixtureId", "OpenPreview"):
            with self.subTest(missing=field):
                value = dict(original)
                del value[field]
                self.write_json(config, value)
                self.rejected_before_writes()

    def test_main_switch_is_explicitly_disabled_and_known_fields_are_typed(self):
        config = self.config_path("host", "XiangyeShiling")
        original = json.loads(config.read_text(encoding="utf-8"))
        for field, value in (("EnableReadOnlyNetworkProbes", True), ("EnableReadOnlyNetworkProbes", 0),
                             ("NetworkProbeSaveFixtureId", True), ("NetworkProbeSaveFixtureId", -1),
                             ("NetworkProbeSaveFixtureId", 2**64), ("NetworkProbeFarmName", None)):
            with self.subTest(field=field, value=value):
                self.write_json(config, {**original, field: value})
                self.rejected_before_writes()
        del original["EnableReadOnlyNetworkProbes"]
        self.write_json(config, original)
        self.rejected_before_writes()

    def test_duplicate_case_aliases_and_nonfinite_config_json_are_rejected(self):
        config = self.config_path("client", "XiangyeShiling")
        original = config.read_text(encoding="utf-8")
        switch = '"EnableReadOnlyNetworkProbes": false'
        for replacement in (switch + ', "EnableReadOnlyNetworkProbes": true',
                            switch + ', "enablereadonlynetworkprobes": true',
                            switch + ', "AdditionalSyntheticNumber": NaN',
                            switch + ', "AdditionalSyntheticNumber": 1e999'):
            with self.subTest(replacement=replacement):
                config.write_text(original.replace(switch, replacement), encoding="utf-8")
                self.rejected_before_writes()

    def test_schema_or_role_declarations_are_rechecked_instead_of_trusting_old_audit(self):
        record_path = self.profiles / "profiles.json"
        original = record_path.read_bytes()
        for change in (lambda r: r.update(schemaVersion=1), lambda r: r.update(schemaVersion=True),
                       lambda r: r["roles"]["client"]["files"].update({"GameHarness/GameHarness.dll": "0" * 64}),
                       lambda r: r["roles"]["client"]["versions"].update({"Sora6A.XiangyeShiling.GameHarness": "9.0.0"})):
            with self.subTest(change=change):
                record_path.write_bytes(original)
                self.revise_record(change)
                self.rejected_before_writes()

    def test_changed_missing_or_unlisted_client_package_fails_full_preflight(self):
        target = self.profiles / "client/Mods/GameHarness/GameHarness.dll"
        original = target.read_bytes()
        target.write_bytes(b"Artificial different build")
        self.rejected_before_writes()
        target.unlink()
        self.rejected_before_writes()
        target.write_bytes(original)
        (target.parent / "Unlisted.dll").write_bytes(b"Artificial unrelated package")
        self.rejected_before_writes()

    def test_record_cannot_omit_required_payloads_even_when_declared_tree_matches(self):
        before = self.snapshot(self.profiles)
        for relative in ("ContentPatcher/ContentPatcher.dll", "XiangyeShiling.Content/content.json"):
            with self.subTest(relative=relative):
                for path, data in before.items():
                    (self.profiles / path).write_bytes(data)
                for role in packages.ROLES:
                    (self.profiles / role / "Mods" / relative).unlink()
                def remove(record):
                    del record["files"][relative]
                    for role in packages.ROLES:
                        del record["roles"][role]["files"][relative]
                self.revise_record(remove)
                self.rejected_before_writes()

    def test_declared_extra_harness_binary_is_rejected_despite_consistent_hashes(self):
        relative = "GameHarness/Other.dll"
        data = b"Artificial additional harness binary"
        for role in packages.ROLES:
            (self.profiles / role / "Mods" / relative).write_bytes(data)
        def add(record):
            record["files"][relative] = sha(data)
            for role in packages.ROLES:
                record["roles"][role]["files"][relative] = sha(data)
        self.revise_record(add)
        self.rejected_before_writes()

    def test_manifest_semantics_are_validated_even_when_all_recorded_hashes_agree(self):
        profile_before = self.snapshot(self.profiles)
        for name, change in (("GameHarness", lambda m: m.update(UniqueID="Another.Harness")),
                             ("GameHarness", lambda m: m.update(EntryDll="Other.dll")),
                             ("GameHarness", lambda m: m.update(MinimumApiVersion="4.1.10")),
                             ("GameHarness", lambda m: m.update(Dependencies=[])),
                             ("XiangyeShiling", lambda m: m.update(MinimumApiVersion="4.1.10"))):
            with self.subTest(name=name, change=change):
                for relative, data in profile_before.items():
                    (self.profiles / relative).write_bytes(data)
                self.rewrite_manifest_and_update_claimed_hashes(name, change)
                self.rejected_before_writes()

    def test_declared_package_paths_cannot_escape_alias_or_disguise_configuration(self):
        record_path = self.profiles / "profiles.json"
        original = record_path.read_bytes()
        for relative in ("../outside.dll", "/outside.dll", "C:/outside.dll", "GameHarness\\GameHarness.dll",
                         "GameHarness/../GameHarness.dll", "GameHarness/config.json", "GameHarness/GAMEHARNESS.dll"):
            with self.subTest(relative=relative):
                record_path.write_bytes(original)
                def add(record):
                    record["files"][relative] = "a" * 64
                    for role in packages.ROLES:
                        record["roles"][role]["files"][relative] = "a" * 64
                self.revise_record(add)
                self.rejected_before_writes()

    @unittest.skipUnless(os.name == "nt", "Case-insensitive Windows file ownership regression")
    def test_case_aliased_config_cannot_be_disguised_as_package_file(self):
        """BUG-018: Windows上的Config.json仍是可写配置，不能通过大小写改成登记包文件。"""
        data = self.config_path("host", "XiangyeShiling").read_bytes()
        for role in packages.ROLES:
            path = self.config_path(role, "XiangyeShiling")
            path.write_bytes(data)
            intermediate = path.with_name("case-alias-fixture.tmp")
            path.rename(intermediate)
            intermediate.rename(path.with_name("Config.json"))
        # 修后补充正常路径：大小写文件名属于配置，不能用一概拒绝大写文件掩盖分类错误。
        # 原修前红仅覆盖下方把配置登记为包文件的拒绝断言，不覆盖这些新增正常路径断言。
        before_normal_plan = self.snapshot()
        inspected = fixture.inspect_profiles(self.profiles)
        plan = self.configure()
        self.assertEqual(4, len(inspected["configs"]))
        self.assertEqual(set(inspected["configs"]), set(plan["configs"]))
        for config_relative, entry in inspected["configs"].items():
            original = (self.profiles / config_relative).read_bytes()
            self.assertEqual(sha(original), entry["beforeSha256"])
            self.assertEqual(sha(original), plan["configs"][config_relative]["beforeSha256"])
            self.assertEqual(json.loads(original), entry["configuration"])
        self.assertEqual(before_normal_plan, self.snapshot())
        self.assertIs(plan["configurationApplied"], False)
        relative = "XiangyeShiling/Config.json"
        def add(record):
            record["files"][relative] = sha(data)
            for role in packages.ROLES:
                record["roles"][role]["files"][relative] = sha(data)
        self.revise_record(add)
        before = self.snapshot()
        with self.assertRaises(ValueError):
            self.configure()
        self.assertEqual(before, self.snapshot())

    def test_existing_output_or_output_overlap_with_inputs_or_runtime_is_rejected(self):
        for output in (self.profiles, self.profiles / "evidence", self.save / "evidence",
                       self.runtime / "evidence", self.root / ".local"):
            with self.subTest(output=output):
                before = self.snapshot()
                with self.assertRaises(ValueError):
                    self.configure(output=output, apply=True)
                self.assertEqual(before, self.snapshot())

    def test_hard_linked_config_is_rejected_and_external_alias_is_preserved(self):
        target = self.config_path("client", "GameHarness")
        alias = self.root / "linked-config.json"
        os.link(target, alias)
        self.rejected_before_writes()
        self.assertEqual(target.read_bytes(), alias.read_bytes())

    def test_second_replace_failure_is_reported_as_partial_application_with_exact_backups(self):
        originals = {role + "/Mods/" + name + "/config.json": self.config_path(role, name).read_bytes()
                     for role in packages.ROLES for name in ("XiangyeShiling", "GameHarness")}
        targets = {str((self.profiles / relative).resolve()) for relative in originals}
        real_replace = os.replace
        attempted = []
        def fail_second(source, target):
            if str(Path(target).resolve()) in targets:
                attempted.append(str(Path(target).resolve()))
                if len(attempted) == 2:
                    raise PermissionError("Artificial second-config replace failure")
            return real_replace(source, target)
        save_before = self.snapshot(self.save)
        profile_record = (self.profiles / "profiles.json").read_bytes()
        with patch("os.replace", side_effect=fail_second), self.assertRaises(OSError):
            self.configure(output=self.output, apply=True)
        audit = json.loads((self.output / "fixture-apply.json").read_text(encoding="utf-8"))
        self.assertIs(audit["configurationApplied"], False)
        self.assertEqual(1, len(audit["replacedConfigs"]))
        self.assertIsInstance(audit["failure"], str)
        changed = [relative for relative, data in originals.items() if (self.profiles / relative).read_bytes() != data]
        self.assertEqual(audit["replacedConfigs"], changed)
        for relative, data in originals.items():
            self.assertEqual(data, (self.output / "before" / relative).read_bytes())
            if relative not in changed:
                self.assertEqual(data, (self.profiles / relative).read_bytes())
        self.assertEqual(save_before, self.snapshot(self.save))
        self.assertEqual(profile_record, (self.profiles / "profiles.json").read_bytes())
        self.assertEqual([], list(self.profiles.rglob(".net02-config-*")))

    def invoke_cli(self, *extra):
        stdout, stderr = io.StringIO(), io.StringIO()
        args = ["configure_network_fixture.py", "--save", str(self.save), "--profiles", str(self.profiles),
                "--farm-name", self.farm, *extra]
        with patch.object(fixture, "ROOT", self.root), patch.object(sys, "argv", args), \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            try:
                code = fixture.main()
            except (ValueError, OSError):
                code = 1
            except SystemExit as error:
                code = error.code
        return code, stdout.getvalue(), stderr.getvalue()

    def test_cli_default_plan_is_readonly_and_explicit_apply_keeps_switches_off(self):
        before = self.snapshot()
        code, stdout, stderr = self.invoke_cli()
        self.assertEqual(0, code, stdout + stderr)
        self.assertEqual(before, self.snapshot())
        self.assertIs(json.loads(stdout)["configurationApplied"], False)
        code, stdout, stderr = self.invoke_cli("--output", str(self.output), "--apply")
        self.assertEqual(0, code, stdout + stderr)
        self.assertIs(json.loads(stdout)["configurationApplied"], True)
        self.assertIs(json.loads(stdout)["networkStarted"], False)
        inspected = fixture.inspect_profiles(self.profiles)
        for relative, entry in inspected["configs"].items():
            switch = "Enabled" if "/GameHarness/" in relative else "EnableReadOnlyNetworkProbes"
            self.assertIs(entry["configuration"][switch], False)

    def test_cli_profiles_and_output_must_stay_within_strict_workspace_local_children(self):
        for option, target in (("--output", self.root / ".local"), ("--output", self.root / "outside-output"),
                               ("--output", self.root / ".local/../escaped-output"),
                               ("--profiles", self.root / ".local"), ("--profiles", self.root / "outside-profiles")):
            with self.subTest(option=option, target=target):
                before = self.snapshot()
                code, _, _ = self.invoke_cli(option, str(target), "--apply")
                self.assertNotEqual(0, code)
                self.assertEqual(before, self.snapshot())


if __name__ == "__main__":
    unittest.main()
