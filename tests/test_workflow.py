"""Regression checks for the maintenance gate; no game files or repository writes required."""
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import check_workflow as workflow


def complete_message(subject="docs(workflow): document the maintenance contract"):
    parts = [subject]
    for heading in workflow.SECTIONS:
        parts += [f"## {heading}", "Concrete explanation with scope and evidence."]
    parts += ["Handoff: docs/HANDOFF.md"]
    return "\n\n".join(parts)


class WorkflowTests(unittest.TestCase):
    def test_complete_commit_is_accepted(self):
        self.assertEqual([], workflow.validate_message(complete_message()))

    def test_one_line_or_duplicate_sections_are_rejected(self):
        self.assertTrue(workflow.validate_message("docs(workflow): only a summary"))
        errors = workflow.validate_message(complete_message() + "\n\n## 验证结果\nRepeated claim")
        self.assertTrue(any("duplicate section" in error for error in errors))

    def test_unfilled_template_is_rejected(self):
        template = (workflow.ROOT / ".github/COMMIT_TEMPLATE.txt").read_text(encoding="utf-8")
        self.assertTrue(workflow.validate_message(template))

    def test_fix_requires_an_existing_bug_record(self):
        message = complete_message("fix(deps): correct incompatible dependency")
        self.assertTrue(any("Fix commit" in error for error in workflow.validate_message(message)))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            relative = "docs/bugfix/BUG-2026-001-sample.md"
            message += f"\nBugfix: {relative}"
            self.assertTrue(any("does not exist" in error for error in workflow.validate_message(message, root)))
            record = root / relative
            record.parent.mkdir(parents=True)
            record.write_text("A fixture", encoding="utf-8")
            self.assertEqual([], workflow.validate_message(message, root))

    def test_fix_commit_must_change_the_linked_record(self):
        message = complete_message("fix(deps): correct incompatible dependency")
        message += "\nBugfix: docs/bugfix/BUG-2026-001-smapi-baseline.md"

        def git_response(*args, **kwargs):
            return {"rev-parse": "a" * 40, "rev-list": "b" * 40,
                    "show": message, "diff-tree": "src/XiangyeShiling/manifest.json"}[args[0]]

        with patch.object(workflow, "git", side_effect=git_response):
            errors, count = workflow.check_commits("origin/main..HEAD")
        self.assertEqual(1, count)
        self.assertTrue(any("must add/update" in error for error in errors))

    def test_option_like_revision_range_is_rejected(self):
        with self.assertRaises(ValueError):
            workflow.check_commits("--all..HEAD")

    def test_real_git_range_checks_messages_and_changed_bug_record(self):
        """Exercise Git's actual range/diff output in an isolated, never-pushed repository."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflow.git("init", root=root)
            (root / "baseline.txt").write_text("fixture", encoding="utf-8")
            workflow.git("add", "--", "baseline.txt", root=root)

            def commit(message):
                workflow.git("-c", "user.name=Workflow Test", "-c", "user.email=validation@example.invalid",
                             "-c", "commit.gpgSign=false", "-c", f"core.hooksPath={root / 'empty-hooks'}",
                             "commit", "-m", message, root=root)

            commit("Fixture baseline, excluded from validation range")
            base = workflow.git("rev-parse", "HEAD", root=root).strip()
            relative = "docs/bugfix/BUG-2026-001-sample.md"
            record = root / relative
            record.parent.mkdir(parents=True)
            record.write_text("Fixture record", encoding="utf-8")
            workflow.git("add", "--", relative, root=root)
            message = complete_message("fix(test): add a paired defect record") + f"\nBugfix: {relative}"
            commit(message)
            errors, count = workflow.check_commits(f"{base}..HEAD", root=root)
            self.assertEqual(([], 1), (errors, count))

            (root / "baseline.txt").write_text("changed", encoding="utf-8")
            workflow.git("add", "--", "baseline.txt", root=root)
            commit(message)
            errors, count = workflow.check_commits(f"{base}..HEAD", root=root)
            self.assertEqual(2, count)
            self.assertTrue(any("must add/update" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
