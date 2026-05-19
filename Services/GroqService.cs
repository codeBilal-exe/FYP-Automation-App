using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FYP_AutomationSystem.Models;
using Microsoft.Extensions.Configuration;

namespace FYP_AutomationSystem.Services
{
    public class GroqService
    {
        private const string GroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";
        private const string Model = "llama-3.3-70b-versatile";

        private int _keyIndex;
        private readonly string[] _keys;
        private readonly IHttpClientFactory _httpFactory;

        public GroqService(IHttpClientFactory httpFactory, IConfiguration configuration)
        {
            _httpFactory = httpFactory;
            _keys = LoadKeys(configuration);
        }

        private static string[] LoadKeys(IConfiguration configuration)
        {
            var fromConfigArray = configuration.GetSection("Groq:ApiKeys").Get<string[]>() ?? [];
            var fromEnv = Environment.GetEnvironmentVariable("GROQ_API_KEYS") ?? string.Empty;
            var parsedEnv = fromEnv.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return fromConfigArray
                .Concat(parsedEnv)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private string PickKey()
        {
            var idx = Interlocked.Increment(ref _keyIndex) - 1;
            return _keys[((idx % _keys.Length) + _keys.Length) % _keys.Length];
        }

        public async Task<List<FypIdeaDto>> GenerateIdeasAsync(IdeaRequestDto req)
        {
            var systemPrompt =
                "You are an expert Final Year Project (FYP) advisor for computer science and IT students. " +
                "Your role is to generate creative, practical, and achievable FYP ideas tailored to the student's profile. " +
                "Always return ONLY a valid JSON array — no markdown fences, no explanatory text, just the raw JSON array.";

            var count = req.IdeaCount > 0 ? req.IdeaCount : 4;
            var notes = string.IsNullOrWhiteSpace(req.AdditionalNotes) ? "None" : req.AdditionalNotes;
            var schema = "[\n  {\n    \"title\": \"Short project title\",\n    \"summary\": \"2-3 sentence overview\",\n    \"problemStatement\": \"Clear paragraph explaining the real-world problem\",\n    \"technologies\": \"Comma-separated key technologies\"\n  }\n]";
            var userPrompt =
                $"Based on the following student profile, generate exactly {count} unique and practical FYP ideas.\n\n" +
                $"Student Profile:\n" +
                $"- Domain / Field of Interest: {req.Domain}\n" +
                $"- Technologies they know or want to use: {req.Technologies}\n" +
                $"- Problem area to address: {req.ProblemArea}\n" +
                $"- Project scope / team size: {req.Scope}\n" +
                $"- Additional preferences or constraints: {notes}\n\n" +
                $"Return ONLY a JSON array (no extra text) using this exact schema:\n{schema}";

            Exception? lastError = null;

            if (_keys.Length == 0)
            {
                throw new InvalidOperationException("Groq API key is not configured. Set GROQ_API_KEYS env var or Groq:ApiKeys in configuration.");
            }

            for (var attempt = 0; attempt < _keys.Length; attempt++)
            {
                var key = PickKey();
                var client = _httpFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

                var payload = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user",   content = userPrompt   }
                    },
                    temperature = 0.85,
                    max_tokens = 2048
                };

                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsJsonAsync(GroqEndpoint, payload);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    lastError = new Exception("Rate limit hit on one key — rotating.");
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    lastError = new Exception($"Groq API error {(int)response.StatusCode}: {err}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                return ParseIdeas(json);
            }

            throw new Exception($"All Groq API keys are rate-limited or unavailable. Please try again shortly. (Last error: {lastError?.Message})");
        }

        private static List<FypIdeaDto> ParseIdeas(string groqJson)
        {
            using var doc = JsonDocument.Parse(groqJson);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "[]";

            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var start = content.IndexOf('[');
                var end   = content.LastIndexOf(']');
                if (start >= 0 && end > start)
                    content = content[start..(end + 1)];
            }

            using var arr = JsonDocument.Parse(content);
            var ideas = new List<FypIdeaDto>();
            foreach (var elem in arr.RootElement.EnumerateArray())
            {
                ideas.Add(new FypIdeaDto
                {
                    Title            = elem.TryGetProperty("title",            out var t) ? t.GetString() ?? "" : "",
                    Summary          = elem.TryGetProperty("summary",          out var s) ? s.GetString() ?? "" : "",
                    ProblemStatement = elem.TryGetProperty("problemStatement", out var p) ? p.GetString() ?? "" : "",
                    Technologies     = elem.TryGetProperty("technologies",     out var tech) ? tech.GetString() ?? "" : ""
                });
            }
            return ideas;
        }
    }
}
