using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;

namespace WindowsAIAgent.Browser;

public sealed class BrowserService : IAsyncDisposable
{
    private readonly Form host = new()
    {
        Text = "Windows AI Agent Browser",
        Width = 1200,
        Height = 800,
        ShowInTaskbar = false,
        StartPosition = FormStartPosition.Manual,
        Left = -32000,
        Top = -32000
    };
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };
    private bool initialized;

    public string CurrentUrl => view.CoreWebView2?.Source ?? "";

    public async Task InitializeAsync()
    {
        if (initialized) return;
        host.Controls.Add(view);
        host.CreateControl();
        host.Show();
        host.Hide();

        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsAIAgent", "BrowserWebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await view.EnsureCoreWebView2Async(env);
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        initialized = true;
    }

    public async Task NavigateAsync(string url, CancellationToken ct = default)
    {
        await InitializeAsync();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Only HTTP and HTTPS URLs are allowed.", nameof(url));

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
        {
            view.CoreWebView2.NavigationCompleted -= Handler;
            if (e.IsSuccess) tcs.TrySetResult();
            else tcs.TrySetException(new InvalidOperationException($"Navigation failed: {e.WebErrorStatus}"));
        }

        view.CoreWebView2.NavigationCompleted += Handler;
        view.CoreWebView2.Navigate(uri.ToString());
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
    }

    public async Task<string> ReadTextAsync()
    {
        await InitializeAsync();
        return await ExecuteStringAsync("document.body ? document.body.innerText : document.documentElement.innerText;");
    }

    public async Task<string> ExecuteStringAsync(string script)
    {
        await InitializeAsync();
        var json = await view.CoreWebView2.ExecuteScriptAsync(script);
        try { return JsonSerializer.Deserialize<string>(json) ?? ""; }
        catch { return json; }
    }

    public async Task ClickAsync(string selector)
    {
        ValidateSelector(selector);
        var script = $@"(() => {{
            const el = document.querySelector({JsonSerializer.Serialize(selector)});
            if (!el) return 'NOT_FOUND';
            el.scrollIntoView({{block:'center'}});
            el.click();
            return 'OK';
        }})()";
        var result = await ExecuteStringAsync(script);
        if (result == "NOT_FOUND") throw new InvalidOperationException($"Element not found: {selector}");
    }

    public async Task TypeAsync(string selector, string text)
    {
        ValidateSelector(selector);
        var script = $@"(() => {{
            const el = document.querySelector({JsonSerializer.Serialize(selector)});
            if (!el) return 'NOT_FOUND';
            el.focus();
            const value = {JsonSerializer.Serialize(text)};
            const proto = Object.getPrototypeOf(el);
            const descriptor = Object.getOwnPropertyDescriptor(proto, 'value');
            if (descriptor?.set) descriptor.set.call(el, value); else el.value = value;
            el.dispatchEvent(new Event('input', {{bubbles:true}}));
            el.dispatchEvent(new Event('change', {{bubbles:true}}));
            return 'OK';
        }})()";
        var result = await ExecuteStringAsync(script);
        if (result == "NOT_FOUND") throw new InvalidOperationException($"Element not found: {selector}");
    }

    public async Task<string> ScreenshotAsync(string path)
    {
        await InitializeAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
        return Path.GetFullPath(path);
    }

    public Task BackAsync()
    {
        if (view.CoreWebView2?.CanGoBack == true) view.CoreWebView2.GoBack();
        return Task.CompletedTask;
    }

    public Task ForwardAsync()
    {
        if (view.CoreWebView2?.CanGoForward == true) view.CoreWebView2.GoForward();
        return Task.CompletedTask;
    }

    private static void ValidateSelector(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) throw new ArgumentException("CSS selector is required.", nameof(selector));
        if (selector.Length > 1000) throw new ArgumentException("Selector is too long.", nameof(selector));
    }

    public async ValueTask DisposeAsync()
    {
        if (view.CoreWebView2 is not null) await view.CoreWebView2.TrySuspendAsync();
        view.Dispose();
        host.Dispose();
    }
}