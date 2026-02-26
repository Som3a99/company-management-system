using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ERP.BLL.Services
{
    public sealed class TaskDescriptionService : ITaskDescriptionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TaskDescriptionService> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly string _model;

        public TaskDescriptionService(
            IHttpClientFactory httpClientFactory,
            ILogger<TaskDescriptionService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiKey = configuration["AiService:ApiKey"] ?? string.Empty;
            _apiUrl = configuration["AiService:ApiUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/models";
            _model = configuration["AiService:Model"] ?? "gemini-2.0-flash";
        }

        public async Task<string> GenerateDescriptionAsync(GenerateTaskDescriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return string.Empty;

            var prompt = BuildPrompt(request);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("AI API key not configured. Returning fallback description.");
                return BuildFallbackDescription(request);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("AiService");

                var fullPrompt = "You are a project management assistant. Generate concise task descriptions with 3 sections: Goal, Steps, Definition of Done. Use plain text, no markdown headers.\n\n" + prompt;

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = fullPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = 400,
                        temperature = 0.5
                    }
                };

                var requestUrl = $"{_apiUrl}/{_model}:generateContent?key={_apiKey}";
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(requestUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini API returned {StatusCode} for task description. Using fallback.", response.StatusCode);
                    return BuildFallbackDescription(request);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text) ? BuildFallbackDescription(request) : text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Gemini API for task description. Using fallback.");
                return BuildFallbackDescription(request);
            }
        }

        internal static string BuildPrompt(GenerateTaskDescriptionRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generate a task description for a task titled \"{request.Title}\".");
            if (!string.IsNullOrWhiteSpace(request.ProjectName))
                sb.AppendLine($"This task belongs to the project \"{request.ProjectName}\".");
            sb.AppendLine("Include 3 sections: Goal (1 sentence), Steps (3-5 bullet points), Definition of Done (2-3 criteria).");
            return sb.ToString();
        }

        internal static string BuildFallbackDescription(GenerateTaskDescriptionRequest request)
        {
            var projectContext = !string.IsNullOrWhiteSpace(request.ProjectName)
                ? $" for the {request.ProjectName} project"
                : string.Empty;

            return $"""
                Goal: Complete the "{request.Title}" task{projectContext}.

                Steps:
                - Analyze requirements and acceptance criteria
                - Implement the necessary changes
                - Write or update tests to cover changes
                - Submit for code review

                Definition of Done:
                - All acceptance criteria are met
                - Tests pass successfully
                - Code review approved
                """;
        }
    }
}
