using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;

namespace Xavissa.Frontend.Services
{
    public class AuthMessageHandler : DelegatingHandler
    {
        private readonly IApiTokenProvider _tokens;
        private readonly IAuthService _auth;

        public AuthMessageHandler(IApiTokenProvider tokens, IAuthService auth)
        {
            _tokens = tokens;
            _auth = auth;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var hasToken = !string.IsNullOrWhiteSpace(_tokens.Token);
            if (hasToken)
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens.Token);
            }

            if (_auth.SelectedStoreId.HasValue)
                request.Headers.TryAddWithoutValidation("X-Store-Id", _auth.SelectedStoreId.Value.ToString());

            return SendAndHandleUnauthorizedAsync(request, hasToken, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendAndHandleUnauthorizedAsync(
            HttpRequestMessage request,
            bool hasToken,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (hasToken && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _tokens.Clear();
                _auth.NotifySessionExpired();
            }

            return response;
        }
    }
}
