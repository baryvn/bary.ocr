using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class LlmService(HttpClient httpClient, IConfiguration configuration, ILogger<LlmService> logger)
{
    public async Task<string> CallLlmAsync(string systemPrompt, string userPrompt)
    {
        string baseUrl = configuration["LlmServer:Url"] ?? "http://localhost:8080";
        string llmUrl = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";
        string apiKey = configuration["LlmServer:ApiKey"] ?? "sk-mock";

        string combinedPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var payload = new
        {
            model = configuration["LlmServer:Model"] ?? "gemma-4-E2B_q4_0-it",
            messages = new[]
            {
                new { role = "user", content = combinedPrompt }
            },
            temperature = 0.0,
            max_tokens = 2000
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var response = await httpClient.PostAsync(llmUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseJson);
            
            var resultText = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return resultText ?? "{}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi gọi LLM Server");
            throw;
        }
    }
}
