using System.Diagnostics;
using System.IO;

namespace SecureMcpDesktopDemo.Services;

/// <summary>
/// Manages starting and stopping the SecureMcpServer as a child process.
/// This gives the desktop app a true "end-to-end" experience:
/// one click starts the secure server + runs the full UI-driven demo against it.
/// 
/// The launcher captures stdout/stderr so the UI can display server-side logs
/// (auth events, tool calls, errors) alongside client-side protocol logs.
/// </summary>
public sealed class ServerLauncher : IDisposable
{
    private Process? _process;
    private readonly string _solutionRoot;
    private readonly Action<string> _onLogLine;

    public bool IsRunning => _process is { HasExited: false };

    public ServerLauncher(string solutionRoot, Action<string> onLogLine)
    {
        _solutionRoot = solutionRoot;
        _onLogLine = onLogLine;
    }

    /// <summary>
    /// Starts the MCP server on the conventional demo port https://localhost:5001.
    /// Uses --no-launch-profile and explicit ASPNETCORE_URLS so it works reliably
    /// from inside the WPF demo without depending on launchSettings.json.
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (IsRunning)
            return true;

        string serverProjectPath = Path.Combine(_solutionRoot, "src", "SecureMcpServer", "SecureMcpServer.csproj");

        if (!File.Exists(serverProjectPath))
        {
            _onLogLine($"[Launcher] ERROR: Could not find server project at {serverProjectPath}");
            return false;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{serverProjectPath}\" --no-launch-profile",
            WorkingDirectory = _solutionRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["ASPNETCORE_URLS"] = "https://localhost:5001",
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            }
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _onLogLine($"[Server] {e.Data}");
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _onLogLine($"[Server][err] {e.Data}");
        };

        _process.Exited += (_, _) =>
        {
            _onLogLine("[Launcher] MCP server process exited.");
        };

        try
        {
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _onLogLine("[Launcher] Starting SecureMcpServer on https://localhost:5001 ...");
            _onLogLine("[Launcher] Waiting a moment for Kestrel to bind...");

            // Give the server a few seconds to start listening
            await Task.Delay(2500, ct);
            return true;
        }
        catch (Exception ex)
        {
            _onLogLine($"[Launcher] Failed to start server: {ex.Message}");
            _process = null;
            return false;
        }
    }

    public void Stop()
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _onLogLine("[Launcher] Stopping MCP server...");
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _onLogLine($"[Launcher] Error while stopping: {ex.Message}");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
