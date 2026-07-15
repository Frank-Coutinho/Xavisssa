namespace Xavissa.Frontend.Auth.Common
{
    public class ApiTokenProvider : IApiTokenProvider
    {
        public string? Token { get; private set; }

        public void SetToken(string token)
        {
            Token = token;
        }

        public void Clear()
        {
            Token = null;
        }
    }
}
