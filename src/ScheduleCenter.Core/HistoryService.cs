using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Xml.Linq;

namespace ScheduleCenter.Core
{
    public sealed class HistoryService
    {
        public const string LogName = "Microsoft-Windows-TaskScheduler/Operational";

        public bool IsLogEnabled()
        {
            try
            {
                using (var config = new EventLogConfiguration(LogName))
                    return config.IsEnabled;
            }
            catch (Exception)
            {
                return true; // 无法判断时不误报
            }
        }

        public IReadOnlyList<HistoryEvent> GetHistory(string taskFullPath, int? last, bool errorsOnly)
        {
            EnsureLogEnabled();
            int limit = last ?? 50;
            string queryText = "*[EventData[Data[@Name='TaskName']='" + taskFullPath + "']]";
            var query = new EventLogQuery(LogName, PathType.LogName, queryText) { ReverseDirection = true };

            var results = new List<HistoryEvent>();
            try
            {
                using (var reader = new EventLogReader(query))
                {
                    EventRecord record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            HistoryEvent ev = ToHistoryEvent(record);
                            bool isError = ev.Type == "startFailed" || ev.Type == "actionFailed";
                            if (errorsOnly && !isError)
                                continue;
                            results.Add(ev);
                            if (results.Count >= limit) break;
                        }
                    }
                }
            }
            catch (EventLogException ex)
            {
                throw new TaskServiceException(ErrorCode.HistoryDisabled,
                    "任务历史记录不可用，可能未启用。请在任务计划程序中启用\"所有任务历史记录\"", ex);
            }
            return results;
        }

        private void EnsureLogEnabled()
        {
            try
            {
                using (var config = new EventLogConfiguration(LogName))
                {
                    if (!config.IsEnabled)
                        throw new TaskServiceException(ErrorCode.HistoryDisabled,
                            "任务历史记录未启用。请在任务计划程序中启用\"所有任务历史记录\"");
                }
            }
            catch (EventLogNotFoundException)
            {
                throw new TaskServiceException(ErrorCode.HistoryDisabled, "任务历史事件日志不存在");
            }
        }

        private static HistoryEvent ToHistoryEvent(EventRecord record)
        {
            string message;
            try { message = record.FormatDescription() ?? ""; }
            catch (Exception) { message = ""; }

            return new HistoryEvent
            {
                Time = record.TimeCreated ?? DateTime.MinValue,
                Type = MapType(record.Id),
                ResultCode = TryGetResultCode(record),
                Message = message
            };
        }

        private static string MapType(int eventId)
        {
            switch (eventId)
            {
                case 107: return "triggered";
                case 100: return "started";
                case 200: return "actionStarted";
                case 201: return "actionCompleted";
                case 102: return "completed";
                case 101: return "startFailed";
                case 203: return "actionFailed";
                case 111: return "terminated";
                default: return "other";
            }
        }

        private static int? TryGetResultCode(EventRecord record)
        {
            if (record.Id != 201 && record.Id != 203) return null;
            try
            {
                XElement xml = XElement.Parse(record.ToXml());
                XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                XElement data = xml.Descendants(ns + "Data")
                    .FirstOrDefault(d => (string)d.Attribute("Name") == "ResultCode");
                int code;
                if (data != null && int.TryParse(data.Value, out code)) return code;
            }
            catch (Exception) { }
            return null;
        }
    }
}
