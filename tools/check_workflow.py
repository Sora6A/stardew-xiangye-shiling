"""Check maintenance records and complete commit messages (Python standard library only).

This is a structural check, not a claim that the documented tests or root causes are correct.
Git commands are read-only and receive separate arguments, never shell-interpolated messages.
"""
import argparse
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
SECTIONS = ("背景与目的", "变更内容", "验证结果", "影响与风险", "回滚方案", "关联记录")
BUG_SECTIONS = ("现象与影响", "环境与前置条件", "复现步骤", "根因分析", "解决方案",
                "单元测试", "验证结果", "风险与回滚", "关联记录")
HANDOFF_SECTIONS = ("当前目标与状态", "分支与工作区", "已完成与入口", "验证与限制",
                    "环境与运行状态", "下一步与验收", "关联记录")
SUBJECT = re.compile(r"^(feat|fix|docs|refactor|test|build|ci|chore|revert|merge)\([a-z0-9][a-z0-9._/-]*\)!?: \S.+$")
BUG_PATH = re.compile(r"docs/bugfix/BUG-\d{4}-\d{3}-[a-z0-9-]+\.md")
PLACEHOLDER = re.compile(r"<[^>\n]*(?:填写|具体变化|触发条件|主要模块|实际命令|未覆盖范围|迁移影响|必须增加)[^>\n]*>|(?m:^\s*(?:TODO|TBD|待填写)\s*$)")


def sections(text):
    """Read level-two sections; duplicate headings must not hide an empty earlier section."""
    parts = re.split(r"(?m)^## ([^\n]+)\n", text.replace("\r\n", "\n"))
    values, duplicates = {}, []
    for heading, body in zip(parts[1::2], parts[2::2]):
        heading = heading.strip()
        if heading in values:
            duplicates.append(heading)
        values[heading] = body.strip()
    return values, duplicates


def require_sections(text, required, label):
    values, duplicates = sections(text)
    errors = [f"{label}: duplicate section {name}" for name in duplicates]
    errors += [f"{label}: missing or empty section {name}" for name in required if not values.get(name)]
    return errors


def validate_message(message, root=ROOT):
    message = message.strip()
    first = message.splitlines()[0] if message else ""
    match = SUBJECT.fullmatch(first)
    errors = [] if match else ["Commit title must be type(scope): concrete change"]
    errors += require_sections(message, SECTIONS, "commit")
    if PLACEHOLDER.search(message):
        errors.append("Commit contains an unfilled template placeholder")
    values, _ = sections(message)
    if "docs/HANDOFF.md" not in values.get("关联记录", ""):
        errors.append("Commit must reference docs/HANDOFF.md in 关联记录")
    bug_paths = set(BUG_PATH.findall(values.get("关联记录", "")))
    if match and match.group(1) == "fix" and not bug_paths:
        errors.append("Fix commit must reference a concrete docs/bugfix/BUG-YYYY-NNN-topic.md record")
    for path in bug_paths:
        if not (root / path).is_file():
            errors.append(f"Bugfix record does not exist: {path}")
    return errors


def check_docs(root=ROOT):
    errors = []
    for relative in ("AGENTS.md", "CONTRIBUTING.md", "docs/HANDOFF.md", "docs/ARCHITECTURE.md",
                     "docs/bugfix/README.md", "docs/bugfix/TEMPLATE.md", ".github/COMMIT_TEMPLATE.txt",
                     ".github/PULL_REQUEST_TEMPLATE.md", "docs/templates/HANDOFF_TEMPLATE.md"):
        if not (root / relative).is_file():
            errors.append(f"Missing maintenance document: {relative}")
    handoff = root / "docs/HANDOFF.md"
    if handoff.is_file():
        errors += require_sections(handoff.read_text(encoding="utf-8"), HANDOFF_SECTIONS, "HANDOFF")
    index = root / "docs/bugfix/README.md"
    index_text = index.read_text(encoding="utf-8") if index.is_file() else ""
    for path in sorted((root / "docs/bugfix").glob("BUG-*.md")):
        body = path.read_text(encoding="utf-8")
        errors += require_sections(body, BUG_SECTIONS, str(path.relative_to(root)))
        if not re.search(r"(?m)^- 状态: \S", body):
            errors.append(f"Bugfix record needs an explicit status: {path.name}")
        if PLACEHOLDER.search(body):
            errors.append(f"Bugfix record has an unfilled placeholder: {path.name}")
        if path.name not in index_text:
            errors.append(f"Bugfix record is missing from the index: {path.name}")
    # Check relative file targets, not remote availability or Markdown anchor spelling.
    documents = [root / n for n in ("README.md", "AGENTS.md", "CONTRIBUTING.md")]
    documents += list((root / "docs").rglob("*.md")) + list((root / ".github").glob("*.md"))
    for path in documents:
        if not path.is_file():
            continue
        body = re.sub(r"(?ms)^```.*?^```[^\n]*", "", path.read_text(encoding="utf-8"))
        for target in re.findall(r"\[[^\]\n]+\]\(([^)\n]+)\)", body):
            target = target.strip().strip("<>")
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            resolved = (path.parent / unquote(parsed.path)).resolve()
            if not resolved.is_relative_to(root.resolve()) or not resolved.exists():
                errors.append(f"Broken/nonportable local link in {path.relative_to(root)}: {target}")
    return errors


def git(*args, root=ROOT):
    # Per-command trust of this explicit checkout handles Windows ownership differences.
    return subprocess.run(["git", "-c", f"safe.directory={root.as_posix()}", "-C", str(root), *args],
                          check=True, capture_output=True, text=True, encoding="utf-8").stdout


def check_commits(revision_range, root=ROOT):
    match = re.fullmatch(r"([A-Za-z0-9][A-Za-z0-9_./-]*)\.\.([A-Za-z0-9][A-Za-z0-9_./-]*)", revision_range)
    if not match:
        raise ValueError("Use an explicit base..head range, e.g. origin/main..HEAD")
    base, head = (git("rev-parse", "--verify", ref + "^{commit}", root=root).strip() for ref in match.groups())
    commits = git("rev-list", "--reverse", f"{base}..{head}", root=root).splitlines()
    if not commits:
        raise ValueError("No commits in range; check a draft with --message before creating the first commit")
    errors = []
    for sha in commits:
        message = git("show", "-s", "--format=%B", sha, root=root)
        errors += [f"{sha}: {error}" for error in validate_message(message, root)]
        if message.startswith("fix("):
            changed = set(git("diff-tree", "--root", "-m", "--no-commit-id", "--name-only", "-r", sha, root=root).splitlines())
            records = set(BUG_PATH.findall(message))
            for path in records:
                if path not in changed:
                    errors.append(f"{sha}: fix commit must add/update its referenced bugfix record: {path}")
    return errors, len(commits)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--message", type=Path, help="Validate a UTF-8 full commit message before committing")
    parser.add_argument("--commit-range", help="Validate all commits, including merges, in explicit base..head")
    args = parser.parse_args()
    try:
        errors = check_docs()
        if args.message:
            errors += validate_message(args.message.read_text(encoding="utf-8-sig"))
        count = 0
        if args.commit_range:
            commit_errors, count = check_commits(args.commit_range)
            errors += commit_errors
        if errors:
            for error in errors:
                print(f"ERROR: {error}", file=sys.stderr)
            return 1
        print(f"PASS: maintenance records and local links; {count} committed messages checked."
              + (" Draft message checked." if args.message else ""))
        return 0
    except (OSError, ValueError, subprocess.CalledProcessError) as ex:
        print(f"WORKFLOW CHECK FAILED: {ex}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
