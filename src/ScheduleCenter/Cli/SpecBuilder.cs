using System;
using System.Globalization;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class SpecBuilder
    {
        public static TaskSpec BuildSpec(ParsedArgs p)
        {
            var spec = new TaskSpec
            {
                Name = p.Require("name"),
                Path = p.Require("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Trigger = BuildTrigger(p, true),
                Enabled = true,
                RunAsSystem = p.Has("run-as-system"),
                Highest = p.Has("highest"),
                StartWhenAvailable = true
            };
            TaskValidator.ValidateSpec(spec);
            return spec;
        }

        public static TaskUpdate BuildUpdate(ParsedArgs p)
        {
            var update = new TaskUpdate
            {
                Name = p.Require("name"),
                Path = p.Get("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Trigger = BuildTrigger(p, false),
                RunAsSystem = p.Has("run-as-system") ? true : (bool?)null,
                Highest = p.Has("highest") ? true : (bool?)null,
                Enabled = p.Has("enabled") ? true : (bool?)null
            };

            if (update.Path == null && update.Arguments == null && update.WorkingDirectory == null &&
                update.Description == null && update.Trigger == null && update.RunAsSystem == null &&
                update.Highest == null && update.Enabled == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "update 命令至少需要一个要修改的选项");

            if (update.Path != null && !System.IO.File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            return update;
        }

        public static TriggerSpec BuildTrigger(ParsedArgs p, bool required)
        {
            string type = p.Get("trigger");
            if (type == null)
            {
                if (required)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少必需参数 --trigger");
                return null;
            }

            var spec = new TriggerSpec();

            string timeStr = p.Get("time");
            if (timeStr != null)
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(timeStr, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "时间格式错误 '" + timeStr + "'，应为 HH:mm");
                spec.Time = time;
            }

            string dateStr = p.Get("date");
            if (dateStr != null)
            {
                DateTime date;
                if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "日期格式错误 '" + dateStr + "'，应为 yyyy-MM-dd");
                spec.Date = date;
            }

            string daysStr = p.Get("days");
            if (daysStr != null)
                spec.Days = daysStr.Split(',').Select(ParseDay).ToArray();

            string domStr = p.Get("day-of-month");
            if (domStr != null)
            {
                int dom;
                if (!int.TryParse(domStr, out dom))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "day-of-month 格式错误 '" + domStr + "'");
                spec.DayOfMonth = dom;
            }

            switch (type.ToLowerInvariant())
            {
                case "once": spec.Kind = TriggerKind.Once; break;
                case "daily": spec.Kind = TriggerKind.Daily; break;
                case "weekly": spec.Kind = TriggerKind.Weekly; break;
                case "monthly": spec.Kind = TriggerKind.Monthly; break;
                case "boot": spec.Kind = TriggerKind.Boot; break;
                case "logon": spec.Kind = TriggerKind.Logon; break;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型 '" + type + "'");
            }

            TaskValidator.ValidateTrigger(spec);
            return spec;
        }

        public static DayOfWeek ParseDay(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "MON": return DayOfWeek.Monday;
                case "TUE": return DayOfWeek.Tuesday;
                case "WED": return DayOfWeek.Wednesday;
                case "THU": return DayOfWeek.Thursday;
                case "FRI": return DayOfWeek.Friday;
                case "SAT": return DayOfWeek.Saturday;
                case "SUN": return DayOfWeek.Sunday;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "非法星期值 '" + s + "'，应为 MON-SUN");
            }
        }
    }
}
