namespace Xavissa.Frontend.Auth.Common
{
    public interface IApiTokenProvider
    {
        string? Token { get; }
        void SetToken(string token);
        void Clear();
    }
}
