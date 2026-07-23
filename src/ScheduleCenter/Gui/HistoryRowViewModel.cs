using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class HistoryRowViewModel
    {
        public HistoryEvent Event { get; private set; }

        public HistoryRowViewModel(HistoryEvent ev)
        {
            Event = ev;
        }

        public string TimeText { get { return Event.Time.ToString("yyyy-MM-dd HH:mm:ss"); } }
        public string TypeText
        {
            get
            {
                switch (Event.Type)
                {
                    case "triggered": return "已触发";
                    case "started": return "已启动";
                    case "actionStarted": return "操作已启动";
                    case "actionCompleted": return "操作已完成";
                    case "completed": return "已完成";
                    case "startFailed": return "启动失败";
                    case "actionFailed": return "操作失败";
                    case "terminated": return "已终止";
                    default: return "其他";
                }
            }
        }
        public string ResultCodeText { get { return Event.ResultCode.HasValue ? "0x" + Event.ResultCode.Value.ToString("X8") : "-"; } }
        public string Message { get { return Event.Message; } }
        public bool IsError { get { return Event.Type == "startFailed" || Event.Type == "actionFailed"; } }
        public bool IsCompleted { get { return Event.Type == "completed" || Event.Type == "actionCompleted"; } }
    }
}
