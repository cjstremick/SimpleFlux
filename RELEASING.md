# Releasing SimpleFlux

This guide explains the entire release system: how versions work, how to publish a
**prerelease**, and how to **promote** it to a stable release. The automation lives in
three GitHub Actions workflows — you never have to run `dotnet nuget push` by hand.

## The three workflows

| Workflow | Runs when | What it does |
|---|---|---|
| `ci.yml` | Automatically: every PR and push to `main` | Restore → build → test (excludes Azurite tests) → pack. **Publishes nothing.** This is your safety net. |
| `Publish Prerelease` | Manually: Actions tab → Run workflow | Builds and publishes a prerelease (e.g. `1.1.0-alpha.1`) of **all** SimpleFlux packages (`SimpleFlux`, `SimpleFlux.AzureTables`, `SimpleFlux.InMemory`) to NuGet, creates a GitHub **Pre-release** + tag (`v1.1.0-alpha.1`). |
| `Publish Release` | Manually: Actions tab → Run workflow | **Promotes** a prerelease to stable (e.g. `1.1.0-alpha.1` → `1.1.0`), publishes all packages to NuGet, creates the GitHub Release + tag (`v1.1.0`). Can also release a stable version directly. |

The version input applies to every package — they always publish together at the
same version. The duplicate-version guard checks the core `SimpleFlux` package.

All three workflows validate their inputs, fail fast with clear error messages, and
refuse to publish a version that already exists on NuGet (NuGet versions are
**immutable** — you can never overwrite or delete a published version).

## Versioning rules (semver)

