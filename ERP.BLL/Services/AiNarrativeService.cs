using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ERP.BLL.Services
{
    public sealed class AiNarrativeService : IAiNarrativeService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICacheService _cacheService;
        private readonly ILogger<AiNarrativeService> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl;
        private readonly string _model;

        private const string CachePrefix = "erp:ai:summary:";

        public AiNarrativeService(
            IHttpClientFactory httpClientFactory,
            ICacheService cacheService,
            ILogger<AiNarrativeService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cacheService = cacheService;
            _logger = logger;
            _apiKey = configuration["AiService:ApiKey"] ?? string.Empty;
            _apiUrl = configuration["AiService:ApiUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/models";
            _model = configuration["AiService:Model"] ?? "gemini-2.0-flash";
        }

        public async Task<string> GenerateSummaryAsync(ReportSummaryInput input)
        {
            var cacheKey = BuildCacheKey(input);

            return await _cacheService.GetOrCreateSafeAsync(
                cacheKey,
                async () => await GenerateSummaryCoreAsync(input),
                TimeSpan.FromMinutes(5));
        }

        private async Task<string> GenerateSummaryCoreAsync(ReportSummaryInput input)
        {
            var prompt = BuildPrompt(input);

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("AI API key not configured. Returning fallback summary.");
                return BuildFallbackSummary(input);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("AiService");

                var fullPrompt = "You are a concise executive report assistant. Respond in exactly 5 sentences. No markdown.\n\n" + prompt;

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
                        maxOutputTokens = 300,
                        temperature = 0.4
                    }
                };

                var requestUrl = $"{_apiUrl}/{_model}:generateContent?key={_apiKey}";
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(requestUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini API returned {StatusCode}. Falling back to template summary.", response.StatusCode);
                    return BuildFallbackSummary(input);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text) ? BuildFallbackSummary(input) : text.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call Gemini API for report summary. Using fallback.");
                return BuildFallbackSummary(input);
            }
        }

        internal static string BuildPrompt(ReportSummaryInput input)
        {
            return $"""
                Summarize the following ERP report data in exactly 5 sentences for an executive audience:
                - Total Tasks: {input.TotalTasks}
                - Completed Tasks: {input.CompletedTasks}
                - Overdue Tasks: {input.OverdueTasks}
                - Blocked Tasks: {input.BlockedTasks}
                - Active Projects: {input.ActiveProjects}
                - Department with most tasks: {input.DepartmentWithMostTasks}
                Focus on completion rate, risk areas, and actionable recommendations.
                """;
        }

        internal static string BuildFallbackSummary(ReportSummaryInput input)
        {
            var completionRate = input.TotalTasks > 0
                ? (double)input.CompletedTasks / input.TotalTasks * 100
                : 0;

            var overdueRatio = input.TotalTasks > 0
                ? (double)input.OverdueTasks / input.TotalTasks * 100
                : 0;

            var sb = new StringBuilder();
            sb.Append($"The organization is tracking {input.TotalTasks} tasks across {input.ActiveProjects} active projects. ");
            sb.Append($"The overall completion rate stands at {completionRate:F0}% ({input.CompletedTasks} of {input.TotalTasks} tasks). ");

            if (input.OverdueTasks > 0)
                sb.Append($"There are {input.OverdueTasks} overdue tasks ({overdueRatio:F0}% of total), indicating scheduling pressure. ");
            else
                sb.Append("No tasks are currently overdue, reflecting healthy schedule adherence. ");

            if (input.BlockedTasks > 0)
                sb.Append($"{input.BlockedTasks} tasks are blocked and require immediate attention to prevent further delays. ");
            else
                sb.Append("No tasks are currently blocked, suggesting smooth workflow progression. ");

            if (!string.IsNullOrWhiteSpace(input.DepartmentWithMostTasks))
                sb.Append($"The {input.DepartmentWithMostTasks} department carries the highest task load and should be monitored for capacity.");
            else
                sb.Append("Task distribution across departments should be reviewed for workload balance.");

            return sb.ToString();
        }

        private static string BuildCacheKey(ReportSummaryInput input)
        {
            var payload = $"{input.TotalTasks}|{input.CompletedTasks}|{input.OverdueTasks}|{input.BlockedTasks}|{input.ActiveProjects}|{input.DepartmentWithMostTasks}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..10];
            return $"{CachePrefix}{hash}";
        }
    }
}
