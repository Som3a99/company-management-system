using ERP.BLL.DTOs;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using Microsoft.Extensions.Logging;
using TaskStatus = ERP.DAL.Models.TaskStatus;

namespace ERP.BLL.Services
{
    public class TaskRiskService : ITaskRiskService
    {
        private readonly ILogger<TaskRiskService> _logger;

        public TaskRiskService(ILogger<TaskRiskService> logger)
        {
            _logger = logger;
        }

        public TaskRiskResult CalculateRisk(TaskItem task)
        {
            var score = 0;
            var reasons = new List<string>();

            // Skip completed/cancelled tasks — no risk
            if (task.Status == TaskStatus.Completed || task.Status == TaskStatus.Cancelled)
            {
                return new TaskRiskResult { Score = 0, Level = "Low", Reason = "Task is completed or cancelled." };
            }

            // ── Days Remaining ──
            double? daysRemaining = null;
            if (task.DueDate.HasValue)
            {
                daysRemaining = (task.DueDate.Value.Date - DateTime.UtcNow.Date).TotalDays;

                if (daysRemaining < 0)
                {
                    score += 40;
                    reasons.Add($"Task is overdue by {Math.Abs((int)daysRemaining.Value)} day(s).");
                }
                else if (daysRemaining <= 2)
                {
                    score += 25;
                    reasons.Add($"Due in {(int)daysRemaining.Value} day(s).");
                }
                else if (daysRemaining <= 5)
                {
                    score += 15;
                    reasons.Add($"Due in {(int)daysRemaining.Value} day(s).");
                }
            }

            // ── Progress Ratio ──
            decimal progress = 0m;
            if (task.EstimatedHours.HasValue && task.EstimatedHours.Value > 0)
            {
                progress = task.ActualHours / task.EstimatedHours.Value;

                // Hours exceeded estimate
                if (task.ActualHours > task.EstimatedHours.Value)
                {
                    score += 15;
                    reasons.Add("Actual hours exceeded estimate.");
                }

                // Low progress with near due date
                if (progress < 0.5m && daysRemaining.HasValue && daysRemaining.Value <= 5)
                {
                    score += 20;
                    var pct = (int)(progress * 100);
                    reasons.Add($"Only {pct}% progress with deadline approaching.");
                }
            }

            // ── Status Penalty ──
            if (task.Status == TaskStatus.Blocked)
            {
                score += 25;
                reasons.Add("Task is blocked.");
            }
            else if (task.Status == TaskStatus.InProgress)
            {
                score += 10;
            }

            // ── Priority Weight ──
            if (task.Priority == TaskPriority.Critical)
            {
                score += 20;
                reasons.Add("Priority is Critical.");
            }
            else if (task.Priority == TaskPriority.High)
            {
                score += 10;
                reasons.Add("Priority is High.");
            }

            // ── Clamp 0–100 ──
            score = Math.Clamp(score, 0, 100);

            // ── Determine Level ──
            var level = score switch
            {
                >= 70 => "High",
                >= 40 => "Medium",
                _ => "Low"
            };

            var reason = reasons.Count > 0
                ? string.Join(" ", reasons)
                : "No significant risk factors detected.";

            // ── Logging Hook ──
            if (level == "High")
            {
                _logger.LogInformation("High risk task detected: {TaskId}, Score: {Score}, Reason: {Reason}", task.Id, score, reason);
            }

            return new TaskRiskResult
            {
                Score = score,
                Level = level,
                Reason = reason
            };
        }
    }
}
