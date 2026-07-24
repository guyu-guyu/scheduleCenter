using System;
using System.Collections.Generic;

namespace ScheduleCenter.Core
{
    public enum TriggerKind { Once, Daily, Weekly, Monthly, Boot, Logon, Idle, Event }

    public sealed class IdleSettingsSpec
    {
        public TimeSpan? WaitTimeout { get; set; }
        public bool StopOnIdleEnd { get; set; }
        public bool RestartOnIdle { get; set; }
    }

    public sealed class TriggerSpec
    {
        public TriggerKind Kind { get; set; }
        public TimeSpan? Time { get; set; }          // once/daily/weekly/monthly
        public DateTime? Date { get; set; }          // once
        public DayOfWeek[] Days { get; set; }        // weekly
        public int? DayOfMonth { get; set; }         // monthly

        // V2: Idle 触发器
        public IdleSettingsSpec IdleSettings { get; set; }

        // V2: Event 触发器
        public string EventSubscription { get; set; }  // 完整 XPath（优先级高）
        public string EventLog { get; set; }           // 简化参数
        public string EventSource { get; set; }        // 简化参数（可选）
        public int? EventId { get; set; }              // 简化参数（可选）
    }

    public sealed class TaskSpec
    {
        public string Name { get; set; }              // 可含子路径 "MyApp\Backup"，相对 \ScheduleCenter\
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public IList<TriggerSpec> Triggers { get; set; }
        public bool Enabled { get; set; }
        public bool RunAsSystem { get; set; }
        public bool Highest { get; set; }
        public bool StartWhenAvailable { get; set; }

        // V2: 高级条件
        public TimeSpan? ExecutionTimeLimit { get; set; }   // null = 无限制
        public bool DisallowStartIfOnBatteries { get; set; }
        public bool StopIfGoingOnBatteries { get; set; }

        public TaskSpec()
        {
            Enabled = true;
            StartWhenAvailable = true;
        }
    }

    public sealed class TaskUpdate
    {
        public string Name { get; set; }              // 定位用，必填
        public string Path { get; set; }              // null = 不修改（下同）
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public IList<TriggerSpec> Triggers { get; set; }    // null = 不修改
        public bool? Enabled { get; set; }
        public bool? RunAsSystem { get; set; }
        public bool? Highest { get; set; }
        public bool? StartWhenAvailable { get; set; }

        // V2: 高级条件（null = 不修改）
        public TimeSpan? ExecutionTimeLimit { get; set; }
        public bool? DisallowStartIfOnBatteries { get; set; }
        public bool? StopIfGoingOnBatteries { get; set; }
    }

    public sealed class TaskInfo
    {
        public string Name { get; set; }              // 叶子名
        public string RelativeName { get; set; }      // 相对 \ScheduleCenter\ 的路径
        public string Folder { get; set; }            // 完整文件夹路径
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public string State { get; set; }
        public bool RunAsSystem { get; set; }
        public bool Highest { get; set; }
        public IList<TriggerSpec> Triggers { get; set; }
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public int LastResult { get; set; }

        // V2: 高级条件
        public TimeSpan? ExecutionTimeLimit { get; set; }
        public bool DisallowStartIfOnBatteries { get; set; }
        public bool StopIfGoingOnBatteries { get; set; }
    }

    public sealed class HistoryEvent
    {
        public DateTime Time { get; set; }
        public string Type { get; set; }          // triggered/started/actionStarted/actionCompleted/completed/startFailed/actionFailed/terminated/other
        public int? ResultCode { get; set; }
        public string Message { get; set; }
    }
}
