using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SecureMcpDesktopDemo.Services;

namespace SecureMcpDesktopDemo;

/// <summary>
/// Main window for the Secure C# MCP Agent End-to-End Desktop Demo.
/// 
/// This single WPF application demonstrates every major concept from the original project:
/// - Starting the secure ASP.NET Core MCP server (HTTPS + SSE + JWT)
/// - Acquiring demo tokens via /auth/token with correct/incorrect secret
/// - Scope enforcement (standard token gets 403 on approve_release)
/// - The four tools and the exact review flow (status → deps → vulns → optional approve)
/// - Local deterministic intent parsing (agent "planner")
/// - Live tool traces, protocol logging, and server-side audit output
/// - Manual experimentation mode
/// 
/// Everything runs from native C# UI against the real secure MCP server.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Core services
    private McpDesktopClient? _mcpClient;
    private readonly ReleaseIntentParser _parser = new();
    private DesktopReviewOrchestrator? _orchestrator;
    private ServerLauncher? _serverLauncher;

    private string? _currentToken;
    private bool _isPrivilegedToken;

    // Live collections for UI binding
    public ObservableCollection<TraceEntry> ToolTraces { get; } = new();

    // Simple INotify for the traces collection (ItemsControl is already bound in XAML)
    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        // Reasonable default for solution root (works when running from repo root or bin folder)
        SolutionRootBox.Text = Environment.CurrentDirectory.Contains("bin", StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, "..", "..", "..", ".."))
            : Environment.CurrentDirectory;

        LogProtocol("Desktop demo initialized. Start the MCP server, then acquire a token or run a scenario.");
        UpdateServerStatus("Stopped", "#FCD34D");
    }

    private void UpdateServerStatus(string text, string colorHex)
    {
        ServerStatusText.Text = text;
        ServerStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
    }

    // ==================== SERVER LIFECYCLE ====================

    private async void StartServer_Click(object sender, RoutedEventArgs e)
    {
        if (_serverLauncher != null && _serverLauncher.IsRunning)
        {
            LogProtocol("Server is already running.");
            return;
        }

        string root = SolutionRootBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(root) || !System.IO.Directory.Exists(root))
        {
            MessageBox.Show("Please enter a valid path to the solution root (folder containing the .sln).", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _serverLauncher = new ServerLauncher(root, LogServer);
        bool started = await _serverLauncher.StartAsync();

        if (started)
        {
            UpdateServerStatus("Running", "#4ADE80");
            LogProtocol("MCP server reported as started. Waiting for Kestrel to accept connections on :5001...");
            await Task.Delay(800);

            // (Re)create the client pointed at the server
            RecreateMcpClient();
        }
        else
        {
            UpdateServerStatus("Failed to start", "#F87171");
        }
    }

    private void StopServer_Click(object sender, RoutedEventArgs e)
    {
        _serverLauncher?.Stop();
        UpdateServerStatus("Stopped", "#FCD34D");
        LogProtocol("Server stop requested.");
    }

    private async void TrustDevCert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogProtocol("Running: dotnet dev-certs https --trust  (this may prompt for elevation)");
            var psi = new ProcessStartInfo("dotnet", "dev-certs https --trust")
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            await Task.Delay(1500);
            LogProtocol("Dev cert trust command launched. You may need to restart the MCP server after trusting.");
        }
        catch (Exception ex)
        {
            LogProtocol($"Could not launch dev-certs trust: {ex.Message}");
            MessageBox.Show("Failed to launch 'dotnet dev-certs https --trust'. Run it manually in an elevated prompt if you see certificate errors.", "Dev Cert", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RecreateMcpClient()
    {
        _mcpClient?.Dispose();
        _mcpClient = new McpDesktopClient("https://localhost:5001", LogProtocol);
        _orchestrator = new DesktopReviewOrchestrator(_mcpClient, _parser);
        LogProtocol("MCP client (re)created targeting https://localhost:5001");
    }

    // ==================== SCENARIOS (exact flows from original demo) ====================

    private async void Scenario_Ready_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Text = "Can we release AnalyzerService version 2.4.1?";
        PrivilegedCheck.IsChecked = false;
        await RunFullReviewInternal();
    }

    private async void Scenario_Approve_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Text = "Approve AnalyzerService version 2.4.1 for production release.";
        PrivilegedCheck.IsChecked = true;
        await RunFullReviewInternal();
    }

    private async void Scenario_Blocked_Click(object sender, RoutedEventArgs e)
    {
        QueryBox.Text = "Can we release PaymentGateway version 5.8.0?";
        PrivilegedCheck.IsChecked = false; // blocked anyway due to vuln
        await RunFullReviewInternal();
    }

    private async void RunFullReview_Click(object sender, RoutedEventArgs e)
    {
        await RunFullReviewInternal();
    }

    private async Task RunFullReviewInternal()
    {
        if (!EnsureClientReady()) return;

        string query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            query = "Can we release AnalyzerService version 2.4.1?";
            QueryBox.Text = query;
        }

        bool privileged = PrivilegedCheck.IsChecked == true;

        ClearResultsForNewRun();

        LogProtocol($"=== Starting full review (privileged={privileged}) ===");
        LogProtocol($"Query: {query}");

        try
        {
            var result = await _orchestrator!.RunReviewAsync(
                query,
                privileged,
                trace => Dispatcher.Invoke(() => AddTraceEntry(trace)),
                msg => Dispatcher.Invoke(() => LogProtocol(msg))
            );

            // Update UI with final result
            Dispatcher.Invoke(() =>
            {
                UpdateIntent(result.Intent);
                UpdateVerdict(result.Verdict, result.Summary, result.Recommendation);

                // Show the last raw response if any
                if (result.ToolCalls.Count > 0)
                {
                    RawResponseBox.Text = result.ToolCalls.Last().Raw;
                }

                LogProtocol($"=== Review complete. Verdict = {result.Verdict} ===");
            });
        }
        catch (Exception ex)
        {
            LogProtocol($"ERROR during review: {ex.Message}");
            MessageBox.Show($"Review failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RunStepByStep_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureClientReady()) return;

        string query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) query = "Can we release AnalyzerService version 2.4.1?";

        bool privileged = PrivilegedCheck.IsChecked == true;

        ClearResultsForNewRun();
        LogProtocol("=== STEP-BY-STEP MODE ===");

        // 1. Parse
        var intent = _parser.Parse(query);
        UpdateIntent(intent);
        LogProtocol($"Parsed: {intent.Component} v{intent.Version}");

        // 2. Get token
        var tokenRes = await _mcpClient!.GetTokenAsync(privileged);
        if (tokenRes.AccessToken == null)
        {
            LogProtocol($"Token acquisition failed (HTTP {tokenRes.HttpStatus})");
            return;
        }
        SetCurrentToken(tokenRes.AccessToken, privileged);
        LogProtocol("Token acquired successfully.");

        string token = tokenRes.AccessToken;

        // 3. The three info tools
        foreach (var tool in new[] { "get_release_status", "get_dependencies", "check_security_vulnerabilities" })
        {
            var trace = await _mcpClient.CallToolAsync(token, tool, intent.Component, intent.Version);
            AddTraceEntry(trace);
            RawResponseBox.Text = trace.Raw;
            await Task.Delay(250);
        }

        // 4. Always show the approve attempt (the key security demo)
        var approveTrace = await _mcpClient.CallToolAsync(token, "approve_release", intent.Component, intent.Version);
        AddTraceEntry(approveTrace);
        RawResponseBox.Text = approveTrace.Raw;

        // Compute quick verdict for the box
        var status = GetStringFromTrace(approveTrace, "status") ?? "unknown"; // not accurate, but illustrative
        // Better: recompute using same logic the orchestrator uses
        LogProtocol("Step-by-step complete. Examine the trace and 403 (if any) above.");
    }

    // ==================== TOKEN HANDLING ====================

    private async void AcquireStandardToken_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureClientReady()) return;
        await AcquireTokenInternal(false);
    }

    private async void AcquirePrivilegedToken_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureClientReady()) return;
        await AcquireTokenInternal(true);
    }

    private async Task AcquireTokenInternal(bool privileged)
    {
        var result = await _mcpClient!.GetTokenAsync(privileged);
        if (result.AccessToken != null)
        {
            SetCurrentToken(result.AccessToken, privileged);
            LogProtocol($"Acquired {(privileged ? "PRIVILEGED" : "STANDARD")} token successfully.");
        }
        else
        {
            LogProtocol($"Token request FAILED: HTTP {result.HttpStatus} — {result.RawResponse}");
            MessageBox.Show($"Failed to get token.\n{result.RawResponse}", "Token Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetCurrentToken(string token, bool privileged)
    {
        _currentToken = token;
        _isPrivilegedToken = privileged;

        string display = token.Length > 80 ? token[..40] + "..." + token[^30..] : token;
        CurrentTokenText.Text = display;
        CurrentScopesText.Text = privileged
            ? "Scopes: mcp:tools, mcp:tools:release  (privileged demo secret used)"
            : "Scopes: mcp:tools  (standard — approve_release will be rejected)";

        RecreateMcpClient(); // keep a fresh client with latest token semantics if needed
    }

    private void DecodeToken_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentToken))
        {
            MessageBox.Show("Acquire a token first.", "No Token", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var parts = _currentToken.Split('.');
            if (parts.Length < 2) throw new Exception("Invalid JWT format");

            string payload = parts[1];
            // Pad base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            byte[] bytes = Convert.FromBase64String(payload);
            string json = Encoding.UTF8.GetString(bytes);

            var pretty = JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, new JsonSerializerOptions { WriteIndented = true });

            MessageBox.Show($"Decoded JWT payload (claims):\n\n{pretty}\n\n" +
                            "Note: The server validates these claims on every request. The 'scope' array is what drives authorization.",
                            "JWT Claims (demo only)", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not decode token: {ex.Message}", "Decode Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== MANUAL TOOL CALLS ====================

    private async void ManualToolCall_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureClientReady() || string.IsNullOrEmpty(_currentToken))
        {
            MessageBox.Show("Acquire a token first (use the buttons on the left).", "No Token", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string tool = ((ComboBoxItem)ManualToolCombo.SelectedItem).Content.ToString()!;
        string component = ManualComponentBox.Text.Trim();
        string version = ManualVersionBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(component) || string.IsNullOrWhiteSpace(version))
        {
            MessageBox.Show("Enter component and version.", "Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LogProtocol($"Manual call: {tool} {component}@{version}");

        var trace = await _mcpClient!.CallToolAsync(_currentToken, tool, component, version);
        AddTraceEntry(trace);
        RawResponseBox.Text = trace.Raw;

        if (trace.HttpStatus == 403)
        {
            LogProtocol("403 Forbidden returned by server — this is the authorization boundary being demonstrated.");
        }
    }

    // ==================== HELPERS & UI UPDATES ====================

    private bool EnsureClientReady()
    {
        if (_mcpClient == null || _orchestrator == null)
        {
            // Auto-create if server is presumably running
            RecreateMcpClient();
            LogProtocol("Client was not ready — created a new one. Make sure the server is running on :5001.");
        }
        return true;
    }

    private void ClearResultsForNewRun()
    {
        ToolTraces.Clear();
        VerdictText.Text = "Running review...";
        VerdictBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2937"));
        SummaryText.Text = "";
        RecommendationText.Text = "";
        IntentComponent.Text = "-";
        IntentVersion.Text = "-";
        IntentSource.Text = "Source: -";
        IntentConfidence.Text = "Confidence: -";
        RawResponseBox.Text = "";
    }

    private void AddTraceEntry(McpDesktopClient.ToolCallResult trace)
    {
        string summary = trace.Status == "ok"
            ? (trace.Result?.ToString() ?? trace.Raw[..Math.Min(180, trace.Raw.Length)])
            : (trace.Error?.ToString() ?? trace.Raw);

        var color = trace.HttpStatus switch
        {
            200 or 201 => "#4ADE80",
            403 => "#F87171",
            >= 400 => "#FB923C",
            _ => "#94A3B8"
        };

        var entry = new TraceEntry
        {
            Tool = trace.Tool,
            HttpStatusLabel = $"HTTP {trace.HttpStatus}  ({trace.Status})",
            StatusColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            Summary = summary.Length > 260 ? summary[..260] + "..." : summary,
            Raw = trace.Raw
        };

        ToolTraces.Add(entry);

        // Auto-select last raw for convenience
        RawResponseBox.Text = trace.Raw;
    }

    private void UpdateIntent(ReleaseIntent intent)
    {
        IntentComponent.Text = intent.Component;
        IntentVersion.Text = intent.Version;
        IntentSource.Text = $"Source: {intent.Source}";
        IntentConfidence.Text = $"Confidence: {intent.Confidence:P0}";
    }

    private void UpdateVerdict(string verdict, string summary, string recommendation)
    {
        VerdictText.Text = verdict.ToUpperInvariant();

        var (bg, fg) = verdict switch
        {
            "approved" => ("#166534", "#86EFAC"),
            "needs_approval_scope" => ("#854D0E", "#FCD34D"),
            "blocked" => ("#7F1D1D", "#FCA5A5"),
            _ => ("#1E2937", "#E2E8F0")
        };

        VerdictBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        VerdictText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));

        SummaryText.Text = summary;
        RecommendationText.Text = recommendation;
    }

    private static string? GetStringFromTrace(McpDesktopClient.ToolCallResult trace, string prop)
    {
        try
        {
            if (trace.Result is { } el && el.TryGetProperty(prop, out var p))
                return p.GetString();
        }
        catch { }
        return null;
    }

    private void LogProtocol(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        ProtocolLogBox.AppendText(line);
        ProtocolLogBox.ScrollToEnd();
    }

    private void LogServer(string message)
    {
        Dispatcher.Invoke(() =>
        {
            string line = $"{message}{Environment.NewLine}";
            ServerLogBox.AppendText(line);
            ServerLogBox.ScrollToEnd();

            // Also surface interesting server lines into protocol for visibility
            if (message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("403") || message.Contains("401"))
            {
                LogProtocol($"[from server] {message}");
            }
        });
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        ProtocolLogBox.Clear();
        ServerLogBox.Clear();
        RawResponseBox.Clear();
        ToolTraces.Clear();
        LogProtocol("Logs cleared.");
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _serverLauncher?.Stop();
        _mcpClient?.Dispose();
        base.OnClosing(e);
    }

    private void NotifyPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// View model item for the live tool trace list.
/// </summary>
public sealed class TraceEntry
{
    public string Tool { get; set; } = "";
    public string HttpStatusLabel { get; set; } = "";
    public Brush StatusColor { get; set; } = Brushes.Gray;
    public string Summary { get; set; } = "";
    public string Raw { get; set; } = "";
}
