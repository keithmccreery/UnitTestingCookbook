# Unit Testing Cookbook

[![CI](https://github.com/keithmccreery/UnitTestingCookbook/actions/workflows/ci.yml/badge.svg)](https://github.com/keithmccreery/UnitTestingCookbook/actions/workflows/ci.yml)

...with an introduction to **AwesomeAssertions**.  

**Why AwesomeAssertions and not FluentAssertions?** FluentAssertions moved to a commercial license starting with
v8. AwesomeAssertions is a community-maintained fork that kept the original Apache 2.0 license and has committed
to keeping it - see the [AwesomeAssertions GitHub repo](https://github.com/AwesomeAssertions/AwesomeAssertions)
for details.

## Why

- I don't like being woken up at 2:00 AM with a production issue.

## What this is...

- An introduction to AwesomeAssertions.
- A **Cookbook** of Unit Test patterns / examples.
- An introduction to several useful NuGet packages.
- Examples that range from beginner to expert.
- Including some examples that would be considered **Integration Tests**.

## What this is not...

- Theory of (Unit/Integration) Testing.
- Why should I (Unit/Integration) Test.
- Discussion on minutia/definitions (e.g. Mock vs Fake vs Stub).
    - for this presentation, the term **Mock** will be used in the general sense.
- Complete - If you have examples or corrections, please share.

## Goals

- Hopefully, everyone learns something new or a new way of implementing a (Unit/Integration) Test.

## Assumptions

- There are many Testing Frameworks. This presentation will use NUnit.
- Some of the Frameworks (e.g. XUnit) may have functionality built-in that is being presented here.

## Caveat Emptor

- These examples and samples are what I have accumulated over the years.
They may not resonate with everyone.  
- There are a couple of examples where one might say "don't do that". Please be patient,
and the explanation will follow.  

---

# Sub-Section README

- [AwesomeAssertions](./README/README_AwesomeAssertions.md)
    - [AwesomeAssertionsTests.cs](./UnitTestingCookbook.Tests/AwesomeAssertionsTests.cs)
- [Data Hangover](./README/README_DataHangover.md)
    - [DataHangoverFailureTests.cs](./UnitTestingCookbook.Tests/DataHangoverFailureTests.cs)
    - [DataHangoverRighteousTests.cs](./UnitTestingCookbook.Tests/DataHangoverRighteousTests.cs)
- [Mocking](./README/README_Mocking.md)
    - [MockingTests.cs](./UnitTestingCookbook.Tests/MockingTests.cs)
- [Data Driven](./README/README_DataDriven.md)
    - [DataDrivenTests.cs](./UnitTestingCookbook.Tests/DataDrivenTests.cs)
- [AwesomeAssertions Add-Ons](./README/README_AwesomeAssertionsAddOns.md)
    - [AwesomeAssertionsAddOnsTests.cs](./UnitTestingCookbook.Tests/AwesomeAssertionsAddOnsTests.cs)
- [Dependency Injection](./README/README_DependencyInjection.md)
    - [DependencyInjectionTests.cs](./UnitTestingCookbook.Tests/DependencyInjectionTests.cs)
- [System.IO Abstraction](./README/README_SystemIoAbstraction.md)
    - [SystemIoAbstractionsTests.cs](./UnitTestingCookbook.Tests/SystemIoAbstractionsTests.cs)
- [Docker](./README/README_Docker.md)
    - [DockerTests.cs](./UnitTestingCookbook.Tests/DockerTests.cs)
- [Logging](./README/README_Logging.md)
    - [LoggingTests.cs](./UnitTestingCookbook.Tests/LoggingTests.cs)
- [HttpClientFactory](./README/README_HttpClientFactory.md)
    - [HttpClientFactoryTests.cs](./UnitTestingCookbook.Tests/HttpClientFactoryTests.cs)
- [WireMock.NET](./README/README_WireMockNet.md)
    - [WireMockNetTests.cs](./UnitTestingCookbook.Tests/WireMockNetTests.cs)
- [Polly Policies](./README/README_WireMockNetPollyPolicies.md)
    - [WireMockNetPollyPoliciesTests.cs](./UnitTestingCookbook.Tests/WireMockNetPollyPoliciesTests.cs)
- [Analyzers](./README/README_Analyzers.md)
    - [AnalyzersTests.cs](./UnitTestingCookbook.Tests/AnalyzersTests.cs)
- [General Tips](./README/README_GeneralTips.md)
    - [GeneralTipsTests.cs](./UnitTestingCookbook.Tests/GeneralTipsTests.cs)
- [Bogus](./README/README_Bogus.md)
    - [BogusTests.cs](./UnitTestingCookbook.Tests/BogusTests.cs)
- [MinimalApi Integration Testing](./README/README_MinimalApi.md)
    - [MinimalApiTests.cs](./UnitTestingCookbook.Tests/MinimalApiTests.cs)
    - [UnitTestingCookbook.MinimalApi](./UnitTestingCookbook.MinimalApi)
- [NetArchTest](./README/README_NetArchTest.md)
    - [NetArchTestTests.cs](./UnitTestingCookbook.Tests/NetArchTestTests.cs)
- [AppSettings Validation](./README/README_AppSettingsValidation.md)
    - No NUnit tests - see the README (code lives in [UnitTestingCookbook.MinimalApi](./UnitTestingCookbook.MinimalApi))
- [Connection String Validation](./README/README_ConnectionStringValidation.md)
    - [ConnectionStringValidationTests.cs](./UnitTestingCookbook.Tests/ConnectionStringValidationTests.cs)
- [Singleton HttpClient](./README/README_SingletonHttpClient.md)
    - [SingletonHttpClientTests.cs](./UnitTestingCookbook.Tests/SingletonHttpClientTests.cs)
- [Preventing HTTP Requests to External Services in Unit Tests](./README/README_PreventHttpRequests.md)
    - [PreventHttpRequestsTests.cs](./UnitTestingCookbook.Tests/PreventHttpRequestsTests.cs)
- [TimeProvider](./README/README_TimeProvider.md)
    - [TimeProviderTests.cs](./UnitTestingCookbook.Tests/TimeProviderTests.cs)
- [Testing IHostedService / BackgroundService](./README/README_HostedService.md)
    - [HostedServiceTests.cs](./UnitTestingCookbook.Tests/HostedServiceTests.cs)
- [Entity Framework Core](./README/README_EntityFrameworkCore.md)
    - [EntityFrameworkCoreTests.cs](./UnitTestingCookbook.Tests/EntityFrameworkCoreTests.cs)
- [Parallel Processing](./README/README_ParallelProcessing.md)
    - [ParallelProcessingSafeTests.cs](./UnitTestingCookbook.Tests/ParallelProcessingSafeTests.cs)
    - [ParallelProcessingUnsafeTests.cs](./UnitTestingCookbook.Tests/ParallelProcessingUnsafeTests.cs)

# Errata

- NUnit vs. XUnit vs. MSTest: Comparing Unit Testing Frameworks In C# https://www.lambdatest.com/blog/nunit-vs-xunit-vs-mstest/

# Future

- Snapshot Testing (Verify/Verify.NUnit) - deferred: the package requires either a paid sponsorship or an
  explicit, time-bounded open-source exemption declaration to build (its "SponsorCheck" mechanism) - still
  deciding how to handle that before adding the chapter.
- Create Relative Code Snippet links in GitHub
