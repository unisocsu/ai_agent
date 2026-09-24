using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Reflection;
using System.Text.Json;
using WindowsAIAgent.AI;
using WindowsAIAgent.Browser;

namespace WindowsAIAgent;

public sealed class MainWindow : Form
{
    private readonly WebView2 webView = new();
    private readonly BrowserService browser = new();
    private readonly Agent.AgentEngine agent;

    public MainWindow()
    {
        agent = new Agent.AgentEngine(browser);
        Text = "Windows AI Agent";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        webView.Dock = DockStyle.Fill;
        Controls.Add(webView);
        Load += MainWindow_Load;
    }

    private async void MainWindow_Load(object? sender, EventArgs e)
    {
        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsAIAgent", "WebView2");

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    ShowError($"WebView2 failed to load the interface: {args.WebErrorStatus}");
            };

            await LoadEmbeddedUiAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.ToString());
        }
    }

    private async Task LoadEmbeddedUiAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string Read(string suffix)
        {
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (name is null)
                throw new FileNotFoundException($"Embedded UI resource not found: {suffix}");
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var html = Read(".Web.index.html");
        var css = Read(".Web.style.css");
        var js = Read(".Web.app.js");

        html = html.Replace(
            "<link rel="stylesheet" href="style.css">",
            $"<style>{css}</style>",
            StringComparison.OrdinalIgnoreCase);

        html = html.Replace(
            "<script src="app.js"></script>",
            $"<script>{js}</script>",
            StringComparison.OrdinalIgnoreCase);

        webView.NavigateToString(html);
        await Task.CompletedTask;
    }

    private void ShowError(string message)
    {
        if (webView.IsDisposed) return;
        var safe = System.Net.WebUtility.HtmlEncode(message);
        webView.NavigateToString($@"<!doctype html><html><body style='font-family:Segoe UI;padding:40px'><h2>Windows AI Agent</h2><pre style='white-space:pre-wrap'>{safe}</pre></body></html>");
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WebRequest>(e.WebMessageAsJson);
            if (request is null) return;

            if (request.Action == "chat")
            {
                var provider = ProviderFactory.Create(request.Provider ?? "gemini", request.ApiKey ?? "", request.Model);
                var result = await agent.RunAsync(provider, request.Message ?? "");
                webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id = request.Id, ok = true, result }));
                return;
            }

            webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id = request.Id, ok = false, error = "Unknown action" }));
        }
        catch (Exception ex)
        {
            webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
        }
    }

    private sealed record WebRequest(string? Id, string? Action, string? Message, string? Provider, string? ApiKey, string? Model);
}

internal static class ProviderFactory
{
    public static IAIProvider Create(string provider, string apiKey, string? model)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("API key is required.");
        return provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIProvider(apiKey, string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model),
            "grok" => new GrokProvider(apiKey, string.IsNullOrWhiteSpace(model) ? "grok-4-1-fast-reasoning" : model),
            _ => new GeminiProvider(apiKey, string.IsNullOrWhiteSpace(model) ? "gemini-2.5-flash" : model)
        };
    }
}