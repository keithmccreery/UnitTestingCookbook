# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A personal "Unit Testing Cookbook" - a reference/presentation, not a library or service. Each topic (AwesomeAssertions,
Data Driven tests, DI, Docker, HttpClientFactory, Logging, Polly Policies, System.IO.Abstractions, WireMock.Net,
etc.) is a paired **README chapter + NUnit test file**: `README/README_<Topic>.md` explains the pattern with prose
and an embedded copy of the code, and `UnitTestingCookbook.Tests/<Topic>Tests.cs` is the actual, runnable version of
that same code. See `README.md` for the full chapter list and the project's stated non-goals (it's explicitly not a
theory-of-testing document, and it deliberately includes a few "don't do that, but here's why" anti-pattern examples
- see `README_DataHangover.md` and the "BUG" comments in `WireMockNetPollyPoliciesTests.cs`).

**The READMEs are hand-maintained copies, not generated.** There is no tooling that keeps a README's fenced
` ```csharp ` blocks in sync with the real source file. Any change to a test method's code, or to its surrounding
setup/teardown, must be re-copied into the corresponding README section by hand, or the two drift (this has
happened before - see git history for fixes to README/source drift).

## Commands

Build the whole solution:
```
dotnet build UnitTestingCookbook.sln
```

Run the full test suite:
```
dotnet test UnitTestingCookbook.Tests/UnitTestingCookbook.Tests.csproj
```

Run a single test by name:
```
dotnet test UnitTestingCookbook.Tests/UnitTestingCookbook.Tests.csproj --filter "FullyQualifiedName~MethodName"
```

Run/exclude by NUnit category (categories are assigned via `[Category("...")]`; every test also carries one of
`_passes` / `_fails` / `_false_positive` / `_false_negative` marking its **intended** outcome - a test tagged
`_fails` or `_false_negative` failing is expected, not a regression):
```
dotnet test UnitTestingCookbook.Tests/UnitTestingCookbook.Tests.csproj --filter "TestCategory=wiremocknet_pollypolicies"
dotnet test UnitTestingCookbook.Tests/UnitTestingCookbook.Tests.csproj --filter "TestCategory!=wiremocknet_pollypolicies"
```

Check/apply formatting per `.editorconfig`:
```
dotnet format UnitTestingCookbook.sln --verify-no-changes
dotnet format UnitTestingCookbook.sln
```

Run the same filtered set CI runs (excludes the by-design failures - see below):
```
dotnet test UnitTestingCookbook.sln --filter "TestCategory!=_fails&TestCategory!=_false_negative"
```

Notes on running tests:
- `WireMockNetPollyPoliciesTests.cs` exercises real Polly wait/retry/circuit-breaker delays against a local
  WireMock.Net server - the full suite takes roughly a minute, dominated by this file.
- `DockerTests.cs`'s one test is permanently `[Ignore]`d - not because the technique doesn't work (verified to
  pass locally, Docker running, attribute temporarily removed), but as a deliberate development-speed tradeoff:
  a real container adds real wall-clock time to every full test-suite run. See `README_Docker.md`.
- `UnitTestingCookbook.Tests/GlobalAttributes.cs` sets `[assembly: LevelOfParallelism(4)]` - this has no effect
  on any fixture except `ParallelProcessingSafeTests`, the only one marked `[Parallelizable]`. Every other
  fixture keeps running sequentially exactly as before; see `README_ParallelProcessing.md` for why most of this
  repo's `TestHelpers` (env vars, console capture) are unsafe to parallelize as-is.
- After changing package versions, `dotnet build`/`dotnet restore` may print `NU1608` warnings for
  `Humanizer.Core.<locale>` satellite packages - these come from WireMock.Net's own transitive dependency on an
  older Humanizer and are resolved correctly (the repo's direct reference wins); they're noise, not a real conflict.

CI (`.github/workflows/ci.yml`) runs restore/build/test on push and PR to master with the same filtered command
above, against `Release` configuration.

## Architecture

Five projects:
- **UnitTestingCookbook.Support** - the "production" code every chapter's tests exercise (`HttpBinOrgService`,
  `Miscellaneous`, sample logging/IO wrapper classes, `PollyContextExtensions`). Not a real product; exists purely
  to give the test chapters something realistic to test against.
- **UnitTestingCookbook.TestHelpers** - a small reusable test-helper library (`ManageCaptureConsole`,
  `ManageEnvironmentVariables`, `TestHelper.CreateInstanceInternal` for invoking non-public constructors,
  `ReflectionExtensions`, `MockLoggerExtensions`, `TestCorrelatorExtensions`, `HttpClientExtensions`,
  `HttpRequestsDetector`). Some chapters (e.g.
  `GeneralTipsTests.A_CaptureConsole` vs `A_CaptureConsole_TestHelpers`) deliberately show the same technique
  implemented inline *and* via this shared library, back to back, as a comparison. Note: this project's name ends
  in "Helpers", not "Test"/"Tests", so it does **not** match `.editorconfig`'s test-project exemption glob (see
  below) - it's held to the same analyzer bar as Support, deliberately.
- **UnitTestingCookbook.TestHelpers.Tests** - unit tests for TestHelpers itself, using NUnit + AwesomeAssertions +
  Moq. Kept separate from `UnitTestingCookbook.Tests` because it tests the helper library's own behavior rather
  than demonstrating a cookbook technique - these aren't README chapters, just correctness coverage for shared
  test infrastructure.
- **UnitTestingCookbook.MinimalApi** - a small real ASP.NET Core app (`Microsoft.NET.Sdk.Web`), not folded into
  Support since it needs a real app host. Hosts both the MinimalApi Integration Testing chapter's "animal store"
  endpoints and the AppSettings/Connection String Validation chapters' `.ValidateOnStart()` options wiring - the
  latter two have no NUnit tests of their own (see their READMEs for why), but every
  `WebApplicationFactory<Program>` created in `MinimalApiTests.cs` boots this app for real, so `appsettings.json`
  has to stay valid for *any* of these three chapters' tests to pass.
- **UnitTestingCookbook.Tests** - the cookbook itself: one test fixture per README chapter, using NUnit +
  AwesomeAssertions.

Version/property management is centralized at the repo root: `Directory.Build.props` holds the
`TargetFramework`/`ImplicitUsings`/`Nullable` settings shared by all five projects (plus a global set of
code-quality analyzer `PackageReference`s - see below), and `Directory.Packages.props` centrally manages every
package version (`ManagePackageVersionsCentrally=true`) - individual `.csproj` files reference packages by name
only, no `Version=` attribute.

**Gotcha with `dotnet add package`**: it inserts the new `<PackageVersion>` entry into `Directory.Packages.props`
purely by alphabetical proximity to whatever's nearby - it doesn't know the file has two separate `<ItemGroup>`s
(one `Label="Analyzers"` for pure analyzer packages, one for everything else), so a package like `NSubstitute`
or `Microsoft.EntityFrameworkCore` reliably lands in the wrong (`Analyzers`) group if its name happens to sort
near an analyzer package. It also strips the file's blank-line formatting and trailing newline as a side effect.
Always diff `Directory.Packages.props` after running `dotnet add package` and fix both before committing -
happened repeatedly (four separate packages) in one session before this was caught and documented.

### Analyzers: two separate things that both have to be true

`.editorconfig` sets severities for diagnostics from several third-party analyzers (Meziantou/`MA`, ErrorProne.NET/
`EPC`+`EPS`, AsyncFixer/`AsyncFixerNN`, IDisposableAnalyzers/`IDISP`, VS Threading/`VSTHRD`, Roslynator/`RCS`,
StyleCop/`SA`+`SX`). Setting a severity does nothing by itself - the analyzer package that actually *produces*
that diagnostic ID has to be referenced too (`Directory.Build.props`, global to every project). This repo went
through a period where the severities were configured but the packages weren't installed, so double-check both
sides are present when working with `.editorconfig` in a repo you don't fully recognize.

**Gotcha if you ever touch the test-project-exemption glob** (`[**{Test,Tests}/**.cs]`, near the bottom of
`.editorconfig`): a leading `**/` (i.e. `**/*{Test,Tests}/**.cs`) looks more "correct" but silently fails to match
a project folder sitting directly under the repo root (one level deep, e.g. `UnitTestingCookbook.Tests/Foo.cs`) -
Roslyn's glob engine requires `**/` to consume at least one path segment, so it can't match "zero directories."
Verified empirically (see the comment above that section in `.editorconfig`) against both a depth-1 and a
deeper-nested test folder before landing on the current form.

### Conventions specific to this repo

- **Test methods are prefixed `A_`, `B_`, `C_`, ...** so their alphabetical/declaration order matches the order
  the corresponding README walks through them. When adding a new example to an existing chapter, pick the next
  free letter rather than a descriptive name.
- **No space-inside-parens** (`Should().Be("x")`, not `Should().Be( "x" )`) is the enforced C# style, per
  `.editorconfig`'s `csharp_space_between_*_parentheses = false`. This was a deliberate one-time repo-wide
  reformat; don't reintroduce the old spaced style in new code.
- **AwesomeAssertions, not FluentAssertions.** This repo migrated off FluentAssertions (commercial license as of
  v8) to the AwesomeAssertions community fork. A few FluentAssertions add-on packages have no AwesomeAssertions
  equivalent yet (`FluentAssertions.AspNetCore.Mvc`, `.Reactive`, `.Microsoft.Extensions.DependencyInjection`) -
  see `README_AwesomeAssertionsAddOns.md` for what was dropped vs. rewritten without an add-on.
- Classic NUnit asserts (where a chapter deliberately contrasts them with fluent assertions, e.g.
  `AwesomeAssertionsTests.C_Assert_Vs_AwesomeAssertion_ErrorMessage`) use `NUnit.Framework.Legacy.ClassicAssert`,
  not bare `Assert.AreEqual`/`Assert.IsTrue` - those were removed from `Assert` itself in NUnit 4+.
