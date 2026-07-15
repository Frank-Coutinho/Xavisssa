namespace Xavissa.Frontend.Services;

public interface IOnlineSessionCredentialCache
{
    bool HasCredentials { get; }
    string Username { get; }
    string Password { get; }
    void Set(string username, string password);
    void Clear();
}

public sealed class OnlineSessionCredentialCache : IOnlineSessionCredentialCache
{
    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;

    public void Set(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public void Clear()
    {
        Username = string.Empty;
        Password = string.Empty;
    }
}
