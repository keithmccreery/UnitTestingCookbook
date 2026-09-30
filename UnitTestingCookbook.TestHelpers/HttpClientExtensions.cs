namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// HttpClientExtensions
/// </summary>
public static class HttpClientExtensions
{
    extension(HttpClient httpClient)
    {
        /// <summary>
        /// Walks the handler chain built into an HttpClient - starting from HttpMessageInvoker's private
        /// _handler field (see <see cref="ReflectionExtensions.GetFieldValue{T}"/>), then each
        /// DelegatingHandler.InnerHandler - to find the first handler assignable to THandler. There is no
        /// public API to get a configured primary handler (e.g. one set via
        /// IHttpClientBuilder.ConfigurePrimaryHttpMessageHandler()) back out of a built HttpClient.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <returns></returns>
        /// <exception cref="MissingMemberException"></exception>
        public THandler GetPrimaryHttpMessageHandler<THandler>() where THandler : HttpMessageHandler
        {
            HttpMessageHandler? current = httpClient.GetFieldValue<HttpMessageHandler>("_handler");

            while (current is not null)
            {
                if (current is THandler match)
                    return match;

                if (current is not DelegatingHandler delegatingHandler)
                    break;

                current = delegatingHandler.InnerHandler;
            }

            throw new MissingMemberException(nameof(HttpClient), typeof(THandler).Name);
        }
    }
}
