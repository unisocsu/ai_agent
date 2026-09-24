using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
using WindowsAIAgent.AI;

namespace WindowsAIAgent;

public sealed class MainWindow : Form
{
    private readonly WebView2 webView = new();
    private readonly Agent.AgentEngine agent = new();

    public MainWindow()
    {
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
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsAIAgent", "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await webView.EnsureCoreWebView2Async(env);
        webView.CoreWebView2.WebMessageReceived += WebMessageReceived;
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping("agent.local", Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.Allow);
        webView.Source = new Uri("https://agent.local/index.html");
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WebRequest>(e.WebMessageAsJson());
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