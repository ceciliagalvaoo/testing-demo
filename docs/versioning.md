# Versioning

This file defines how the LendIt Docker image `ghcr.io/ceciliagalvaoo/testing-demo/lendit-dotnet` is named (the same rules apply to the libraries of [`publish-demo`](https://github.com/ceciliagalvaoo/publish-demo)). The version is **never typed by hand**: it is derived from git by [`.github/scripts/version.sh`](../.github/scripts/version.sh).

## 1. Scheme: Semantic Versioning 2.0.0

`MAJOR.MINOR.PATCH[-PRERELEASE]`, for example `1.1.0`, `1.1.0-rc.1`, `1.1.0-dev.57`.

| Change in the libraries `LendIt.Pricing` / `lendit-pricing` / `@ceciliagalvaoo/lendit-pricing` | Increase |
|---|---|
| Incompatible API change: a public method renamed or removed, a changed signature (e.g. `Calculate(int)` → `Calculate(TimeSpan)`), a changed meaning of the result (e.g. the fee in cents instead of units) | **MAJOR** |
| New, backwards-compatible functionality: a new method or overload, a new optional parameter, a new configurable rule | **MINOR** |
| Backwards-compatible bug fix: a wrong rounding, a wrong boundary (day 14 vs. 15), documentation or packaging fixes | **PATCH** |

For the Docker image of the LendIt API, "the API" is the HTTP API: removing or changing an endpoint or a response field is MAJOR, a new endpoint is MINOR, a fix is PATCH.

Pre-releases sort **before** the release: `1.1.0-dev.2 < 1.1.0-dev.10 < 1.1.0-rc.1 < 1.1.0 < 1.1.1`.

## 2. Channels

| Channel | Triggered by | Version | Published to | Kept |
|---|---|---|---|---|
| none | pull request | – | **nothing**: build and test only | – |
| **dev** | push to `main` (after the tests pass) | `X.(Y+1).0-dev.<run number>` (next minor after the last `v*` tag) | GitHub Packages (NuGet, Maven, npm dist-tag `dev`); GHCR tags `<version>`, `dev` (and `<commit-sha>`) | short: last 20 dev versions or 14 days |
| **rc** | tag `vX.Y.Z-rc.N` | `X.Y.Z-rc.N` | GitHub Packages (npm dist-tag `next`); GHCR tags `<version>`, `rc`; GitHub pre-release | until `vX.Y.Z` is released |
| **release** | tag `vX.Y.Z` (after approval of the `production` environment) | `X.Y.Z` | GitHub Packages (npm dist-tag `latest`); GHCR + Docker Hub tags `X.Y.Z`, `X.Y`, `X`, `latest`; GitHub Release | **forever** |

Tag format, enforced by `version.sh` (any other `v*` tag fails the workflow): `^v[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$`.

Separation per ecosystem: Docker by tag, NuGet and Maven by pre-release versions (`--prerelease` / an exact version), npm by dist-tags (`dev`, `next`, `latest`).

## 3. Mutable and immutable names

| Name | Mutable? | Rule |
|---|---|---|
| `X.Y.Z`, `X.Y.Z-rc.N`, `X.Y.Z-dev.N` (all ecosystems) | **immutable** | never re-published; GitHub Packages refuses an existing NuGet/Maven/npm version (409); a fix is a new version |
| Docker `@sha256:…` digest | **immutable** by definition | deployment manifests use the digest or the exact version |
| Docker `:<commit-sha>` | immutable by convention | the *build identity*: exists only if that commit passed all tests |
| Docker `dev`, `rc`, `X.Y`, `X`, `latest` | **floating** | convenience only, never in a deployment manifest |
| npm dist-tags `dev`, `next`, `latest` | **floating** | point at the newest version of the channel |

## 4. Who may create a release

- A release is created **only** by pushing a tag `v*`. Nobody types a version into a workflow, and nothing is published from a pull request.
- Only the repository **owner / administrators** (`ceciliagalvaoo`) may push `v*` tags. Collaborators with *write* access can merge to `main` (dev channel) through pull requests with required checks, but cannot create a release.
- A `vX.Y.Z` release additionally waits for an approval of the `production` environment (required reviewer: `ceciliagalvaoo`), and the environment only accepts refs matching `v*`.
- Tags are never moved or deleted after they were pushed; a bad release is fixed with a new PATCH version.
