using System.Net;

using Flurl; // https://github.com/tmenier/Flurl

using Microsoft.Extensions.Logging;

namespace UnitTestingCookbook.Support.Services;

public class HttpBinOrgService : IHttpBinOrgService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<HttpBinOrgService> logger;

    public HttpBinOrgService(IHttpClientFactory httpClientFactory, ILogger<HttpBinOrgService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<HttpStatusCode> GetStatusAsync(HttpStatusCode status, CancellationToken cancellationToken = default)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("HttpBinOrg"); // short-lived

        Uri uri = new Uri("status", UriKind.Relative) // endpoint has no leading slash or trailing slash
            .AppendPathSegment((int) status) // flurl
            .ToUri();

        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.AddPollyContext(logger);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return response.StatusCode;
    }
}