SimpleFlux follows [Semantic Versioning](https://semver.org): `MAJOR.MINOR.PATCH`.

- **MAJOR** — breaking API changes (consumers must change code)
- **MINOR** — new features, backwards compatible
- **PATCH** — bug fixes, backwards compatible
- **Prerelease suffix** — appended with a dash for anything not final: `1.1.0-alpha.1`, `1.1.0-beta.2`, `1.1.0-rc.1`

NuGet orders versions automatically. For the same base version:

```
1.1.0-alpha.1  <  1.1.0-alpha.2  <  1.1.0-beta.1  <  1.1.0-rc.1  <  1.1.0  (stable)
```

The suffix is your choice — common conventions: `alpha` (early/experimental),
`beta` (feature-complete, testing), `rc` (release candidate, final polish).
Prerelease packages are **not** picked up by default when consumers install
SimpleFlux — they must opt in (see [Consuming prereleases](#consuming-prereleases)).

Where does the version live? The csproj only defines a local default
(`VersionPrefix` = 1.0.0). **The workflows pass the exact version at pack time**
(`-p:Version=...`) — you never edit the csproj to release.

## One-time setup (do this once)

Publishing uses [NuGet trusted publishers](https://learn.microsoft.com/en-us/nuget/create-packages/trusted-publishers) — no long-lived API key stored in GitHub secrets. GitHub issues a short-lived OIDC token that NuGet verifies against your repo + workflow.

### 1. Link your nuget.org account to GitHub

1. Go to [nuget.org → Account Settings → Trusted Publishers](https://www.nuget.org/account) and configure trusted publishing for the `SimpleFlux` package.
2. Add your GitHub repository (`cjstremick/SimpleFlux`) and the workflow filenames (`publish-prerelease.yml`, `publish-release.yml`).

### 2. Add the NUGET_USER secret

1. GitHub → SimpleFlux → **Settings → Secrets and variables → Actions**.
2. Click **New repository secret**.
3. Name: `NUGET_USER` (exact spelling), value: your **nuget.org profile name** (not your email address).

Both publish workflows use this to exchange a GitHub OIDC token for a short-lived NuGet API key via `NuGet/login@v1`. No static API key is ever stored in the repo.

### 3. Make sure Actions is enabled

GitHub → SimpleFlux → **Settings → Actions → General** → ensure **Allow all actions and reusable workflows** is on. (The repo's old workflow never recorded a run, so if the new ones don't appear, check this first.)

## Publishing a prerelease

1. Go to the **Actions** tab → **Publish Prerelease** → **Run workflow**.
2. Enter the version, e.g. `1.1.0-alpha.1`, pick branch `main`, click **Run workflow**.
3. Watch it: validate version → check NuGet → build → pack → OIDC login → push → GitHub prerelease.

When it finishes:
- NuGet: [SimpleFlux](https://www.nuget.org/packages/SimpleFlux) shows `1.1.0-alpha.1`
  (it appears under "Versions" with a prerelease flag)
- GitHub: a Pre-release `v1.1.0-alpha.1` with the `.nupkg` attached

Repeat as many times as you like, bumping the suffix: `1.1.0-alpha.2`, `-beta.1`, etc.

## Promoting a prerelease to a stable release

1. Go to the **Actions** tab → **Publish Release** → **Run workflow**.
2. Enter **the prerelease version** you already published, e.g. `1.1.0-alpha.1`.
3. Click **Run workflow**. The workflow:
   - validates the input,
   - verifies `SimpleFlux 1.1.0-alpha.1` actually exists on NuGet (fails if you try to promote something that was never published),
   - strips the suffix → `1.1.0`,
   - publishes the stable `1.1.0` package,
   - creates the GitHub Release `v1.1.0` (marked Latest) with the `.nupkg` attached.

You now have both `1.1.0-alpha.1` (prerelease, still on NuGet — that's normal and
fine) and `1.1.0` (stable) published.

## Releasing directly (no prerelease)

Skipping the prerelease dance? Run **Publish Release** with a stable version, e.g.
`1.1.0`. It publishes it straight to NuGet and creates the GitHub Release. (You can't
accidentally publish a stable version through the prerelease workflow — it rejects
inputs without a suffix.)

## Consuming prereleases

Prereleases are opt-in for anyone installing SimpleFlux:

```bash
# Stable only (default)
dotnet add package SimpleFlux

# Include prereleases
dotnet add package SimpleFlux --prerelease
# or pin one explicitly
dotnet add package SimpleFlux --version 1.1.0-alpha.1
```

Backends are separate packages: add `SimpleFlux.AzureTables` (or `SimpleFlux.InMemory`)
alongside the core package at the same version.

In a .csproj, `<PackageReference Include="SimpleFlux" Version="1.1.0-alpha.1" />`
works as-is; for wildcards add `AllowPrereleaseVersions="true"`:
`Version="1.1.0-*" AllowPrereleaseVersions="true"`.

## What each workflow does under the hood

**Validation:** every input must match semver (`1.2.3` or `1.2.3-suffix.N`). Bad input → immediate, readable failure.

**Duplicate guard:** a curl check against NuGet's own API refuses to publish a version that already exists. (NuGet would reject it anyway — this just fails *before* building.)

**Build & pack:** `dotnet build` + `dotnet pack` with `-p:Version=<input>`, so the package, assembly, and file versions all match the release.

**Publish:** `dotnet nuget push` to api.nuget.org using a short-lived OIDC token (`NuGet/login@v1`). No long-lived API key is stored in the repo.
Packages include the README, icon, license (MIT), XML docs, and **debug symbols**
(`.snupkg` + SourceLink), so consumers can step into the library source.
nuget.org shows the icon, description, repository link, and changelog link automatically.

**GitHub release:** the `gh` CLI creates the tag + release (Pre-release for prereleases) and attaches the `.nupkg`, so every version is browsable and downloadable from GitHub too.

**Safety:** `concurrency` blocks parallel publishes, so two runs can't push at the same time.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| OIDC login fails / no `NUGET_API_KEY` output | Check that `NUGET_USER` secret is set (your nuget.org profile name, not email). Verify trusted publishers are configured on nuget.org for this repo + workflow. |
| "already exists on NuGet (HTTP 200)" | You (or a previous run) already published that version. NuGet versions are immutable — pick the next version. |
| "SimpleFlux X was not found on NuGet" | Promoting a prerelease that was never published. Run *Publish Prerelease* first. |
| Push fails with 401/403 | OIDC token rejected — verify trusted publishers match the repo owner, repo name, and workflow filename exactly. |
| Workflow doesn't appear in the Actions tab | Push a commit that touches `.github/workflows/` to main, then check Actions is enabled (one-time setup, step 3). |
| GitHub Release says "tag already exists" | The tag `vX.Y.Z` exists but the release was deleted. Delete the tag (git push origin :refs/tags/vX.Y.Z) and re-run, or pick a new version. |

## FAQ

- **Can I delete a bad package from NuGet?** Not really — NuGet allows *unlisting* (nuget.org → Manage packages → Unlist) but never deletion, and the version stays taken forever. Always test prereleases before promoting.
- **Why manual instead of automatic publishing on push?** You get a deliberate "do I really want to ship this?" checkpoint. CI still runs automatically on every push/PR, so nothing breaks silently.
- **What's the version after 1.1.0?** When you want to publish `1.2.0-beta.1`, just type it — the workflows don't care what came before.
- **Do I need to bump anything in the csproj?** No. `VersionPrefix` is only the local build default.
