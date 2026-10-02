using System.Net;

using Microsoft.Extensions.DependencyInjection;

using UnitTestingCookbook.Support.Services;

using WireMock.AwesomeAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace UnitTestingCookbook.Tests;

[Category("unit")]
[Category("wiremocknet")]
[TestFixture]
public class WireMockNetTests
{
    private static WireMockServer _wireMockServer;
    private string? _baseUrl;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Structure", "NUnit1032:An IDisposable field/property should be Disposed in a TearDown method", Justification = "IServiceProvider is cast to IDisposable")]
    private IServiceProvider? _serviceProvider;

    const string ENDPOINT_STATUS_OK = "/status/200"; // requires leading slash

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        //
        // WireMock
        //
        _wireMockServer = WireMockServer.Start();
        _baseUrl = _wireMockServer.Urls[0];

        // status endpoint
        _wireMockServer
            .Given(
                Request.Create()
                    .WithPath(ENDPOINT_STATUS_OK)
                    .UsingGet()
            )
            .RespondWith(
                Response.Create()
                    .WithHeader("Authorization", "valid")
                    .WithStatusCode(HttpStatusCode.OK)
            );
    }

    // begin-snippet: WireMockNetTests_OneTimeTearDown
    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _wireMockServer.Stop();
        _wireMockServer.Dispose();
    }
    // end-snippet

    [SetUp]
    // begin-snippet: WireMockNetTests_SetUp
    public void SetUp()
    {
        //
        // Clear Logs
        //
        _wireMockServer.ResetLogEntries();

        //
        // services
        //
        IServiceCollection services = new ServiceCollection();
        services.AddScoped<IHttpBinOrgService, HttpBinOrgService>();
        services.AddHttpClient("HttpBinOrg", client =>
        {
            client.BaseAddress = new Uri(_baseUrl!); // setup capture of URLs
        });

        _serviceProvider = services.BuildServiceProvider(true);
    }
    // end-snippet

    // begin-snippet: WireMockNetTests_TearDown
    [TearDown]
    public void TearDown()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
    // end-snippet

    //
    // Q: 
    //
    [Test]
    [Category("_passes")]
    // begin-snippet: WireMockNetTests_A_WireMockNet
    public async Task A_WireMockNet()
    {
        // Arrange
        IServiceScopeFactory? serviceScopeFactory = _serviceProvider!.GetRequiredService<IServiceScopeFactory>();
        using IServiceScope? scope = serviceScopeFactory.CreateScope();

        IHttpBinOrgService service = scope!.ServiceProvider.GetRequiredService<IHttpBinOrgService>();

        // Act
        await service.GetStatusAsync(HttpStatusCode.OK);

        // Assert
        _wireMockServer.Should()
            .HaveReceivedACall()
            .AtUrl($"{_baseUrl}{ENDPOINT_STATUS_OK}");

        _wireMockServer.LogEntries.Should().ContainSingle()
            .Which
            .ResponseMessage!.Headers!.FirstOrDefault(h => h.Key.Equals("Authorization"))
            .Value.FirstOrDefault().Should().Be("valid");
    }
    // end-snippet
}
