using System;
using Xavissa.Frontend.ViewModels;

public static class AuthService
{
    public static string Token { get; private set; } = string.Empty;
    public static LoginViewModel.LoginResponse? CurrentUser { get; private set; }

    public static event Action? UserChanged;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(Token) && CurrentUser != null;

    public static void SetCredentials(string token, LoginViewModel.LoginResponse user)
    {
        Token = token;
        CurrentUser = user;

        Console.WriteLine("---- AUTHSERVICE DEBUG ----");
        Console.WriteLine($"Token: {Token}");
        Console.WriteLine($"Stored CurrentUser.username: {CurrentUser?.username}");
        Console.WriteLine($"Stored CurrentUser.role: {CurrentUser?.role}");
        Console.WriteLine("---------------------------");
        UserChanged?.Invoke();
    }

    public static void Clear()
    {
        Token = string.Empty;
        CurrentUser = null;
        UserChanged?.Invoke();
    }
}
