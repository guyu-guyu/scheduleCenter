using System;
using System.Collections.Generic;
using System.IO;

namespace ScheduleCenter.Core
{
    public static class TaskValidator
    {
        private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '|', '?', '*', '\'' };

        public static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称不能为空");

            string[] segments = name.Split('\\');
            foreach (string seg in segments)
            {
                if (seg.Length == 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称 '" + name + "' 包含空的路径段");
                if (seg != seg.Trim())
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 首尾不能有空格");
                if (seg == "." || seg == "..")
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 非法");
                if (seg.IndexOfAny(InvalidChars) >= 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 包含非法字符");
            }
        }

        public static void ValidateSpec(TaskSpec spec)
        {
            if (spec == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "任务定义不能为空");
            ValidateName(spec.Name);
            if (string.IsNullOrWhiteSpace(spec.Path))
                throw new TaskServiceException(ErrorCode.InvalidArguments, "程序路径不能为空");
            if (!File.Exists(spec.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + spec.Path);
            if (spec.Triggers == null || spec.Triggers.Count == 0)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "至少需要一个触发器");
            foreach (TriggerSpec t in spec.Triggers)
                ValidateTrigger(t);
            ValidateExecutionTimeLimit(spec.ExecutionTimeLimit);
        }

        public static void ValidateUpdate(TaskUpdate update)
        {
            ValidateName(update.Name);
            if (update.Path != null && !File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            if (update.Triggers != null)
            {
                if (update.Triggers.Count == 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "触发器列表不能为空");
                foreach (TriggerSpec t in update.Triggers)
                    ValidateTrigger(t);
            }
            ValidateExecutionTimeLimit(update.ExecutionTimeLimit);

            if (update.Path == null && update.Arguments == null && update.WorkingDirectory == null &&
                update.Description == null && update.Triggers == null && update.RunAsSystem == null &&
                update.Highest == null && update.Enabled == null && update.StartWhenAvailable == null &&
                update.ExecutionTimeLimit == null && update.DisallowStartIfOnBatteries == null &&
                update.StopIfGoingOnBatteries == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "update 命令至少需要一个要修改的选项");
        }

        public static void ValidateTrigger(TriggerSpec t)
        {
            if (t == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少触发器定义");

            switch (t.Kind)
            {
                case TriggerKind.Once:
                    RequireTime(t, "once");
                    if (!t.Date.HasValue)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "once 触发器需要日期");
                    if (t.Date.Value.Date + t.Time.Value < DateTime.Now)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "once 触发器的日期时间不能早于当前时间");
                    break;
                case TriggerKind.Daily:
                    RequireTime(t, "daily");
                    break;
                case TriggerKind.Weekly:
                    RequireTime(t, "weekly");
                    if (t.Days == null || t.Days.Length == 0)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "weekly 触发器需要至少一个星期值");
                    break;
                case TriggerKind.Monthly:
                    RequireTime(t, "monthly");
                    if (!t.DayOfMonth.HasValue || t.DayOfMonth.Value < 1 || t.DayOfMonth.Value > 31)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "monthly 触发器的 day-of-month 必须在 1-31 之间");
                    break;
                case TriggerKind.Boot:
                case TriggerKind.Logon:
                case TriggerKind.Idle:
                    break;
                case TriggerKind.Event:
                    if (string.IsNullOrWhiteSpace(t.EventSubscription) && string.IsNullOrWhiteSpace(t.EventLog))
                        throw new TaskServiceException(ErrorCode.InvalidEventSubscription,
                            "event 触发器需要 --event-log 或 --event-subscription");
                    break;
            }
        }

        public static void ValidateExecutionTimeLimit(TimeSpan? limit)
        {
            if (limit.HasValue && limit.Value < TimeSpan.Zero)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "执行时间限制不能为负数");
        }

        private static void RequireTime(TriggerSpec t, string kindName)
        {
            if (!t.Time.HasValue)
                throw new TaskServiceException(ErrorCode.InvalidArguments, kindName + " 触发器需要时间参数");
        }
    }
}
