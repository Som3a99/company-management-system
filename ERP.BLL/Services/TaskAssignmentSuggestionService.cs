using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Services
{
    internal sealed class EmployeeExperienceData
    {
        public int EmployeeId { get; init; }
        public List<string> CompletedTitles { get; init; } = new();
        public int CompletedCount { get; init; }
    }

    public class TaskAssignmentSuggestionService : ITaskAssignmentSuggestionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorkloadService _workloadService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<TaskAssignmentSuggestionService> _logger;

        public TaskAssignmentSuggestionService(
            ApplicationDbContext context,
            IWorkloadService workloadService,
            IMemoryCache cache,
            ILogger<TaskAssignmentSuggestionService> logger)
        {
            _context = context;
            _workloadService = workloadService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<TaskAssignmentSuggestion>> GetSuggestionsAsync(int projectId, string? taskTitle)
        {
            var cacheKey = $"erp:suggest:{projectId}:{taskTitle?.GetHashCode() ?? 0}";

            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
                entry.Size = 1;
                return await ComputeSuggestionsAsync(projectId, taskTitle);
            });

            return result ?? new List<TaskAssignmentSuggestion>();
        }

        private async Task<List<TaskAssignmentSuggestion>> ComputeSuggestionsAsync(int projectId, string? taskTitle)
        {
            // Get employees assigned to this project
            var projectEmployeeIds = await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId)
                .Select(pe => pe.EmployeeId)
                .Distinct()
                .ToListAsync();

            // Also include employees directly assigned to the project (legacy relation)
            var legacyEmployeeIds = await _context.Employees
                .Where(e => e.ProjectId == projectId && !e.IsDeleted && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();

            var allEmployeeIds = projectEmployeeIds.Union(legacyEmployeeIds).Distinct().ToList();

            if (allEmployeeIds.Count == 0)
                return new List<TaskAssignmentSuggestion>();

            // Get employee info
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e => allEmployeeIds.Contains(e.Id) && !e.IsDeleted && e.IsActive)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName })
                .ToListAsync();

            // Get workload data
            var workloads = await _workloadService.GetWorkloadAsync(projectId);
            var workloadMap = workloads.ToDictionary(w => w.EmployeeId, w => w.LoadScore);

            // Get accuracy data per employee (completed tasks with estimates)
            var accuracyData = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status == TaskStatus.Completed
                    && t.AssignedToEmployeeId.HasValue
                    && allEmployeeIds.Contains(t.AssignedToEmployeeId.Value)
                    && t.EstimatedHours.HasValue && t.EstimatedHours.Value > 0)
                .GroupBy(t => t.AssignedToEmployeeId!.Value)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    TotalEstimated = g.Sum(t => (double)t.EstimatedHours!.Value),
                    TotalActual = g.Sum(t => (double)t.ActualHours)
                })
                .ToListAsync();

            var accuracyMap = accuracyData.ToDictionary(
                a => a.EmployeeId,
                a => a.TotalEstimated > 0 ? a.TotalActual / a.TotalEstimated : 1.0);

            // Get experience data — titles of completed tasks per employee for keyword matching
            var taskTitleKeywords = ExtractKeywords(taskTitle);

            var experienceData = await _context.TaskItems
                .AsNoTracking()
                .Where(t => t.Status == TaskStatus.Completed
                    && t.AssignedToEmployeeId.HasValue
                    && allEmployeeIds.Contains(t.AssignedToEmployeeId.Value))
                .GroupBy(t => t.AssignedToEmployeeId!.Value)
                .Select(g => new EmployeeExperienceData
                {
                    EmployeeId = g.Key,
                    CompletedTitles = g.Select(t => t.Title).ToList(),
                    CompletedCount = g.Count()
                })
                .ToListAsync();

            var experienceMap = experienceData.ToDictionary(e => e.EmployeeId);

            // Score each employee
            var suggestions = new List<TaskAssignmentSuggestion>();

            foreach (var emp in employees)
            {
                var experienceScore = CalculateExperienceScore(emp.Id, taskTitleKeywords, experienceMap);
                var accuracyScore = CalculateAccuracyScore(emp.Id, accuracyMap);
                var availabilityScore = CalculateAvailabilityScore(emp.Id, workloadMap);

                var composite = Math.Clamp(
                    (int)Math.Round(experienceScore * 0.4 + accuracyScore * 0.3 + availabilityScore * 0.3),
                    0, 100);

                var reasons = new List<string>();
                if (experienceScore >= 60) reasons.Add("strong experience match");
                else if (experienceScore >= 30) reasons.Add("moderate experience");
                if (accuracyScore >= 60) reasons.Add("accurate estimator");
                if (availabilityScore >= 60) reasons.Add("available capacity");
                else if (availabilityScore < 30) reasons.Add("heavy workload");

                suggestions.Add(new TaskAssignmentSuggestion
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    CompositeScore = composite,
                    ExperienceScore = experienceScore,
                    AccuracyScore = accuracyScore,
                    AvailabilityScore = availabilityScore,
                    Reasoning = reasons.Count > 0
                        ? string.Join(", ", reasons).CapitalizeFirst()
                        : "No strong signals"
                });
            }

            return suggestions
                .OrderByDescending(s => s.CompositeScore)
                .Take(5)
                .ToList();
        }

        internal static int CalculateExperienceScore(
            int employeeId,
            List<string> taskKeywords,
            Dictionary<int, EmployeeExperienceData> experienceMap)
        {
            if (!experienceMap.TryGetValue(employeeId, out var data))
                return 0;

            // Base experience from completed task count (up to 30 points)
            var baseExperience = Math.Min(data.CompletedCount * 3, 30);

            if (taskKeywords.Count == 0)
                return Math.Clamp(baseExperience, 0, 100);

            // Keyword overlap scoring (up to 70 points)
            var matchCount = 0;
            var allCompletedText = string.Join(" ", data.CompletedTitles).ToLowerInvariant();

            foreach (var keyword in taskKeywords)
            {
                if (allCompletedText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    matchCount++;
            }

            var keywordScore = taskKeywords.Count > 0
                ? (int)Math.Round((double)matchCount / taskKeywords.Count * 70)
                : 0;

            return Math.Clamp(baseExperience + keywordScore, 0, 100);
        }

        internal static int CalculateAccuracyScore(int employeeId, Dictionary<int, double> accuracyMap)
        {
            if (!accuracyMap.TryGetValue(employeeId, out var ratio))
                return 50; // No data — neutral score

            // Perfect accuracy (ratio = 1.0) gets 100
            // The further from 1.0, the lower the score
            var deviation = Math.Abs(ratio - 1.0);
            var score = (int)Math.Round(Math.Max(0, 100 - deviation * 100));
            return Math.Clamp(score, 0, 100);
        }

        internal static int CalculateAvailabilityScore(int employeeId, Dictionary<int, int> workloadMap)
        {
            if (!workloadMap.TryGetValue(employeeId, out var loadScore))
                return 80; // No workload data = likely available

            // Invert workload: high load = low availability
            return Math.Clamp(100 - loadScore, 0, 100);
        }

        internal static List<string> ExtractKeywords(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "a", "an", "is", "are", "was", "were", "be", "been",
                "and", "or", "not", "for", "to", "in", "on", "at", "of",
                "with", "this", "that", "it", "as", "by", "from", "task",
                "create", "update", "fix", "add", "new", "implement"
            };

            return text
                .Split(new[] { ' ', '-', '_', '.', ',', '/', '\\', '(', ')', '[', ']' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList();
        }
    }

    internal static class StringExtensions
    {
        internal static string CapitalizeFirst(this string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
