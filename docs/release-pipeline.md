# Release Pipeline

Step-by-step procedure for cutting a BeatDash **server** release (backend, admin CLI and
bundled web dashboard). Written for agents: follow the steps in order, run the commands
exactly as written, and stop and report to the user whenever a check fails.

Mod releases are not automated. `beatdash-mod/v*` tags only run CI; nothing is published.

## How it works

| Piece | Location | Role |
|---|---|---|
| Version source | `GitVersion.yml` (`tag-prefix: '^v'`) | Computes the server SemVer from tags and conventional commits |
| Release workflow | `.github/workflows/release.yml` | Fires on `v*` tag pushes, builds, tests, packages and publishes |
| CI workflow | `.github/workflows/ci.yml` | Also fires on `v*` tags (and `main` pushes / PRs) |
| Packaging | `tools/release/package.py` | Builds the server ZIP + `.sha256` checksum |

`release.yml` jobs, in order:

1. **version**: runs GitVersion at the tagged commit and asserts that its `SemVer` equals the
   tag without the leading `v`. Also runs `tools/versioning/test_versions.py` and
   `tools/release/test_package.py`.
2. **server** (matrix `linux-x64` on `ubuntu-24.04`, `win-x64` on `windows-2025`): publishes
   the API and CLI, runs `Shiron.BeatDash.API.Tests` against the published `wwwroot`, and
   packages `BeatDash-server-<VERSION>-<RID>.zip`.
3. **publish**: verifies checksums, creates a draft GitHub Release, uploads the assets and
   publishes it. Versions containing `-` (for example `0.1.0-alpha`) are marked as
   prereleases. Releases never move GitHub's `latest` pointer.

Pushing the `rel/<VERSION>` branch triggers no workflow. Only the tag starts a release.

## Naming rules

- `VERSION` **must be strict SemVer**: `MAJOR.MINOR.PATCH[-label]`, for example `0.1.0-alpha`,
  `0.2.0`, `1.0.0-rc.1`. GitVersion parses tags strictly, so `0.1-alpha` or `1.0` are
  ignored as version sources and fail the `version` job.
- Release branch: `rel/<VERSION>` (for example `rel/0.1.0-alpha`). GitVersion takes the
  branch's prerelease label from this name (`alpha`). A tag only counts as the version source
  when its label matches, so the branch name and the tag **must use the same `<VERSION>`**.
- Tag: `v<VERSION>` (for example `v0.1.0-alpha`). Use an **annotated** tag.
- Never reuse a version that already has a tag or a GitHub Release.

If the user gives a non-SemVer version, normalize it (`0.1-alpha` becomes `0.1.0-alpha`)
and tell them that you did.

## Prerequisites

Check all of these before creating anything. Stop and report if any fail.

```sh
git rev-parse --abbrev-ref HEAD        # must print: main
git status --porcelain                 # must print nothing
git fetch origin --tags
git status -sb | head -1               # main must not be behind origin/main
git tag -l "v<VERSION>"                # must print nothing
git ls-remote --tags origin "v<VERSION>"   # must print nothing
gh auth status                         # GitHub CLI must be authenticated
```

Everything you want in the release must already be committed on `main`. The release is
cut from the current `main` HEAD.

## Pre-release verification (local)

Run what the release workflow runs, so failures surface before anything is pushed:

```sh
dotnet tool restore
python3 tools/versioning/test_versions.py
python3 -B tools/release/test_package.py
dotnet build src/BeatDash.API/Shiron.BeatDash.API.csproj -c Release
dotnet build src/BeatDash.Cli/Shiron.BeatDash.Cli.csproj -c Release
dotnet test tests/BeatDash.API.Tests/Shiron.BeatDash.API.Tests.csproj -c Release --verbosity minimal
pnpm --filter @shiron/beatdash-web typecheck
pnpm --filter @shiron/beatdash-web exec biome ci src
```

Do not continue on failures. Fix them on `main` (or report them) first. Don't build the whole
solution: `src/BeatDash.Mod` needs a local Beat Saber install and isn't part of the server release.
Integration tests are skipped without Docker. Say so when you report results.

## Release steps

Replace `<VERSION>` with the exact version, for example `0.1.0-alpha`.

### 1. Create the release branch

```sh
git switch -c rel/<VERSION> main
```

### 2. Create the tag

```sh
git tag -a v<VERSION> -m "BeatDash server <VERSION>"
```

### 3. Verify the computed version

