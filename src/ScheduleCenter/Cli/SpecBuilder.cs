using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
                Triggers = BuildTriggers(p),
                Enabled = true,
                RunAsSystem = p.Has("run-as-system"),
                Highest = p.Has("highest"),
                StartWhenAvailable = true
            };
            ApplyAdvancedConditions(spec, p);
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
                Triggers = BuildTriggers(p, false),
                RunAsSystem = p.Has("run-as-system") ? true : (bool?)null,
                Highest = p.Has("highest") ? true : (bool?)null,
                Enabled = p.Has("enabled") ? true : (bool?)null
            };
            ApplyAdvancedConditionsUpdate(update, p);
            TaskValidator.ValidateUpdate(update);
            return update;
        }

        public static IList<TriggerSpec> BuildTriggers(ParsedArgs p)
        {
            return BuildTriggers(p, true);
        }

        private static IList<TriggerSpec> BuildTriggers(ParsedArgs p, bool required)
        {
            // 多触发器 JSON 路径
            if (p.Has("triggers-json"))
            {
                return ParseTriggersJson(p.Get("triggers-json"));
            }
            if (p.Has("triggers-file"))
            {
                string filePath = p.Get("triggers-file");
                if (!File.Exists(filePath))
                    throw new TaskServiceException(ErrorCode.InvalidPath, "触发器文件不存在: " + filePath);
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                return ParseTriggersJson(json);
            }

            // 单触发器 V1 兼容路径
            string type = p.Get("trigger");
            if (type == null)
            {
                if (required)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少必需参数 --trigger");
                return null;
            }

            var spec = BuildSingleTrigger(p, type);
            TaskValidator.ValidateTrigger(spec);
            return new List<TriggerSpec> { spec };
        }

        private static IList<TriggerSpec> ParseTriggersJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 内容为空");

            List<JsonTrigger> parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<List<JsonTrigger>>(json);
            }
            catch (Exception ex)
            {
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 解析失败: " + ex.Message, ex);
            }
            if (parsed == null || parsed.Count == 0)
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 至少包含一个触发器");

            var result = new List<TriggerSpec>();
            foreach (JsonTrigger jt in parsed)
            {
                TriggerSpec spec = JsonTriggerToSpec(jt);
                TaskValidator.ValidateTrigger(spec);
                result.Add(spec);
            }
            return result;
        }

        private static TriggerSpec BuildSingleTrigger(ParsedArgs p, string type)
        {
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
                case "idle":
                    spec.Kind = TriggerKind.Idle;
                    spec.IdleSettings = BuildIdleSettings(p);
                    break;
                case "event":
                    spec.Kind = TriggerKind.Event;
                    ApplyEventParams(p, spec);
                    break;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型 '" + type + "'");
            }

            return spec;
        }

        private static IdleSettingsSpec BuildIdleSettings(ParsedArgs p)
        {
            var s = new IdleSettingsSpec
            {
                StopOnIdleEnd = p.Has("idle-stop-on-end"),
                RestartOnIdle = p.Has("idle-restart")
            };
            string wait = p.Get("idle-wait");
            if (wait != null)
            {
                int minutes;
                if (!int.TryParse(wait, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "idle-wait 格式错误 '" + wait + "'，应为非负整数分钟");
                s.WaitTimeout = TimeSpan.FromMinutes(minutes);
            }
            return s;
        }

        private static void ApplyEventParams(ParsedArgs p, TriggerSpec spec)
        {
            spec.EventSubscription = p.Get("event-subscription");
            spec.EventLog = p.Get("event-log");
            spec.EventSource = p.Get("event-source");
            string eid = p.Get("event-id");
            if (eid != null)
            {
                int id;
                if (!int.TryParse(eid, out id))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "event-id 格式错误 '" + eid + "'");
                spec.EventId = id;
            }
        }

        private static void ApplyAdvancedConditions(TaskSpec spec, ParsedArgs p)
        {
            string etl = p.Get("execution-time-limit");
            if (etl != null)
            {
                int minutes;
                if (!int.TryParse(etl, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "execution-time-limit 格式错误 '" + etl + "'，应为非负整数分钟");
                spec.ExecutionTimeLimit = minutes == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
            }
            spec.DisallowStartIfOnBatteries = p.Has("no-start-on-battery");
            spec.StopIfGoingOnBatteries = p.Has("stop-on-battery");
        }

        private static void ApplyAdvancedConditionsUpdate(TaskUpdate update, ParsedArgs p)
        {
            string etl = p.Get("execution-time-limit");
            if (etl != null)
            {
                int minutes;
                if (!int.TryParse(etl, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "execution-time-limit 格式错误 '" + etl + "'，应为非负整数分钟");
                update.ExecutionTimeLimit = minutes == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
            }
            if (p.Has("no-start-on-battery")) update.DisallowStartIfOnBatteries = true;
            if (p.Has("stop-on-battery")) update.StopIfGoingOnBatteries = true;
        }

        private static TriggerSpec JsonTriggerToSpec(JsonTrigger jt)
        {
            var spec = new TriggerSpec();
            if (jt == null || string.IsNullOrEmpty(jt.kind))
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "触发器缺少 kind 字段");

            switch (jt.kind.ToLowerInvariant())
            {
                case "once": spec.Kind = TriggerKind.Once; break;
                case "daily": spec.Kind = TriggerKind.Daily; break;
                case "weekly": spec.Kind = TriggerKind.Weekly; break;
                case "monthly": spec.Kind = TriggerKind.Monthly; break;
                case "boot": spec.Kind = TriggerKind.Boot; break;
                case "logon": spec.Kind = TriggerKind.Logon; break;
                case "idle": spec.Kind = TriggerKind.Idle; break;
                case "event": spec.Kind = TriggerKind.Event; break;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "未知触发器 kind: " + jt.kind);
            }

            if (!string.IsNullOrEmpty(jt.time))
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(jt.time, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "time 格式错误 '" + jt.time + "'");
                spec.Time = time;
            }
            if (!string.IsNullOrEmpty(jt.date))
            {
                DateTime date;
                if (!DateTime.TryParseExact(jt.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "date 格式错误 '" + jt.date + "'");
                spec.Date = date;
            }
            if (jt.days != null)
                spec.Days = jt.days.Select(ParseDay).ToArray();
            if (jt.dayOfMonth.HasValue) spec.DayOfMonth = jt.dayOfMonth.Value;

            if (jt.idleSettings != null)
            {
                spec.IdleSettings = new IdleSettingsSpec
                {
                    StopOnIdleEnd = jt.idleSettings.stopOnIdleEnd,
                    RestartOnIdle = jt.idleSettings.restartOnIdle
                };
                if (!string.IsNullOrEmpty(jt.idleSettings.waitTimeout))
                {
                    TimeSpan w;
                    if (!TimeSpan.TryParse(jt.idleSettings.waitTimeout, out w))
                        throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "idleSettings.waitTimeout 格式错误");
                    spec.IdleSettings.WaitTimeout = w;
                }
            }

            spec.EventSubscription = jt.eventSubscription;
            spec.EventLog = jt.eventLog;
            spec.EventSource = jt.eventSource;
            if (jt.eventId.HasValue) spec.EventId = jt.eventId;

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

        private sealed class JsonTrigger
        {
            public string kind { get; set; }
            public string time { get; set; }
            public string date { get; set; }
            public string[] days { get; set; }
            public int? dayOfMonth { get; set; }
            public JsonIdleSettings idleSettings { get; set; }
            public string eventSubscription { get; set; }
            public string eventLog { get; set; }
            public string eventSource { get; set; }
            public int? eventId { get; set; }
        }

        private sealed class JsonIdleSettings
        {
            public string waitTimeout { get; set; }
            public bool stopOnIdleEnd { get; set; }
            public bool restartOnIdle { get; set; }
        }
    }
}
