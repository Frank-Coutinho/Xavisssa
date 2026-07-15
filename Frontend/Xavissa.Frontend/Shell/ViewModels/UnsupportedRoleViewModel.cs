using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels;

public sealed class UnsupportedRoleViewModel : ViewModelBase
{
    private readonly IAuthService _auth;

    public UnsupportedRoleViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    public string Title => "Desktop shell unavailable";
    public string Message => $"The {_auth.ActingRole} role is not supported in the Xavissa desktop client yet.";
}
