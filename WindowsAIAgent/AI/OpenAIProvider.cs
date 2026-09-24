using System.Net.Http.Json;
using System.Text.Json;

namespace WindowsAIAgent.AI;

public sealed class OpenAIProvider : IAIProvider
{
    private readonly HttpClient http;
    private readonly string apiKey;
    private readonly string model;

    public string Name => "OpenAI";

    public OpenAIProvider(string apiKey, string model = "gpt-4o-mini")
    {
        this.apiKey = apiKey;
        this.model = model;
        http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<AIResponse> SendAsync(IReadOnlyList<AIMessage> messages, IReadOnlyList<AIToolDefinition> tools, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };

        if (tools.Count > 0)
            body["tools"] = tools.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.Parameters } }).ToArray();

        using var response = await http.PostAsJsonAsync("v1/chat/completions", body, cancellationToken: ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var content = message.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetString() ?? "" : "";
        var calls = new List<AIToolCall>();

        if (message.TryGetProperty("tool_calls", out var tc))
            foreach (var call in tc.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                calls.Add(new AIToolCall(call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(), fn.GetProperty("name").GetString() ?? "", fn.GetProperty("arguments").GetString() ?? "{}"));
            }

        return new AIResponse(content, calls);
    }
}