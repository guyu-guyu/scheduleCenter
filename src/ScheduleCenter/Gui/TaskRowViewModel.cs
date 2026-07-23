using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class TaskRowViewModel
    {
        public TaskInfo Info { get; private set; }

        public TaskRowViewModel(TaskInfo info)
        {
            Info = info;
        }

        public string Name { get { return Info.RelativeName; } }
        public string State { get { return Info.Enabled ? Info.State : "已禁用"; } }
        public string NextRunTimeText { get { return Info.NextRunTime.HasValue ? Info.NextRunTime.Value.ToString("yyyy-MM-dd HH:mm") : "-"; } }
        public string LastRunTimeText { get { return Info.LastRunTime.HasValue ? Info.LastRunTime.Value.ToString("yyyy-MM-dd HH:mm") : "-"; } }
        public string LastResultText { get { return Info.LastRunTime.HasValue ? "0x" + Info.LastResult.ToString("X8") : "-"; } }
    }
}
