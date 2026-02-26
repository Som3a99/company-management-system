using ERP.BLL.DTOs;
using ERP.BLL.Services;
using FluentAssertions;

namespace Tests.Services
{
    public class AuditAnomalyServiceTests
    {
        #region IsOffHours
        [Theory]
        [InlineData(23, true)]   // 11 PM
        [InlineData(0, true)]    // Midnight
        [InlineData(3, true)]    // 3 AM
        [InlineData(4, true)]    // 4 AM
        [InlineData(5, false)]   // 5 AM — boundary, not off-hours
        [InlineData(12, false)]  // Noon
        [InlineData(17, false)]  // 5 PM
        [InlineData(22, false)]  // 10 PM
        public void IsOffHours_CorrectlyClassifies(int hour, bool expected)
        {
            var timestamp = new DateTime(2024, 1, 15, hour, 30, 0, DateTimeKind.Utc);
            AuditAnomalyService.IsOffHours(timestamp).Should().Be(expected);
        }
        #endregion

        #region DetectActivitySpikes
        [Fact]
        public void DetectActivitySpikes_BelowThreshold_NoAnomaly()
        {
            var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
            var logs = Enumerable.Range(0, 9).Select(i => new AuditLogEntry
            {
                UserId = "u1",
                UserEmail = "user@test.com",
                Action = "LOGIN",
                Succeeded = true,
                Timestamp = baseTime.AddSeconds(i * 10)
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectActivitySpikes("u1", "user@test.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }

        [Fact]
        public void DetectActivitySpikes_AtThreshold_DetectsSpike()
        {
            var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
            var logs = Enumerable.Range(0, 10).Select(i => new AuditLogEntry
            {
                UserId = "u1",
                UserEmail = "user@test.com",
                Action = "EDIT_EMPLOYEE",
                Succeeded = true,
                Timestamp = baseTime.AddSeconds(i * 10)
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectActivitySpikes("u1", "user@test.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].AnomalyType.Should().Be("ActivitySpike");
            anomalies[0].Severity.Should().Be("Medium");
            anomalies[0].RelatedLogCount.Should().Be(10);
        }

        [Fact]
        public void DetectActivitySpikes_HighVolume_SeverityHigh()
        {
            var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
            var logs = Enumerable.Range(0, 25).Select(i => new AuditLogEntry
            {
                UserId = "u1",
                UserEmail = "user@test.com",
                Action = "VIEW",
                Succeeded = true,
                Timestamp = baseTime.AddSeconds(i * 5)
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectActivitySpikes("u1", "user@test.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].Severity.Should().Be("High");
        }

        [Fact]
        public void DetectActivitySpikes_SpreadOutActions_NoAnomaly()
        {
            var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
            // 10 actions spread over 30 minutes (3 min apart)
            var logs = Enumerable.Range(0, 10).Select(i => new AuditLogEntry
            {
                UserId = "u1",
                UserEmail = "user@test.com",
                Action = "VIEW",
                Succeeded = true,
                Timestamp = baseTime.AddMinutes(i * 3)
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectActivitySpikes("u1", "user@test.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }
        #endregion

        #region DetectOffHoursActivity
        [Fact]
        public void DetectOffHoursActivity_NormalHours_NoAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "user@test.com", Action = "LOGIN", Succeeded = true, Timestamp = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc) },
                new() { UserId = "u1", UserEmail = "user@test.com", Action = "VIEW", Succeeded = true, Timestamp = new DateTime(2024, 1, 15, 14, 0, 0, DateTimeKind.Utc) }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectOffHoursActivity("u1", "user@test.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }

        [Fact]
        public void DetectOffHoursActivity_LateNight_DetectsAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "user@test.com", Action = "LOGIN", Succeeded = true, Timestamp = new DateTime(2024, 1, 15, 23, 30, 0, DateTimeKind.Utc) },
                new() { UserId = "u1", UserEmail = "user@test.com", Action = "EDIT", Succeeded = true, Timestamp = new DateTime(2024, 1, 16, 2, 0, 0, DateTimeKind.Utc) }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectOffHoursActivity("u1", "user@test.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].AnomalyType.Should().Be("OffHoursAccess");
            anomalies[0].RelatedLogCount.Should().Be(2);
        }

        [Fact]
        public void DetectOffHoursActivity_ManyOffHours_SeverityHigh()
        {
            var logs = Enumerable.Range(0, 6).Select(i => new AuditLogEntry
            {
                UserId = "u1",
                UserEmail = "user@test.com",
                Action = "VIEW",
                Succeeded = true,
                Timestamp = new DateTime(2024, 1, 15, 1, i * 10, 0, DateTimeKind.Utc)
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectOffHoursActivity("u1", "user@test.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].Severity.Should().Be("High");
        }
        #endregion

        #region DetectFailedAttempts
        [Fact]
        public void DetectFailedAttempts_BelowThreshold_NoAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = true, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectFailedAttempts("u1", "test@t.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }

        [Fact]
        public void DetectFailedAttempts_ThreeConsecutive_DetectsAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectFailedAttempts("u1", "test@t.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].AnomalyType.Should().Be("ConsecutiveFailures");
            anomalies[0].RelatedLogCount.Should().Be(3);
            anomalies[0].Severity.Should().Be("Medium");
        }

        [Fact]
        public void DetectFailedAttempts_FiveConsecutive_SeverityHigh()
        {
            var logs = Enumerable.Range(0, 5).Select(_ => new AuditLogEntry
            {
                UserId = "u1", UserEmail = "test@t.com", Action = "LOGIN",
                Succeeded = false, Timestamp = DateTime.UtcNow
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectFailedAttempts("u1", "test@t.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].Severity.Should().Be("High");
        }

        [Fact]
        public void DetectFailedAttempts_InterruptedBySuccess_NoAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN", Succeeded = false, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectFailedAttempts("u1", "t@t.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }
        #endregion

        #region DetectDestructivePatterns
        [Fact]
        public void DetectDestructivePatterns_BelowThreshold_NoAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_EMPLOYEE", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_DEPARTMENT", Succeeded = true, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectDestructivePatterns("u1", "t@t.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }

        [Fact]
        public void DetectDestructivePatterns_AtThreshold_DetectsAnomaly()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_EMPLOYEE", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_DEPARTMENT", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_PROJECT", Succeeded = true, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectDestructivePatterns("u1", "t@t.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].AnomalyType.Should().Be("DestructivePattern");
            anomalies[0].RelatedLogCount.Should().Be(3);
        }

        [Fact]
        public void DetectDestructivePatterns_FiveOrMore_SeverityHigh()
        {
            var logs = new List<AuditLogEntry>
            {
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_EMPLOYEE", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_DEPARTMENT", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_PROJECT", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "DELETE_TASK", Succeeded = true, Timestamp = DateTime.UtcNow },
                new() { UserId = "u1", UserEmail = "t@t.com", Action = "LOCK_ACCOUNT", Succeeded = true, Timestamp = DateTime.UtcNow }
            };

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectDestructivePatterns("u1", "t@t.com", logs, anomalies);

            anomalies.Should().HaveCount(1);
            anomalies[0].Severity.Should().Be("High");
        }

        [Fact]
        public void DetectDestructivePatterns_NonDestructiveActions_NoAnomaly()
        {
            var logs = Enumerable.Range(0, 10).Select(_ => new AuditLogEntry
            {
                UserId = "u1", UserEmail = "t@t.com", Action = "LOGIN",
                Succeeded = true, Timestamp = DateTime.UtcNow
            }).ToList();

            var anomalies = new List<AuditAnomalyFlag>();
            AuditAnomalyService.DetectDestructivePatterns("u1", "t@t.com", logs, anomalies);

            anomalies.Should().BeEmpty();
        }
        #endregion
    }
}
