using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class EditorViewModel : ViewModelBase
    {
        private static readonly string[] TriggerTypeNames =
            { "一次性", "每日", "每周", "每月", "开机时", "登录时" };

        private readonly ScheduledTaskService _service;
        private readonly bool _isEdit;

        public event Action<bool> RequestClose;

        public EditorViewModel(ScheduledTaskService service, TaskInfo existing)
        {
            _service = service;
            _isEdit = existing != null;
            _enabled = true;

            if (existing != null)
            {
                _name = existing.RelativeName;
                _description = existing.Description ?? "";
                _path = existing.Path ?? "";
                _arguments = existing.Arguments ?? "";
                _workingDirectory = existing.WorkingDirectory ?? "";
                _runAsSystem = existing.RunAsSystem;
                _highest = existing.Highest;
                _enabled = existing.Enabled;
                LoadTrigger(existing.Trigger);
            }

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(delegate { RaiseClose(false); });
        }

        private void LoadTrigger(TriggerSpec t)
        {
            if (t == null) return;
            _triggerTypeIndex = (int)t.Kind;
            if (t.Time.HasValue) _timeText = t.Time.Value.ToString(@"hh\:mm");
            if (t.Date.HasValue) _date = t.Date.Value;
            if (t.DayOfMonth.HasValue) _dayOfMonthText = t.DayOfMonth.Value.ToString();
            if (t.Days != null)
            {
                foreach (DayOfWeek d in t.Days)
                {
                    switch (d)
                    {
                        case DayOfWeek.Monday: _mon = true; break;
                        case DayOfWeek.Tuesday: _tue = true; break;
                        case DayOfWeek.Wednesday: _wed = true; break;
                        case DayOfWeek.Thursday: _thu = true; break;
                        case DayOfWeek.Friday: _fri = true; break;
                        case DayOfWeek.Saturday: _sat = true; break;
                        case DayOfWeek.Sunday: _sun = true; break;
                    }
                }
            }
        }

        // ---- 常规 ----
        private string _name = "";
        public string Name { get { return _name; } set { Set(ref _name, value); } }
        public bool IsNameEditable { get { return !_isEdit; } }
        public string Title { get { return _isEdit ? "编辑任务" : "新建任务"; } }

        private string _description = "";
        public string Description { get { return _description; } set { Set(ref _description, value); } }

        private string _path = "";
        public string Path { get { return _path; } set { Set(ref _path, value); } }

        private string _arguments = "";
        public string Arguments { get { return _arguments; } set { Set(ref _arguments, value); } }

        private string _workingDirectory = "";
        public string WorkingDirectory { get { return _workingDirectory; } set { Set(ref _workingDirectory, value); } }

        private bool _runAsSystem;
        public bool RunAsSystem { get { return _runAsSystem; } set { Set(ref _runAsSystem, value); } }

        private bool _highest;
        public bool Highest { get { return _highest; } set { Set(ref _highest, value); } }

        private bool _enabled;
        public bool Enabled { get { return _enabled; } set { Set(ref _enabled, value); } }

        // ---- 触发器 ----
        public string[] TriggerTypes { get { return TriggerTypeNames; } }

        private int _triggerTypeIndex;
        public int TriggerTypeIndex
        {
            get { return _triggerTypeIndex; }
            set
            {
                if (Set(ref _triggerTypeIndex, value))
                {
                    OnPropertyChanged("ShowTime"); OnPropertyChanged("ShowDate");
                    OnPropertyChanged("ShowWeekdays"); OnPropertyChanged("ShowDayOfMonth");
                }
            }
        }

        public Visibility ShowTime { get { return _triggerTypeIndex <= 3 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowDate { get { return _triggerTypeIndex == 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowWeekdays { get { return _triggerTypeIndex == 2 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowDayOfMonth { get { return _triggerTypeIndex == 3 ? Visibility.Visible : Visibility.Collapsed; } }

        private string _timeText = "09:00";
        public string TimeText { get { return _timeText; } set { Set(ref _timeText, value); } }

        private DateTime? _date = DateTime.Today.AddDays(1);
        public DateTime? Date { get { return _date; } set { Set(ref _date, value); } }

        private string _dayOfMonthText = "1";
        public string DayOfMonthText { get { return _dayOfMonthText; } set { Set(ref _dayOfMonthText, value); } }

        private bool _mon, _tue, _wed, _thu, _fri, _sat, _sun;
        public bool Mon { get { return _mon; } set { Set(ref _mon, value); } }
        public bool Tue { get { return _tue; } set { Set(ref _tue, value); } }
        public bool Wed { get { return _wed; } set { Set(ref _wed, value); } }
        public bool Thu { get { return _thu; } set { Set(ref _thu, value); } }
        public bool Fri { get { return _fri; } set { Set(ref _fri, value); } }
        public bool Sat { get { return _sat; } set { Set(ref _sat, value); } }
        public bool Sun { get { return _sun; } set { Set(ref _sun, value); } }

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private TriggerSpec BuildTrigger()
        {
            var spec = new TriggerSpec { Kind = (TriggerKind)_triggerTypeIndex };
            if (_triggerTypeIndex <= 3)
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(_timeText, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "时间格式错误，应为 HH:mm");
                spec.Time = time;
            }
            if (_triggerTypeIndex == 0)
            {
                if (!_date.HasValue)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "请选择日期");
                spec.Date = _date.Value;
            }
            if (_triggerTypeIndex == 2)
            {
                var days = new List<DayOfWeek>();
                if (_mon) days.Add(DayOfWeek.Monday);
                if (_tue) days.Add(DayOfWeek.Tuesday);
                if (_wed) days.Add(DayOfWeek.Wednesday);
                if (_thu) days.Add(DayOfWeek.Thursday);
                if (_fri) days.Add(DayOfWeek.Friday);
                if (_sat) days.Add(DayOfWeek.Saturday);
                if (_sun) days.Add(DayOfWeek.Sunday);
                spec.Days = days.ToArray();
            }
            if (_triggerTypeIndex == 3)
            {
                int dom;
                if (!int.TryParse(_dayOfMonthText, out dom))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "每月第几天必须为数字");
                spec.DayOfMonth = dom;
            }
            return spec;
        }

        private void Save()
        {
            try
            {
                TriggerSpec trigger = BuildTrigger();
                if (_isEdit)
                {
                    _service.Update(new TaskUpdate
                    {
                        Name = _name,
                        Path = _path,
                        Arguments = string.IsNullOrEmpty(_arguments) ? null : _arguments,
                        WorkingDirectory = string.IsNullOrEmpty(_workingDirectory) ? null : _workingDirectory,
                        Description = _description,
                        Trigger = trigger,
                        RunAsSystem = _runAsSystem,
                        Highest = _highest,
                        Enabled = _enabled
                    });
                }
                else
                {
                    _service.Add(new TaskSpec
                    {
                        Name = _name,
                        Path = _path,
                        Arguments = string.IsNullOrEmpty(_arguments) ? null : _arguments,
                        WorkingDirectory = string.IsNullOrEmpty(_workingDirectory) ? null : _workingDirectory,
                        Description = _description,
                        Trigger = trigger,
                        RunAsSystem = _runAsSystem,
                        Highest = _highest,
                        Enabled = _enabled
                    });
                }
                RaiseClose(true);
            }
            catch (TaskServiceException ex)
            {
                MainViewModel.ShowError(ex);
            }
        }

        private void RaiseClose(bool saved)
        {
            Action<bool> handler = RequestClose;
            if (handler != null) handler(saved);
        }
    }
}
