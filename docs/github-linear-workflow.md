# GitHub and Linear workflow

This repo uses Linear issue IDs in branches, commits, and pull requests so work is traceable from code back to the issue.

## Linear status mapping

| Linear status | Meaning | GitHub signal |
| --- | --- | --- |
| Backlog | Idea exists, not selected for implementation | No branch or pull request yet |
| Todo | Scoped and ready to start | Issue assigned or selected for the current work batch |
| In Progress | Implementation is underway | Branch created, first commit pushed, or draft PR opened |
| In Review | Code is ready for review | Pull request opened or review requested |
| Ready QA | Review and CI are green; manual game QA remains | Pull request is mergeable and required CI checks passed |
| Done | Accepted and integrated | Pull request or linked commit merged to the default branch |
| Canceled | Work intentionally stopped | Manual status change |
| Duplicate | Same work is tracked by another issue | Manual status change |

## Branches

Use the Linear issue ID at the start of the branch name:

```text
TEO-14-animated-player-character
```

The repository default is usually `codex/<issue-id>-<short-slug>`, but if a task explicitly requests no `codex/` prefix, use the issue ID directly.

## Commits

Use a conventional commit subject and include the Linear issue ID:

```text
feat(game): add animated ranger player TEO-14
```

To make the commit appear under the Linear issue through commit linking, include a Linear magic word in the commit body:

```text
Refs TEO-14
```

Use `Refs` by default so partial commits link to the issue without closing it. Only use closing magic words such as `Implements`, `Fixes`, or `Closes` after testing is successful, functionality is complete, and user acceptance criteria are met.

Optional local setup for the commit template:

```powershell
git config commit.template .gitmessage
```

## Pull requests

Use the issue ID in the PR title:

```text
TEO-14 Add animated ranger player
```

Keep the PR template's Linear section and replace the placeholder:

```text
Refs TEO-14
```

This links the pull request to the Linear issue without closing it early. Replace `Refs` with `Implements` or `Fixes` only when the PR represents complete, tested, user-accepted work that should participate in Linear closing automation.

## CI and branch protection

The CI workflow lives at `.github/workflows/ci.yml` and runs:

- `dotnet restore`
- Python tests for asset tooling
- `dotnet test --no-restore`
- `dotnet build --no-restore`

Recommended GitHub branch protection for `main` or `master`:

- Require pull requests before merging.
- Require the `CI / .NET build and tests` check to pass.
- Require branches to be up to date before merging if multiple people are changing the same code.

With branch protection enabled, Linear's "ready for merge" automation can safely move linked issues to `Ready QA` only after GitHub reports the PR as stable and mergeable.

## Linear automation settings

Configure these in Linear:

```text
Settings -> Team -> Workflows & automations -> Pull request and commit automations
```

Suggested mappings:

| Automation event | Move issue to |
| --- | --- |
| Branch copied or work started | In Progress |
| Draft PR opened | In Progress |
| PR opened | In Review |
| Review requested or review activity | In Review |
| PR ready for merge | Ready QA |
| PR or linked commit merged to default branch | Done |

Enable commit linking in the Linear GitHub integration settings and add Linear's webhook to the GitHub repository or organization with push events enabled. Without commit linking, PR linking will still work when the PR title or body contains `Refs <ISSUE-ID>`, but individual commits may not appear under the Linear issue.
