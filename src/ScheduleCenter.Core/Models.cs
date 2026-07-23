using System;

namespace ScheduleCenter.Core
{
    public enum TriggerKind { Once, Daily, Weekly, Monthly, Boot, Logon }

    public sealed class TriggerSpec
    {
        public TriggerKind Kind { get; set; }
        public TimeSpan? Time { get; set; }       // once/daily/weekly/monthly
        public DateTime? Date { get; set; }       // once
        public DayOfWeek[] Days { get; set; }     // weekly
        public int? DayOfMonth { get; set; }      // monthly
    }

    public sealed class TaskSpec
    {
        public string Name { get; set; }          // 可含子路径 "MyApp\Backup"，相对 \ScheduleCenter\
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public TriggerSpec Trigger { get; set; }
        public bool Enabled { get; set; }
        public bool RunAsSystem { get; set; }
        public bool Highest { get; set; }
        public bool StartWhenAvailable { get; set; }

        public TaskSpec()
        {
            Enabled = true;
            StartWhenAvailable = true;
        }
    }

    public sealed class TaskUpdate
    {
        public string Name { get; set; }          // 定位用，必填
        public string Path { get; set; }          // null = 不修改（下同）
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public TriggerSpec Trigger { get; set; }
        public bool? Enabled { get; set; }
        public bool? RunAsSystem { get; set; }
        public bool? Highest { get; set; }
        public bool? StartWhenAvailable { get; set; }
    }

    public sealed class TaskInfo
    {
        public string Name { get; set; }          // 叶子名
        public string RelativeName { get; set; }  // 相对 \ScheduleCenter\ 的路径
        public string Folder { get; set; }        // 完整文件夹路径
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public string State { get; set; }
        public bool RunAsSystem { get; set; }
        public bool Highest { get; set; }
        public TriggerSpec Trigger { get; set; }
        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
        public int LastResult { get; set; }
    }

    public sealed class HistoryEvent
    {
        public DateTime Time { get; set; }
        public string Type { get; set; }          // triggered/started/actionStarted/actionCompleted/completed/startFailed/actionFailed/terminated/other
        public int? ResultCode { get; set; }
        public string Message { get; set; }
    }
}
