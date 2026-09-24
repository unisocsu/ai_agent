using System.Text.Json;
using WindowsAIAgent.Browser;

namespace WindowsAIAgent.Tools;

public sealed class BrowserTool : IAgentTool
{
    private readonly BrowserService browser;
    public BrowserTool(BrowserService browser) => this.browser = browser;

    public string Name => "browser";
    public string Description => "Control the built-in WebView2 browser. Actions: open(url), navigate(url), back, forward, read, click(selector), type(selector,text), screenshot(path).";
    public object Parameters => new
    {
        type = "object",
        properties = new
        {
            action = new { type = "string", @enum = new[] { "open", "navigate", "back", "forward", "read", "click", "type", "screenshot" } },
            url = new { type = "string" },
            selector = new { type = "string" },
            text = new { type = "string" },
            path = new { type = "string" }
        },
        required = new[] { "action" }
    };

    public async Task<string> ExecuteAsync(JsonElement args, CancellationToken ct = default)
    {
        var action = args.GetProperty("action").GetString()?.ToLowerInvariant();
        switch (action)
        {
            case "open":
            case "navigate":
                await browser.NavigateAsync(args.GetProperty("url").GetString() ?? "", ct);
                return $"Navigated to {browser.CurrentUrl}";
            case "back":
                await browser.BackAsync();
                return $"Back: {browser.CurrentUrl}";
            case "forward":
                await browser.ForwardAsync();
                return $"Forward: {browser.CurrentUrl}";
            case "read":
                return await browser.ReadTextAsync();
            case "click":
                await browser.ClickAsync(args.GetProperty("selector").GetString() ?? "");
                return "Clicked successfully.";
            case "type":
                await browser.TypeAsync(args.GetProperty("selector").GetString() ?? "", args.GetProperty("text").GetString() ?? "");
                return "Text entered successfully.";
            case "screenshot":
                var path = args.TryGetProperty("path", out var p) ? p.GetString() : null;
                path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WindowsAIAgent", "workspace", "browser.png");
                return $"Screenshot saved: {await browser.ScreenshotAsync(path)}";
            default:
                throw new ArgumentException($"Unknown browser action: {action}");
        }
    }
}