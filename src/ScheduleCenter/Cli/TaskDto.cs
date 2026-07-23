using System;
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
                trigger = TriggerDto(t.Trigger),
                nextRunTime = t.NextRunTime,
                lastRunTime = t.LastRunTime,
                lastResult = t.LastResult
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
                        type = "once",
                        date = spec.Date.HasValue ? spec.Date.Value.ToString("yyyy-MM-dd") : null,
                        time = FmtTime(spec.Time)
                    };
                case TriggerKind.Daily:
                    return new { type = "daily", time = FmtTime(spec.Time) };
                case TriggerKind.Weekly:
                    return new { type = "weekly", time = FmtTime(spec.Time), days = spec.Days == null ? null : spec.Days.Select(DayCode).ToArray() };
                case TriggerKind.Monthly:
                    return new { type = "monthly", time = FmtTime(spec.Time), dayOfMonth = spec.DayOfMonth };
                case TriggerKind.Boot:
                    return new { type = "boot" };
                default:
                    return new { type = "logon" };
            }
        }

        private static string FmtTime(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm") : null;
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
