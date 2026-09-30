namespace UnitTestingCookbook.TestHelpers;

/// <summary>
/// Extension for walking an <see cref="HttpClient"/>'s internal handler chain - reaching configuration (e.g. a
/// primary handler set via <c>IHttpClientBuilder.ConfigurePrimaryHttpMessageHandler()</c>) that has no public
/// API to read back out of a built client.
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
        /// <typeparam name="THandler">The specific <see cref="HttpMessageHandler"/> type to find in the chain (e.g. <see cref="SocketsHttpHandler"/>).</typeparam>
        /// <returns>The first handler in the chain assignable to <typeparamref name="THandler"/>.</returns>
        /// <exception cref="MissingMemberException">No handler assignable to <typeparamref name="THandler"/> was found anywhere in the chain.</exception>
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
