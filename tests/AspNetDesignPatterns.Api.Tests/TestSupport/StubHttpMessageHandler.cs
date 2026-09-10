using System.Net;

namespace AspNetDesignPatterns.Api.Tests.TestSupport;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that answers every request with a fixed status code
/// (or throws a supplied exception), so a typed client can be exercised without the network.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly Exception? _throw;

    public StubHttpMessageHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

    public StubHttpMessageHandler(Exception toThrow) => _throw = toThrow;

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_throw is not null)
        {
            return Task.FromException<HttpResponseMessage>(_throw);
        }

        return Task.FromResult(new HttpResponseMessage(_statusCode) { RequestMessage = request });
    }
}
