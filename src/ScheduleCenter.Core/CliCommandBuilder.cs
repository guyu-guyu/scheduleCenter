using System;
using System.Linq;
using System.Text;

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
            AppendTriggerArgs(sb, task.Trigger);
            if (task.RunAsSystem) sb.Append(" --run-as-system");
            if (task.Highest) sb.Append(" --highest");
            if (!string.IsNullOrEmpty(task.Description))
                sb.Append(" --description \"").Append(task.Description).Append("\"");
            if (task.Enabled) sb.Append(" --enabled");
            return sb.ToString();
        }

        private static void AppendTriggerArgs(StringBuilder sb, TriggerSpec trigger)
        {
            if (trigger == null) return;
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
