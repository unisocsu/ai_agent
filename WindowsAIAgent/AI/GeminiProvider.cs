using System.Text;
using System.Text.Json;

namespace WindowsAIAgent.AI;

public sealed class GeminiProvider : IAIProvider
{
    private readonly HttpClient http = new();
    private readonly string apiKey;
    private readonly string model;
    public string Name => "Gemini";

    public GeminiProvider(string apiKey, string model = "gemini-2.5-flash")
    {
        this.apiKey = apiKey;
        this.model = model;
    }

    public async Task<AIResponse> SendAsync(IReadOnlyList<AIMessage> messages, IReadOnlyList<AIToolDefinition> tools, CancellationToken ct = default)
    {
        var contents = messages.Where(m => m.Role != "system").Select(m => new
        {
            role = m.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = m.Content } }
        }).ToArray();

        var system = messages.FirstOrDefault(m => m.Role == "system")?.Content;
        var body = new Dictionary<string, object?> { ["contents"] = contents };
        if (!string.IsNullOrWhiteSpace(system))
            body["systemInstruction"] = new { parts = new[] { new { text = system } } };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        using var response = await http.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        return new AIResponse(text, Array.Empty<AIToolCall>());
    }
}