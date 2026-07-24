using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class TaskDto
    {
        public static object From(TaskInfo t)
        {
            return new
            {
                name = t.RelativeName,
                folder = t.Folder,
                path = t.Path,
                args = t.Arguments,
                workdir = t.WorkingDirectory,
                description = t.Description,
                enabled = t.Enabled,
                state = t.State,
                runAsSystem = t.RunAsSystem,
                highest = t.Highest,
                triggers = (t.Triggers == null ? new List<object>() : t.Triggers.Select(TriggerDto).ToList()),
                nextRunTime = t.NextRunTime,
                lastRunTime = t.LastRunTime,
                lastResult = t.LastResult,
                executionTimeLimit = FmtTimeSpan(t.ExecutionTimeLimit),
                disallowStartIfOnBatteries = t.DisallowStartIfOnBatteries,
                stopIfGoingOnBatteries = t.StopIfGoingOnBatteries
            };
        }

        private static object TriggerDto(TriggerSpec spec)
        {
            if (spec == null) return null;
            switch (spec.Kind)
            {
                case TriggerKind.Once:
                    return new
                    {
                        kind = "once",
                        date = spec.Date.HasValue ? spec.Date.Value.ToString("yyyy-MM-dd") : null,
                        time = FmtTime(spec.Time)
                    };
                case TriggerKind.Daily:
                    return new { kind = "daily", time = FmtTime(spec.Time) };
                case TriggerKind.Weekly:
                    return new { kind = "weekly", time = FmtTime(spec.Time), days = spec.Days == null ? null : spec.Days.Select(DayCode).ToArray() };
                case TriggerKind.Monthly:
                    return new { kind = "monthly", time = FmtTime(spec.Time), dayOfMonth = spec.DayOfMonth };
                case TriggerKind.Boot:
                    return new { kind = "boot" };
                case TriggerKind.Logon:
                    return new { kind = "logon" };
                case TriggerKind.Idle:
                    return new
                    {
                        kind = "idle",
                        idleSettings = spec.IdleSettings == null ? null : new
                        {
                            waitTimeout = FmtTimeSpan(spec.IdleSettings.WaitTimeout),
                            stopOnIdleEnd = spec.IdleSettings.StopOnIdleEnd,
                            restartOnIdle = spec.IdleSettings.RestartOnIdle
                        }
                    };
                case TriggerKind.Event:
                    return new
                    {
                        kind = "event",
                        eventSubscription = spec.EventSubscription,
                        eventLog = spec.EventLog,
                        eventSource = spec.EventSource,
                        eventId = spec.EventId
                    };
                default:
                    return new { kind = spec.Kind.ToString().ToLowerInvariant() };
            }
        }

        private static string FmtTime(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm") : null;
        }

        private static string FmtTimeSpan(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm\:ss") : null;
        }

        private static string DayCode(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "MON";
                case DayOfWeek.Tuesday: return "TUE";
                case DayOfWeek.Wednesday: return "WED";
                case DayOfWeek.Thursday: return "THU";
                case DayOfWeek.Friday: return "FRI";
                case DayOfWeek.Saturday: return "SAT";
                default: return "SUN";
            }
        }
    }
}
