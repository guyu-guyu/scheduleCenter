using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class EditorViewModel : ViewModelBase
    {
        private static readonly string[] TriggerTypeNames =
            { "一次性", "每日", "每周", "每月", "开机时", "登录时", "空闲时", "事件" };

        private readonly ScheduledTaskService _service;
        private readonly bool _isEdit;

        public event Action<bool> RequestClose;

        public ObservableCollection<TriggerSpec> Triggers { get; private set; }
        private int _selectedTriggerIndex = -1;
        public int SelectedTriggerIndex
        {
            get { return _selectedTriggerIndex; }
            set
            {
                if (Set(ref _selectedTriggerIndex, value))
                {
                    LoadTriggerFromList();
                    OnPropertyChanged("CanDeleteTrigger");
                    OnPropertyChanged("IsTriggerListSelected");
                }
            }
        }

        public bool CanDeleteTrigger { get { return Triggers.Count > 1; } }
        public bool IsTriggerListSelected { get { return _selectedTriggerIndex >= 0 && _selectedTriggerIndex < Triggers.Count; } }

        public EditorViewModel(ScheduledTaskService service, TaskInfo existing)
        {
            _service = service;
            _isEdit = existing != null;
            _enabled = true;
            Triggers = new ObservableCollection<TriggerSpec>();

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
                _executionTimeLimitText = existing.ExecutionTimeLimit.HasValue
                    ? ((int)existing.ExecutionTimeLimit.Value.TotalMinutes).ToString()
                    : "0";
                _disallowStartIfOnBatteries = existing.DisallowStartIfOnBatteries;
                _stopIfGoingOnBatteries = existing.StopIfGoingOnBatteries;

                if (existing.Triggers != null)
                {
                    foreach (TriggerSpec t in existing.Triggers)
                        Triggers.Add(CloneTrigger(t));
                }
            }
            if (Triggers.Count == 0)
            {
                Triggers.Add(new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) });
            }
            SelectedTriggerIndex = 0;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(delegate { RaiseClose(false); });
            AddTriggerCommand = new RelayCommand(AddTrigger);
            RemoveTriggerCommand = new RelayCommand(RemoveSelectedTrigger, () => CanDeleteTrigger);
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

        // ---- 高级条件 ----
        private string _executionTimeLimitText = "0";
        public string ExecutionTimeLimitText { get { return _executionTimeLimitText; } set { Set(ref _executionTimeLimitText, value); } }

        private bool _disallowStartIfOnBatteries;
        public bool DisallowStartIfOnBatteries { get { return _disallowStartIfOnBatteries; } set { Set(ref _disallowStartIfOnBatteries, value); } }

        private bool _stopIfGoingOnBatteries;
        public bool StopIfGoingOnBatteries { get { return _stopIfGoingOnBatteries; } set { Set(ref _stopIfGoingOnBatteries, value); } }

        // ---- 触发器编辑面板（绑定到选中项）----
        public string[] TriggerTypes { get { return TriggerTypeNames; } }

        private int _triggerTypeIndex;
        public int TriggerTypeIndex
        {
            get { return _triggerTypeIndex; }
            set
            {
                if (Set(ref _triggerTypeIndex, value))
                {
                    UpdateSelectedTriggerKind();
                    OnPropertyChanged("ShowTime");
                    OnPropertyChanged("ShowDate");
                    OnPropertyChanged("ShowWeekdays");
                    OnPropertyChanged("ShowDayOfMonth");
                    OnPropertyChanged("ShowIdle");
                    OnPropertyChanged("ShowEvent");
                }
            }
        }

        public Visibility ShowTime { get { return _triggerTypeIndex <= 3 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowDate { get { return _triggerTypeIndex == 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowWeekdays { get { return _triggerTypeIndex == 2 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowDayOfMonth { get { return _triggerTypeIndex == 3 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowIdle { get { return _triggerTypeIndex == 6 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ShowEvent { get { return _triggerTypeIndex == 7 ? Visibility.Visible : Visibility.Collapsed; } }

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

        // Idle 设置
        private string _idleWaitText = "60";
        public string IdleWaitText { get { return _idleWaitText; } set { Set(ref _idleWaitText, value); } }
        private bool _idleStopOnEnd = true;
        public bool IdleStopOnEnd { get { return _idleStopOnEnd; } set { Set(ref _idleStopOnEnd, value); } }
        private bool _idleRestart;
        public bool IdleRestart { get { return _idleRestart; } set { Set(ref _idleRestart, value); } }

        // Event 设置
        private bool _useEventSubscription;
        public bool UseEventSubscription
        {
            get { return _useEventSubscription; }
            set { if (Set(ref _useEventSubscription, value)) { OnPropertyChanged("ShowEventSimple"); OnPropertyChanged("ShowEventXPath"); } }
        }
        public Visibility ShowEventSimple { get { return _useEventSubscription ? Visibility.Collapsed : Visibility.Visible; } }
        public Visibility ShowEventXPath { get { return _useEventSubscription ? Visibility.Visible : Visibility.Collapsed; } }

        private string _eventLog = "";
        public string EventLog { get { return _eventLog; } set { Set(ref _eventLog, value); } }
        private string _eventSource = "";
        public string EventSource { get { return _eventSource; } set { Set(ref _eventSource, value); } }
        private string _eventIdText = "";
        public string EventIdText { get { return _eventIdText; } set { Set(ref _eventIdText, value); } }
        private string _eventSubscription = "";
        public string EventSubscription { get { return _eventSubscription; } set { Set(ref _eventSubscription, value); } }

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand AddTriggerCommand { get; private set; }
        public ICommand RemoveTriggerCommand { get; private set; }

        private void AddTrigger()
        {
            var t = new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) };
            Triggers.Add(t);
            SelectedTriggerIndex = Triggers.Count - 1;
            OnPropertyChanged("CanDeleteTrigger");
        }

        private void RemoveSelectedTrigger()
        {
            if (!CanDeleteTrigger || _selectedTriggerIndex < 0 || _selectedTriggerIndex >= Triggers.Count) return;
            Triggers.RemoveAt(_selectedTriggerIndex);
            if (_selectedTriggerIndex >= Triggers.Count)
                SelectedTriggerIndex = Triggers.Count - 1;
            OnPropertyChanged("CanDeleteTrigger");
        }

        private void LoadTriggerFromList()
        {
            if (_selectedTriggerIndex < 0 || _selectedTriggerIndex >= Triggers.Count) return;
            TriggerSpec t = Triggers[_selectedTriggerIndex];
            _triggerTypeIndex = (int)t.Kind;
            OnPropertyChanged("TriggerTypeIndex");

            if (t.Time.HasValue) _timeText = t.Time.Value.ToString(@"hh\:mm"); else _timeText = "09:00";
            OnPropertyChanged("TimeText");

            _date = t.Date ?? DateTime.Today.AddDays(1);
            OnPropertyChanged("Date");

            _dayOfMonthText = t.DayOfMonth.HasValue ? t.DayOfMonth.Value.ToString() : "1";
            OnPropertyChanged("DayOfMonthText");

            _mon = _tue = _wed = _thu = _fri = _sat = _sun = false;
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
            OnPropertyChanged("Mon"); OnPropertyChanged("Tue"); OnPropertyChanged("Wed");
            OnPropertyChanged("Thu"); OnPropertyChanged("Fri"); OnPropertyChanged("Sat"); OnPropertyChanged("Sun");

            // Idle
            if (t.IdleSettings != null)
            {
                _idleWaitText = t.IdleSettings.WaitTimeout.HasValue ? ((int)t.IdleSettings.WaitTimeout.Value.TotalMinutes).ToString() : "60";
                _idleStopOnEnd = t.IdleSettings.StopOnIdleEnd;
                _idleRestart = t.IdleSettings.RestartOnIdle;
            }
            else
            {
                _idleWaitText = "60"; _idleStopOnEnd = true; _idleRestart = false;
            }
            OnPropertyChanged("IdleWaitText");
            OnPropertyChanged("IdleStopOnEnd"); OnPropertyChanged("IdleRestart");

            // Event
            _useEventSubscription = !string.IsNullOrEmpty(t.EventSubscription);
            OnPropertyChanged("UseEventSubscription");
            _eventLog = t.EventLog ?? "";
            _eventSource = t.EventSource ?? "";
            _eventIdText = t.EventId.HasValue ? t.EventId.Value.ToString() : "";
            _eventSubscription = t.EventSubscription ?? "";
            OnPropertyChanged("EventLog"); OnPropertyChanged("EventSource");
            OnPropertyChanged("EventIdText"); OnPropertyChanged("EventSubscription");

            OnPropertyChanged("ShowTime"); OnPropertyChanged("ShowDate");
            OnPropertyChanged("ShowWeekdays"); OnPropertyChanged("ShowDayOfMonth");
            OnPropertyChanged("ShowIdle"); OnPropertyChanged("ShowEvent");
        }

        private void UpdateSelectedTriggerKind()
        {
            if (_selectedTriggerIndex < 0 || _selectedTriggerIndex >= Triggers.Count) return;
            TriggerSpec t = Triggers[_selectedTriggerIndex];
            t.Kind = (TriggerKind)_triggerTypeIndex;
        }

        private void SyncPanelBackToList()
        {
            if (_selectedTriggerIndex < 0 || _selectedTriggerIndex >= Triggers.Count) return;
            TriggerSpec t = Triggers[_selectedTriggerIndex];
            t.Kind = (TriggerKind)_triggerTypeIndex;

            if (_triggerTypeIndex <= 3)
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(_timeText, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "时间格式错误，应为 HH:mm");
                t.Time = time;
            }
            else t.Time = null;

            if (_triggerTypeIndex == 0)
            {
                if (!_date.HasValue)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "请选择日期");
                t.Date = _date.Value;
            }
            else t.Date = null;

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
                t.Days = days.ToArray();
            }
            else t.Days = null;

            if (_triggerTypeIndex == 3)
            {
                int dom;
                if (!int.TryParse(_dayOfMonthText, out dom))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "每月第几天必须为数字");
                t.DayOfMonth = dom;
            }
            else t.DayOfMonth = null;

            if (_triggerTypeIndex == 6) // idle
            {
                int wait;
                if (!int.TryParse(_idleWaitText, out wait) || wait < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "空闲等待时间必须为非负整数");
                t.IdleSettings = new IdleSettingsSpec
                {
                    WaitTimeout = TimeSpan.FromMinutes(wait),
                    StopOnIdleEnd = _idleStopOnEnd,
                    RestartOnIdle = _idleRestart
                };
            }
            else t.IdleSettings = null;

            if (_triggerTypeIndex == 7) // event
            {
                if (_useEventSubscription)
                {
                    if (string.IsNullOrWhiteSpace(_eventSubscription))
                        throw new TaskServiceException(ErrorCode.InvalidEventSubscription, "事件 XPath 不能为空");
                    t.EventSubscription = _eventSubscription;
                    t.EventLog = null; t.EventSource = null; t.EventId = null;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(_eventLog))
                        throw new TaskServiceException(ErrorCode.InvalidEventSubscription, "事件日志名不能为空");
                    t.EventLog = _eventLog;
                    t.EventSource = string.IsNullOrEmpty(_eventSource) ? null : _eventSource;
                    int eid;
                    if (!string.IsNullOrEmpty(_eventIdText))
                    {
                        if (!int.TryParse(_eventIdText, out eid))
                            throw new TaskServiceException(ErrorCode.InvalidArguments, "事件 ID 必须为数字");
                        t.EventId = eid;
                    }
                    else t.EventId = null;
                    t.EventSubscription = null;
                }
            }
            else
            {
                t.EventSubscription = null; t.EventLog = null;
                t.EventSource = null; t.EventId = null;
            }
        }

        private static TriggerSpec CloneTrigger(TriggerSpec src)
        {
            var dst = new TriggerSpec
            {
                Kind = src.Kind,
                Time = src.Time,
                Date = src.Date,
                Days = src.Days == null ? null : (DayOfWeek[])src.Days.Clone(),
                DayOfMonth = src.DayOfMonth,
                EventSubscription = src.EventSubscription,
                EventLog = src.EventLog,
                EventSource = src.EventSource,
                EventId = src.EventId
            };
            if (src.IdleSettings != null)
            {
                dst.IdleSettings = new IdleSettingsSpec
                {
                    WaitTimeout = src.IdleSettings.WaitTimeout,
                    StopOnIdleEnd = src.IdleSettings.StopOnIdleEnd,
                    RestartOnIdle = src.IdleSettings.RestartOnIdle
                };
            }
            return dst;
        }

        private void Save()
        {
            try
            {
                // 把所有面板值同步回 Triggers 列表
                int savedIndex = _selectedTriggerIndex;
                for (int i = 0; i < Triggers.Count; i++)
                {
                    _selectedTriggerIndex = i;
                    SyncPanelBackToList();
                }
                _selectedTriggerIndex = savedIndex;
                LoadTriggerFromList();

                // 高级条件
                TimeSpan? executionTimeLimit = null;
                int etlMinutes;
                if (int.TryParse(_executionTimeLimitText, out etlMinutes) && etlMinutes > 0)
                    executionTimeLimit = TimeSpan.FromMinutes(etlMinutes);
                else
                    executionTimeLimit = TimeSpan.Zero; // 0 = 无限制

                if (_isEdit)
                {
                    _service.Update(new TaskUpdate
                    {
                        Name = _name,
                        Path = _path,
                        Arguments = string.IsNullOrEmpty(_arguments) ? null : _arguments,
                        WorkingDirectory = string.IsNullOrEmpty(_workingDirectory) ? null : _workingDirectory,
                        Description = _description,
                        Triggers = new List<TriggerSpec>(Triggers),
                        RunAsSystem = _runAsSystem,
                        Highest = _highest,
                        Enabled = _enabled,
                        ExecutionTimeLimit = executionTimeLimit,
                        DisallowStartIfOnBatteries = _disallowStartIfOnBatteries,
                        StopIfGoingOnBatteries = _stopIfGoingOnBatteries
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
                        Triggers = new List<TriggerSpec>(Triggers),
                        RunAsSystem = _runAsSystem,
                        Highest = _highest,
                        Enabled = _enabled,
                        ExecutionTimeLimit = executionTimeLimit,
                        DisallowStartIfOnBatteries = _disallowStartIfOnBatteries,
                        StopIfGoingOnBatteries = _stopIfGoingOnBatteries
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
