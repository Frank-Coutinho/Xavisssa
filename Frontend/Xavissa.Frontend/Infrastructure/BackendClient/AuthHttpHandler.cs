using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;

public class AuthHttpHandler : DelegatingHandler
{
    private readonly IApiTokenProvider _tokens;

    public AuthHttpHandler(IApiTokenProvider tokens)
    {
        _tokens = tokens;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var token = _tokens.Token;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
