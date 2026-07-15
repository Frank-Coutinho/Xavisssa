namespace Xavissa.Frontend.ViewModels;

public sealed class NoWorkspaceViewModel : ViewModelBase
{
    public string Title => "No assigned workspace";
    public string Message => "This account has no tenant or store assignment for the desktop client.";
}
