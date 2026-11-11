# testing-demo

![dotnet-ci](https://github.com/ceciliagalvaoo/testing-demo/actions/workflows/dotnet-ci.yml/badge.svg)

Applied DevOps **Lab 7 – Automated testing and the CI/CD pipeline**.

**LendIt** is a small ASP.NET Core (.NET 10) application that lends items to borrowers. It is tested on four levels and every push / pull request runs the [`dotnet-ci`](.github/workflows/dotnet-ci.yml) pipeline:

```text
build + unit tests (coverage gate ≥ 80 %)  →  integration tests  →  image + E2E + UI tests  →  push to ghcr.io (main / v* only)
```

| Level | Project | Framework | Tests |
|-------|---------|-----------|-------|
| Unit | `dotnet-app/tests/LendIt.Tests` (`Category=Unit`) | xUnit v2 + NSubstitute + `FakeTimeProvider` | 30 |
| Integration | `dotnet-app/tests/LendIt.Tests` (`Category=Integration`) | xUnit + `WebApplicationFactory<Program>` | 15 |
| E2E | `dotnet-app/tests/LendIt.E2E.Tests` | xUnit + `HttpClient` against the running container | 3 journeys |
| UI | `dotnet-app/tests/LendIt.UI.Tests` | NUnit + Playwright (Chromium), page object | 6 |

## Run locally

```bash
cd dotnet-app
dotnet test tests/LendIt.Tests --filter "Category=Unit"
dotnet test tests/LendIt.Tests --filter "Category=Integration"

docker build -t lendit:local . && docker run -d --name lendit -p 8080:8080 lendit:local
BASE_URL=http://localhost:8080 dotnet test tests/LendIt.E2E.Tests
pwsh tests/LendIt.UI.Tests/bin/Debug/net10.0/playwright.ps1 install chromium   # once, after a build
BASE_URL=http://localhost:8080 dotnet test tests/LendIt.UI.Tests
docker rm -f lendit
```

Do not run a plain `dotnet test` at the solution level: the E2E and UI projects need a running application.

Published images: `ghcr.io/ceciliagalvaoo/testing-demo/lendit-dotnet:main` and `:<version>` for `v*` tags.
