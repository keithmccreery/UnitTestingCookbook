# Unit Testing Cookbook

...with an introduction to **AwesomeAssertions**.  

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
    - [AwesomeAssertionsTest.cs](./UnitTestingCookbook.Test/AwesomeAssertionsTest.cs)
- [Data Driven](./README/README_DataDriven.md)
    - [DataDrivenTest.cs](./UnitTestingCookbook.Test/DataDrivenTest.cs)
- [AwesomeAssertions Add-Ons](./README/README_AwesomeAssertionsAddOns.md)
    - [AwesomeAssertionsAddOnsTest.cs](./UnitTestingCookbook.Test/AwesomeAssertionsAddOnsTest.cs)
- [Dependency Injection](./README/README_DependencyInjection.md)
    - [DependencyInjectionTest.cs](./UnitTestingCookbook.Test/DependencyInjectionTest.cs)
- [System.IO Abstraction](./README/README_SystemIoAbstraction.md)
    - [SystemIoAbstractionsTest.cs](./UnitTestingCookbook.Test/SystemIoAbstractionsTest.cs)
- [Docker](./README/README_Docker.md)
    - [DockerTest.cs](./UnitTestingCookbook.Test/DockerTest.cs)
- [Logging](./README/README_Logging.md)
    - [LoggingTest.cs](./UnitTestingCookbook.Test/LoggingTest.cs)
- [HttpClientFactory](./README/README_HttpClientFactory.md)
    - [HttpClientFactoryTest.cs](./UnitTestingCookbook.Test/HttpClientFactoryTest.cs)
- [WireMock.NET](./README/README_WireMockNet.md)
    - [WireMockNetTest.cs](./UnitTestingCookbook.Test/WireMockNetTest.cs)
- [Polly Policies](./README/README_WireMockNetPollyPolicies.md)
    - [WireMockNetPollyPoliciesTest.cs](./UnitTestingCookbook.Test/WireMockNetPollyPoliciesTest.cs)
- [Data Hangover](./README/README_DataHangover.md)
    - [DataHangoverFailureTest.cs](./UnitTestingCookbook.Test/DataHangoverFailureTest.cs)
    - [DataHangoverRighteousTest.cs](./UnitTestingCookbook.Test/DataHangoverRighteousTest.cs)
- [Analyzers](./README/README_Analyzers.md)
    - [AnalyzersTest.cs](./UnitTestingCookbook.Test/AnalyzersTest.cs)
- [General Tips](./README/README_GeneralTips.md)
    - [GeneralTipsTest.cs](./UnitTestingCookbook.Test/GeneralTipsTest.cs)
- [Bogus](./README/README_Bogus.md)
    - [BogusTest.cs](./UnitTestingCookbook.Test/BogusTest.cs)
- [MinimalApi Integration Testing](./README/README_MinimalApi.md)
    - [MinimalApiTest.cs](./UnitTestingCookbook.Test/MinimalApiTest.cs)
    - [UnitTestingCookbook.MinimalApi](./UnitTestingCookbook.MinimalApi)

# Errata

- NUnit vs. XUnit vs. MSTest: Comparing Unit Testing Frameworks In C# https://www.lambdatest.com/blog/nunit-vs-xunit-vs-mstest/

# Future

- Parallel Processing
- Moq Examples
- AWS via LocalStack
- Microsoft TestServer
- More Extension Methods / Helper Classes for TestCorrelator and WireMock.Net Objects
- Create Relative Code Snippet links in GitHub
- More Architecture / Policy / Standards Tests using https://github.com/BenMorris/NetArchTest
- Connection String Validation using https://khalidabuhakmeh.com/validating-connection-strings-on-dotnet-startup
- `appsetting.json` Validation examples https://code-maze.com/aspnet-configuration-options-validation/
- Prevent http requests to external services in unit tests https://www.meziantou.net/prevent-http-requests-to-external-services-in-unit-tests.htm
- Add WireMockInspector Examples https://github.com/WireMock-Net/WireMockInspector
- Test for HttpClient consumed by a Singleton, using `SocketsHttpHandler` via `.ConfigurePrimaryHttpMessageHandler()`, setting `PooledConnectionLifetime`, asserted directly against `IServiceCollection` (see [AwesomeAssertions Add-Ons](./README/README_AwesomeAssertionsAddOns.md) - there is no AwesomeAssertions equivalent of `FluentAssertions.Microsoft.Extensions.DependencyInjection`) and `GetPrivateField()`.
