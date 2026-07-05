using System.Net;
using System.Net.Http;

namespace TuneMaster.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage>? Handler { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = Handler?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NotFound);
        return Task.FromResult(response);
    }
}