CI checks out the tag detached, and GitVersion versions it as the branch `tags/v<VERSION>`
(see the `tag` branch in `GitVersion.yml`). Reproduce that in a **throwaway clone**:

```sh
rm -rf /tmp/beatdash-release-check
git clone -q --branch v<VERSION> . /tmp/beatdash-release-check
GITHUB_ACTIONS=true GITHUB_REF=refs/tags/v<VERSION> GITHUB_REF_NAME=v<VERSION> \
  dotnet tool run dotnet-gitversion /tmp/beatdash-release-check /config GitVersion.yml /nofetch /showvariable SemVer
rm -rf /tmp/beatdash-release-check
```

This must print exactly `<VERSION>`. If it doesn't, the `version` job fails in CI. Delete
the local tag (`git tag -d v<VERSION>`), switch back to `main`, delete the local branch
(`git branch -D rel/<VERSION>`) and report the mismatch.

> **Never** run GitVersion with the `GITHUB_*` variables against the real checkout. In CI mode
> GitVersion "normalizes" the repository: it resets local branches to their `origin/*`
> counterparts (dropping unpushed commits on `main`) and creates `tags/*` branches. Only run it
> against the throwaway clone. Outside CI, always pass `/nonormalize`.

### 4. Switch back to main

```sh
git switch main
```

### 5. Push the release branch and the tag

Push them explicitly. Never use `git push --tags`, which would publish every local tag.

```sh
git push origin rel/<VERSION>
git push origin v<VERSION>
```

Pushing the tag starts the `GitHub releases` and `CI` workflows.

## Inspecting the workflow runs

**Don't poll.** Never check status repeatedly across turns, and never loop `sleep` +
`gh run list`. Use one blocking command per run, ideally in the background so you get
notified when it finishes.

```sh
# Find the runs that the tag push started
gh run list --event push --branch v<VERSION> --json databaseId,workflowName,status,conclusion,url

# Block until a run finishes (single command, exits non-zero on failure)
gh run watch <RUN_ID> --exit-status --interval 30 > /dev/null; gh run view <RUN_ID>
```

If `gh run list` returns nothing straight after the push, the run hasn't registered yet.
Run `gh run list` once more a few seconds later as a single command. Don't start a loop.

When a run fails, inspect only the failing jobs:

```sh
gh run view <RUN_ID>                   # job overview + annotations
gh run view <RUN_ID> --log-failed      # logs of failed steps only
```

Report every failure to the user with the job name, the failing step and the relevant
log excerpt. Common causes:

| Failing step | Likely cause |
|---|---|
| `version` → `test "$VERSION" = "$EXPECTED"` | Tag isn't strict SemVer, isn't on the expected commit, or `GitVersion.yml` was changed without updating `tools/versioning/test_versions.py` |
| `server` → Publish / Test | Build or test regression; reproduce locally with the commands above |
| `server` → Package | `package.py` found a missing asset or local config (`appsettings.json`, `.env`) |
| `publish` → gh release | Repository Actions settings deny `contents: write` |

### Recovering from a failure

- **Transient failure (runner, network):** `gh run rerun <RUN_ID> --failed`. The `publish` job
  resumes an existing draft release, so reruns are safe.
- **Code failure:** don't move or overwrite the pushed tag. Deleting or re-pointing a
  published tag is destructive and needs **explicit user approval**. Fix the bug on `main`
  and release the next version (for example `0.1.0-alpha.2` or `0.1.1`).

## Verifying the result

When the release run succeeds:

```sh
gh release view v<VERSION>
```

Confirm all of these:

- Release title is `BeatDash server <VERSION>`, and it's marked as a prerelease if
  `<VERSION>` contains `-`
- Assets `BeatDash-server-<VERSION>-linux-x64.zip`, `BeatDash-server-<VERSION>-win-x64.zip`
  and their `.sha256` files are attached
- The release isn't a draft

Then give the user the release URL, each workflow's conclusion, and any failures.

## Checklist

- [ ] On `main`, clean tree, up to date with `origin/main`
- [ ] Version is strict SemVer and unused
- [ ] Local verification passed
- [ ] `rel/<VERSION>` created from `main`
- [ ] Annotated tag `v<VERSION>` created, GitVersion prints `<VERSION>`
- [ ] Switched back to `main`
- [ ] Pushed `rel/<VERSION>` and `v<VERSION>` explicitly
- [ ] Workflow runs inspected with blocking commands, failures reported
- [ ] Release and assets verified
