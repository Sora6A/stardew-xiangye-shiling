import json
import hashlib
import sys
import tempfile
from pathlib import Path
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import prepare_multiplayer as profiles


class MultiplayerProfileTests(unittest.TestCase):
    def setUp(self):
        self.sandbox = tempfile.TemporaryDirectory()
        self.addCleanup(self.sandbox.cleanup)
        self.root = Path(self.sandbox.name)
        self.source = self.root / "source"
        self.runtime = self.root / "runtime"
        self.runtime.mkdir()
        (self.runtime / "StardewModdingAPI.exe").write_bytes(b"runtime fixture, not an executable")
        self.destination = self.root / ".local/multiplayer"
        manifests = {
            "ContentPatcher": {"UniqueID": "Pathoschild.ContentPatcher", "Version": "2.9.0",
                "MinimumApiVersion": "4.1.10", "EntryDll": "ContentPatcher.dll"},
            "XiangyeShiling": {"UniqueID": "Sora6A.XiangyeShiling", "Version": "0.1.0-alpha.2",
                "MinimumApiVersion": "4.4.0", "EntryDll": "XiangyeShiling.dll", "Dependencies": [
                    {"UniqueID": "Pathoschild.ContentPatcher", "MinimumVersion": "2.9.0", "IsRequired": True}]},
            "XiangyeShiling.Content": {"UniqueID": "Sora6A.XiangyeShiling.Content", "Version": "0.1.0-alpha.2",
                "MinimumApiVersion": "4.4.0", "ContentPackFor": {"UniqueID": "Pathoschild.ContentPatcher",
                    "MinimumVersion": "2.9.0"}, "Dependencies": [{"UniqueID": "Sora6A.XiangyeShiling",
                    "MinimumVersion": "0.1.0-alpha.2", "IsRequired": True}]},
        }
        for name, manifest in manifests.items():
            directory = self.source / name
            directory.mkdir(parents=True)
            (directory / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
        (self.source / "ContentPatcher/ContentPatcher.dll").write_bytes(b"cp-v1")
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"module-v1")
        (self.source / "XiangyeShiling.Content/content.json").write_text("{}", encoding="utf-8")
        (self.source / "ContentPatcher/config.json").write_text('{"SourceOnly": true}', encoding="utf-8")

    def prepare(self, destination=None, harness=None):
        return profiles.prepare_profiles(self.source, self.runtime, destination or self.destination, harness_dir=harness)

    def snapshot(self, directory):
        return {p.relative_to(directory): p.read_bytes() for p in directory.rglob("*") if p.is_file()}

    def harness(self):
        directory = self.root / "probe-package"
        directory.mkdir()
        (directory / "manifest.json").write_text(json.dumps({"UniqueID": "Sora6A.XiangyeShiling.Probe",
            "Version": "0.1.0", "MinimumApiVersion": "4.4.0", "EntryDll": "NetworkProbe.dll",
            "Dependencies": [{"UniqueID": "Sora6A.XiangyeShiling", "MinimumVersion": "0.1.0-alpha.2",
                              "IsRequired": True}]}), encoding="utf-8")
        (directory / "NetworkProbe.dll").write_bytes(b"probe fixture, not a game DLL")
        (directory / "config.json").write_text('{"Enabled": true, "SourceOnly": true}', encoding="utf-8")
        return directory

    def test_client_extra_preflight_does_not_update_host_or_profiles(self):
        """BUG-015: reject all destinations before changing either role or the last profile record."""
        self.prepare()
        before = {p.relative_to(self.destination): p.read_bytes()
                  for p in self.destination.rglob("*") if p.is_file()}
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"module-v2")
        extra = self.destination / "client/Mods/unrelated.dll"
        extra.write_bytes(b"must not delete")
        with self.assertRaises(ValueError):
            self.prepare()
        for relative, original in before.items():
            self.assertEqual(original, (self.destination / relative).read_bytes(),
                             f"BUG-015: preflight rejection already changed {relative}")
        self.assertEqual(b"must not delete", extra.read_bytes())

    def test_existing_runtime_configs_are_preserved_and_source_config_is_not_copied(self):
        """BUG-015: legitimate per-profile runtime configuration is not an unexpected package file."""
        self.prepare()
        for role in ("host", "client"):
            config = self.destination / role / "Mods/ContentPatcher/config.json"
            self.assertFalse(config.exists(), "Source config leaked into a fresh profile.")
            config.write_bytes(b'{\n  "UserRuntime": true\n}\n')
        self.prepare()
        for role in ("host", "client"):
            self.assertEqual(b'{\n  "UserRuntime": true\n}\n',
                             (self.destination / role / "Mods/ContentPatcher/config.json").read_bytes())

    def test_all_source_manifest_failures_preflight_before_either_role_changes(self):
        self.prepare()
        before = self.snapshot(self.destination)
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"module-v2")
        manifest_path = self.source / "XiangyeShiling.Content/manifest.json"
        original = json.loads(manifest_path.read_text(encoding="utf-8"))
        for mutation in (lambda m: m.update(Version="0.1.0-alpha.3"),
                         lambda m: m["Dependencies"][0].update(MinimumVersion="99.0.0"),
                         lambda m: m["Dependencies"][0].update(MinimumVersion="0.1.0-alpha.1"),
                         lambda m: m.update(UniqueID="another.mod")):
            value = json.loads(json.dumps(original))
            mutation(value)
            manifest_path.write_text(json.dumps(value), encoding="utf-8")
            with self.subTest(manifest=value), self.assertRaises(ValueError):
                self.prepare()
            self.assertEqual(before, self.snapshot(self.destination))

    def test_missing_source_binary_preflight_does_not_create_destination(self):
        (self.source / "ContentPatcher/ContentPatcher.dll").unlink()
        with self.assertRaises(ValueError):
            self.prepare()
        self.assertFalse(self.destination.exists())

    def test_source_runtime_and_destination_must_not_overlap_in_either_direction(self):
        before = self.snapshot(self.root)
        for destination in (self.source, self.source / "output", self.root,
                            self.runtime, self.runtime / "profiles"):
            with self.subTest(destination=destination), self.assertRaises(ValueError):
                self.prepare(destination)
            self.assertEqual(before, self.snapshot(self.root))

    def test_cli_output_boundary_accepts_new_local_child_and_rejects_escape_or_local_root(self):
        expected = (self.root / ".local/net02-profiles").resolve()
        self.assertEqual(expected, profiles.validate_output(Path(".local/net02-profiles"), self.root))
        for destination in (self.root, self.root / ".local", self.root / ".local/../source",
                            self.root / ".local-other/profiles", self.root.parent / "outside"):
            with self.subTest(destination=destination), self.assertRaises(ValueError):
                profiles.validate_output(destination, self.root)

    def test_cli_explicit_new_output_preserves_older_profiles_and_api_ignores_global_root(self):
        self.prepare()
        before = self.snapshot(self.destination)
        output = self.root / ".local/net02-profiles"
        with patch.object(profiles, "ROOT", self.root), patch.object(sys, "argv", ["prepare_multiplayer.py",
                "--source-mods", str(self.source), "--runtime", str(self.runtime), "--output", str(output)]):
            self.assertEqual(0, profiles.main())
        self.assertEqual(before, self.snapshot(self.destination))
        self.assertTrue((output / "profiles.json").is_file())
        with patch.object(profiles, "ROOT", self.root / "unrelated"):
            self.prepare(output)

    def test_optional_harness_is_whitelisted_defaults_disabled_and_audit_hashes_actual_roles(self):
        harness = self.harness()
        record = self.prepare(harness=harness)
        self.assertEqual(2, record["schemaVersion"])
        self.assertFalse(record["networkStarted"])
        self.assertFalse(record["runtimeBinaryVersionVerified"])
        self.assertEqual(record, json.loads((self.destination / "profiles.json").read_text(encoding="utf-8")))
        for role in profiles.ROLES:
            mods = self.destination / role / "Mods"
            config = json.loads((mods / "GameHarness/config.json").read_text(encoding="utf-8"))
            self.assertEqual({"Enabled": False, "OpenPreview": False}, config)
            self.assertFalse(record["roles"][role]["harnessEnabled"])
            self.assertTrue(record["roles"][role]["generatedDisabledHarnessConfig"])
            self.assertEqual(record["files"], record["roles"][role]["files"])
            self.assertEqual(record["versions"], record["roles"][role]["versions"])
            for relative, digest in record["roles"][role]["configs"].items():
                self.assertEqual(digest, hashlib.sha256((mods / relative).read_bytes()).hexdigest())
        self.assertTrue(json.loads((harness / "config.json").read_text(encoding="utf-8"))["Enabled"])

    def test_existing_harness_config_is_preserved_and_reported_without_automatic_enabling(self):
        harness = self.harness()
        self.prepare(harness=harness)
        custom = b'{"Enabled": true, "OpenPreview": false, "UserMarker": "keep"}\n'
        (self.destination / "client/Mods/GameHarness/config.json").write_bytes(custom)
        record = self.prepare(harness=harness)
        self.assertEqual(custom, (self.destination / "client/Mods/GameHarness/config.json").read_bytes())
        self.assertTrue(record["roles"]["client"]["harnessEnabled"])
        self.assertFalse(record["roles"]["client"]["generatedDisabledHarnessConfig"])
        self.assertFalse(record["roles"]["host"]["harnessEnabled"])

    def test_harness_extra_and_bad_existing_client_config_preflight_before_host_copy(self):
        harness = self.harness()
        extra = harness / "unapproved.dll"
        extra.write_bytes(b"not declared")
        with self.assertRaises(ValueError):
            self.prepare(harness=harness)
        self.assertFalse(self.destination.exists())
        extra.unlink()
        self.prepare(harness=harness)
        (self.destination / "client/Mods/GameHarness/config.json").write_text('{"Enabled": "yes"}', encoding="utf-8")
        before = self.snapshot(self.destination)
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"module-v2")
        with self.assertRaises(ValueError):
            self.prepare(harness=harness)
        self.assertEqual(before, self.snapshot(self.destination))

    def test_io_failure_does_not_replace_last_successful_profiles_record_or_claim_rollback(self):
        self.prepare()
        audit = (self.destination / "profiles.json").read_bytes()
        (self.source / "XiangyeShiling/XiangyeShiling.dll").write_bytes(b"module-v2")
        original_copy = profiles.shutil.copy2
        def fail_client(source, target):
            if target.relative_to(self.destination).parts[0] == "client":
                raise OSError("injected client copy failure")
            return original_copy(source, target)
        with patch.object(profiles.shutil, "copy2", side_effect=fail_client), self.assertRaises(OSError):
            self.prepare()
        self.assertEqual(audit, (self.destination / "profiles.json").read_bytes())
        self.assertEqual(b"module-v2", (self.destination / "host/Mods/XiangyeShiling/XiangyeShiling.dll").read_bytes())
        self.assertEqual(b"module-v1", (self.destination / "client/Mods/XiangyeShiling/XiangyeShiling.dll").read_bytes())


if __name__ == "__main__":
    unittest.main()
