---
name: commit-and-release
description: Review uncommitted ClientManager changes, split into subject commits, push to main, and publish a GitHub release with repo-standard notes and version bump. Use when the user asks to commit, ship, push, cut a release, or publish a version.
---

# Commit and Release

End-to-end workflow: inventory → scope with user → subject commits → push → version bump → GitHub release.

## Phase 1 — Inventory

Run in parallel:

```bash
git status
git diff --stat
git diff
git log --oneline -10
git branch -vv
```

Classify every changed/untracked path:

| Bucket | Action |
|--------|--------|
| **Ship** | Source, config, docs the user wants committed |
| **Skip** | Build/runtime junk — do not stage |
| **Ask** | Ambiguous local files — confirm with user |

### Always skip (never commit)

- `bin/`, `obj/`, `_build_out/`, `.build-out/`, `.artifacts/`
- `.release-bundles/`, `.github/scripts/.release-bundles/`
- `data/`, `logs/`, `lucene-index/` (runtime data)
- Line-ending-only diffs with no content change — mention and leave unstaged

Present skipped files to the user. **Ask whether to delete** local junk (`_build_out/`, `.release-bundles/`, etc.) or leave them.

## Phase 2 — Scope with user

Before staging anything, summarize changes by subject and ask:

1. **What to include** — all ship bucket, or a subset?
2. **How to split** — propose logical commits (one subject per commit); confirm grouping.
3. **Release version** — e.g. `1.2.3` (patch), `1.3.0` (minor). If unstated, suggest next patch from latest `gh release list` tag.
4. **Release section** — patch → `Bugfixes & Tweaks` only; minor → `New Features` + optional `Bugfixes & Tweaks`.

Use `/grilling` if scope, version, or release notes are unclear. One question at a time.

## Phase 3 — Commit by subject

For each agreed group:

1. `git add` only that group's files
2. Commit with repo style: imperative summary, optional body explaining **why**
3. `git status` after each commit

Example subjects from this repo: logging, chart UI, localization fixes, CI/workflow, docs.

**Never** mix unrelated subjects in one commit. **Never** commit skip-bucket files.

## Phase 4 — Push

```bash
git push origin main
```

`main` is protected — retry with user approval if blocked.

## Phase 5 — Release

### Version bump

Bump `<Version>` in all four projects (same value):

- `ClientManager.Api/ClientManager.Api.csproj`
- `ClientManager.AdminUI/ClientManager.AdminUI.csproj`
- `ClientManager.Shared/ClientManager.Shared.csproj`
- `ClientManager.DataAccess/ClientManager.DataAccess.csproj`

Commit message: `Bump version to X.Y.Z.`

Push the bump commit before tagging.

### Release notes format (static — do not invent new structure)

**Patch release** (`X.Y.Z` where Z > 0):

```markdown
### Bugfixes & Tweaks 🛠️
 - **Short bold title** optional detail after title or on same line with dash
```

**Minor release** (`X.Y.0`):

```markdown
### New Features 🥳
 - **Feature title** - detail

### Bugfixes & Tweaks 🛠️
 - **Fix title** - detail
```

Rules:
- Section headers exactly as above (emoji included)
- Each bullet: ` - **Bold lead**` then optional ` - detail` or trailing clause
- Derive bullets from commits since the previous release tag — user-facing outcomes, not file names
- Omit internal-only CI/infra commits unless user asks

### Publish

```bash
gh release create X.Y.Z --title "X.Y.Z" --notes "$(cat <<'EOF'
<notes body>
EOF
)"
```

On Windows PowerShell, use a here-string (`@"..."@`) instead of heredoc.

Verify:

```bash
gh release view X.Y.Z
gh run list --workflow=release-bundles.yml --limit 1
```

The `release-bundles` workflow auto-uploads:

- `ClientManager-X.Y.Z.full.bundle`
- `ClientManager-<prev>-to-X.Y.Z.bundle`

Return the release URL to the user.

## Checklist

```
- [ ] git status/diff reviewed; skip bucket listed
- [ ] User confirmed scope, commit split, and version
- [ ] Skipped junk: user asked about deletion
- [ ] One commit per subject
- [ ] Pushed to origin/main
- [ ] Version bumped in 4 csproj files
- [ ] Release notes match repo template
- [ ] gh release created; bundle workflow succeeded
```
