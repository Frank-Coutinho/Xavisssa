using System;
using System.IO;

namespace Xavissa.Frontend.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly string _realDbPath = BuildPath("Real", "xavissa.db");
    private string _demoDbPath = BuildPath("Demo", "xavissa_demo.db");

    public WorkspaceKind Current { get; private set; } = WorkspaceKind.Real;

    public string RealDbPath => _realDbPath;
    public string DemoDbPath => _demoDbPath;
    public string CurrentDbPath => Current == WorkspaceKind.Demo ? DemoDbPath : RealDbPath;

    public void UseRealWorkspace()
    {
        Current = WorkspaceKind.Real;
        Directory.CreateDirectory(Path.GetDirectoryName(RealDbPath)!);
    }

    public void UseDemoWorkspace()
    {
        Current = WorkspaceKind.Demo;
        Directory.CreateDirectory(Path.GetDirectoryName(DemoDbPath)!);
    }

    public void ResetDemoWorkspace()
    {
        UseDemoWorkspace();
        if (TryDeleteSqliteFiles(DemoDbPath))
            return;

        _demoDbPath = BuildPath("Demo", $"xavissa_demo_{DateTime.UtcNow:yyyyMMddHHmmssfff}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_demoDbPath)!);
        TryCleanupStaleDemoFiles();
    }

    private static bool TryDeleteSqliteFiles(string dbPath)
    {
        try
        {
            DeleteIfExists(dbPath);
            DeleteIfExists(dbPath + "-wal");
            DeleteIfExists(dbPath + "-shm");
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void TryCleanupStaleDemoFiles()
    {
        var demoFolder = Path.GetDirectoryName(_demoDbPath);
        if (string.IsNullOrWhiteSpace(demoFolder) || !Directory.Exists(demoFolder))
            return;

        foreach (var path in Directory.EnumerateFiles(demoFolder, "xavissa_demo*.db*"))
        {
            if (string.Equals(path, _demoDbPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, _demoDbPath + "-wal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, _demoDbPath + "-shm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TryDeleteSqliteFiles(path);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string BuildPath(string workspace, string fileName)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Xavissa",
            "Workspaces",
            workspace);
        Directory.CreateDirectory(root);
        return Path.Combine(root, fileName);
    }
}
