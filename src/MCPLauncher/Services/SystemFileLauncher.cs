using System.Diagnostics;

namespace MCPLauncher.Services;

public sealed class SystemFileLauncher : IFileLauncher
{
    public void Open(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
