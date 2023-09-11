using System.Net;

namespace UnitTestingCookbook.Support.Services;

public interface IHttpBinOrgService
{
    Task<HttpStatusCode> GetStatusAsync( HttpStatusCode status, CancellationToken cancellationToken = default );
}
