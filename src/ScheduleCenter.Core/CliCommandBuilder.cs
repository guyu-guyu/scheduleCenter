using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ScheduleCenter.Core
{
    public static class CliCommandBuilder
    {
        public static string BuildAddCommand(TaskInfo task)
        {
            var sb = new StringBuilder("ScheduleCenter add");
            sb.Append(" --name \"").Append(task.RelativeName).Append("\"");
            sb.Append(" --path \"").Append(task.Path).Append("\"");
            if (!string.IsNullOrEmpty(task.Arguments))
                sb.Append(" --args \"").Append(task.Arguments).Append("\"");
            if (!string.IsNullOrEmpty(task.WorkingDirectory))
                sb.Append(" --workdir \"").Append(task.WorkingDirectory).Append("\"");

            AppendTriggersArg(sb, task.Triggers);

            if (task.RunAsSystem) sb.Append(" --run-as-system");
            if (task.Highest) sb.Append(" --highest");
            if (!string.IsNullOrEmpty(task.Description))
                sb.Append(" --description \"").Append(task.Description).Append("\"");
            if (task.Enabled) sb.Append(" --enabled");

            AppendAdvancedConditions(sb, task);

            return sb.ToString();
        }

        private static void AppendTriggersArg(StringBuilder sb, IList<TriggerSpec> triggers)
        {
            if (triggers == null || triggers.Count == 0) return;

            if (triggers.Count == 1)
            {
                AppendSingleTriggerArgs(sb, triggers[0]);
                return;
            }

            // 多触发器：输出 --triggers-json
            var jsonTriggers = triggers.Select(t => new
            {
                kind = KindCode(t.Kind),
                time = t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : (string)null,
                date = t.Date.HasValue ? t.Date.Value.ToString("yyyy-MM-dd") : (string)null,
                days = t.Days == null ? null : t.Days.Select(DayCode).ToArray(),
                dayOfMonth = t.DayOfMonth,
                idleSettings = t.IdleSettings == null ? null : new
                {
                    waitTimeout = t.IdleSettings.WaitTimeout.HasValue ? t.IdleSettings.WaitTimeout.Value.ToString(@"hh\:mm\:ss") : (string)null,
                    stopOnIdleEnd = t.IdleSettings.StopOnIdleEnd,
                    restartOnIdle = t.IdleSettings.RestartOnIdle
                },
                eventSubscription = t.EventSubscription,
                eventLog = t.EventLog,
                eventSource = t.EventSource,
                eventId = t.EventId
            }).ToList();

            string json = JsonConvert.SerializeObject(jsonTriggers, Formatting.None);
            sb.Append(" --triggers-json '").Append(json).Append("'");
        }

        private static void AppendSingleTriggerArgs(StringBuilder sb, TriggerSpec trigger)
        {
            switch (trigger.Kind)
            {
                case TriggerKind.Once:
                    sb.Append(" --trigger once");
                    if (trigger.Date.HasValue) sb.Append(" --date ").Append(trigger.Date.Value.ToString("yyyy-MM-dd"));
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    break;
                case TriggerKind.Daily:
                    sb.Append(" --trigger daily");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    break;
                case TriggerKind.Weekly:
                    sb.Append(" --trigger weekly");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    if (trigger.Days != null)
                        sb.Append(" --days ").Append(string.Join(",", trigger.Days.Select(DayCode)));
                    break;
                case TriggerKind.Monthly:
                    sb.Append(" --trigger monthly");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    if (trigger.DayOfMonth.HasValue) sb.Append(" --day-of-month ").Append(trigger.DayOfMonth.Value);
                    break;
                case TriggerKind.Boot:
                    sb.Append(" --trigger boot");
                    break;
                case TriggerKind.Logon:
                    sb.Append(" --trigger logon");
                    break;
                case TriggerKind.Idle:
                    sb.Append(" --trigger idle");
                    if (trigger.IdleSettings != null)
                    {
                        if (trigger.IdleSettings.WaitTimeout.HasValue)
                            sb.Append(" --idle-wait ").Append((int)trigger.IdleSettings.WaitTimeout.Value.TotalMinutes);
                        if (trigger.IdleSettings.StopOnIdleEnd) sb.Append(" --idle-stop-on-end");
                        if (trigger.IdleSettings.RestartOnIdle) sb.Append(" --idle-restart");
                    }
                    break;
                case TriggerKind.Event:
                    sb.Append(" --trigger event");
                    if (!string.IsNullOrEmpty(trigger.EventSubscription))
                        sb.Append(" --event-subscription \"").Append(trigger.EventSubscription).Append("\"");
                    else if (!string.IsNullOrEmpty(trigger.EventLog))
                    {
                        sb.Append(" --event-log ").Append(trigger.EventLog);
                        if (!string.IsNullOrEmpty(trigger.EventSource))
                            sb.Append(" --event-source ").Append(trigger.EventSource);
                        if (trigger.EventId.HasValue)
                            sb.Append(" --event-id ").Append(trigger.EventId.Value);
                    }
                    break;
            }
        }

        private static void AppendAdvancedConditions(StringBuilder sb, TaskInfo task)
        {
            if (task.ExecutionTimeLimit.HasValue)
                sb.Append(" --execution-time-limit ").Append((int)task.ExecutionTimeLimit.Value.TotalMinutes);
            if (task.DisallowStartIfOnBatteries) sb.Append(" --no-start-on-battery");
            if (task.StopIfGoingOnBatteries) sb.Append(" --stop-on-battery");
        }

        private static string KindCode(TriggerKind k)
        {
            switch (k)
            {
                case TriggerKind.Once: return "once";
                case TriggerKind.Daily: return "daily";
                case TriggerKind.Weekly: return "weekly";
                case TriggerKind.Monthly: return "monthly";
                case TriggerKind.Boot: return "boot";
                case TriggerKind.Logon: return "logon";
                case TriggerKind.Idle: return "idle";
                case TriggerKind.Event: return "event";
                default: return k.ToString().ToLowerInvariant();
            }
        }

        private static string FmtTime(TimeSpan t)
        {
            return t.ToString(@"hh\:mm");
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
