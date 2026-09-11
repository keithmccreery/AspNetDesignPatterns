using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AspNetDesignPatterns.Api.Tests.TestSupport;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that answers every request with a fixed status code
/// and body (or throws a supplied exception), so a typed client can be exercised without the
/// network. Records the last request sent and how many were sent, so a test can assert on the
/// URI a client built or that a cache actually prevented a second call.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string? _content;
    private readonly Exception? _throw;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string? content = null)
    {
        _statusCode = statusCode;
        _content = content;
    }

    public StubHttpMessageHandler(Exception toThrow) => _throw = toThrow;

    public HttpRequestMessage? LastRequest { get; private set; }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        CallCount++;

        if (_throw is not null)
        {
            return Task.FromException<HttpResponseMessage>(_throw);
        }

        HttpResponseMessage response = new(_statusCode) { RequestMessage = request };

        if (_content is not null)
        {
            response.Content = new StringContent(_content, Encoding.UTF8);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return Task.FromResult(response);
    }
}
