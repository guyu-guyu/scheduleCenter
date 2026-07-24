using System;
using System.Globalization;
using System.Windows.Data;
using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class TriggerSummaryConverter : IValueConverter
    {
        public static readonly TriggerSummaryConverter Instance = new TriggerSummaryConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TriggerSpec t = value as TriggerSpec;
            if (t == null) return "";
            switch (t.Kind)
            {
                case TriggerKind.Once:
                    return "一次性 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "") +
                           (t.Date.HasValue ? " " + t.Date.Value.ToString("yyyy-MM-dd") : "");
                case TriggerKind.Daily:
                    return "每日 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "");
                case TriggerKind.Weekly:
                    return "每周 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "");
                case TriggerKind.Monthly:
                    return "每月" + (t.DayOfMonth.HasValue ? t.DayOfMonth.Value.ToString() : "?") + "日 " +
                           (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "");
                case TriggerKind.Boot: return "开机时";
                case TriggerKind.Logon: return "登录时";
                case TriggerKind.Idle: return "空闲时";
                case TriggerKind.Event:
                    return "事件 " + (t.EventLog ?? "") + (t.EventId.HasValue ? "/" + t.EventId.Value : "");
                default: return t.Kind.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
