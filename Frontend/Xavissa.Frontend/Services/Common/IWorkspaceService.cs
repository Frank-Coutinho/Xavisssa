namespace Xavissa.Frontend.Services;

public enum WorkspaceKind
{
    Real,
    Demo,
}

public interface IWorkspaceService
{
    WorkspaceKind Current { get; }
    string CurrentDbPath { get; }
    string RealDbPath { get; }
    string DemoDbPath { get; }
    void UseRealWorkspace();
    void UseDemoWorkspace();
    void ResetDemoWorkspace();
}
