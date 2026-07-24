using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.TaskScheduler;

namespace ScheduleCenter.Core
{
    public sealed class ScheduledTaskService
    {
        public const string RootFolderPath = @"\ScheduleCenter";

        private readonly HistoryService _history = new HistoryService();

        // ---------- 公共 API ----------

        public TaskInfo Add(TaskSpec spec)
        {
            TaskValidator.ValidateSpec(spec);
            return Run(delegate
            {
                using (var ts = new TaskService())
                {
                    string sub, leaf;
                    SplitRelativeName(spec.Name, out sub, out leaf);
                    TaskFolder folder = GetOrCreateFolder(ts, sub);
                    using (Task existing = FindTask(ts, spec.Name))
                    {
                        if (existing != null)
                            throw new TaskServiceException(ErrorCode.TaskExists, "任务 '" + spec.Name + "' 已存在");
                    }

                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = spec.Description ?? "";
                    td.Settings.Enabled = spec.Enabled;
                    td.Settings.StartWhenAvailable = spec.StartWhenAvailable;
                    ApplyPrincipal(td, spec.RunAsSystem, spec.Highest);
                    foreach (TriggerSpec triggerSpec in spec.Triggers)
                        td.Triggers.Add(BuildTrigger(triggerSpec));
                    ApplyAdvancedSettings(td, spec);
                    ApplyIdleSettingsFromTriggers(td, spec.Triggers);
                    td.Actions.Add(new ExecAction(spec.Path, spec.Arguments, spec.WorkingDirectory));

                    Task created = RegisterTask(folder, leaf, td, spec.RunAsSystem);
                    using (created) { return ToTaskInfo(created); }
                }
            });
        }

        public TaskInfo Get(string name)
        {
            TaskValidator.ValidateName(name);
            return Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    return ToTaskInfo(task);
                }
            });
        }

        public IReadOnlyList<TaskInfo> List(string filter)
        {
            return Run(delegate
            {
                using (var ts = new TaskService())
                {
                    TaskFolder root = TryGetFolder(ts, RootFolderPath);
                    if (root == null)
                        return (IReadOnlyList<TaskInfo>)new List<TaskInfo>();

                    Regex regex = WildcardToRegex(filter);
                    var result = new List<TaskInfo>();
                    foreach (Task t in root.AllTasks)
                    {
                        using (t)
                        {
                            if (regex == null || regex.IsMatch(t.Name))
                                result.Add(ToTaskInfo(t));
                        }
                    }
                    result.Sort((a, b) => string.CompareOrdinal(a.RelativeName, b.RelativeName));
                    return (IReadOnlyList<TaskInfo>)result;
                }
            });
        }

        public TaskInfo Update(TaskUpdate update)
        {
            TaskValidator.ValidateName(update.Name);
            TaskValidator.ValidateUpdate(update);

            return Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, update.Name))
                {
                    TaskDefinition td = task.Definition;

                    if (update.Description != null) td.RegistrationInfo.Description = update.Description;
                    if (update.Highest.HasValue || update.RunAsSystem.HasValue)
                    {
                        bool runAsSystem = update.RunAsSystem ?? IsSystemPrincipal(td);
                        bool highest = update.Highest ?? (td.Principal.RunLevel == TaskRunLevel.Highest);
                        ApplyPrincipal(td, runAsSystem, highest);
                    }
                    if (update.Enabled.HasValue) td.Settings.Enabled = update.Enabled.Value;
                    if (update.StartWhenAvailable.HasValue) td.Settings.StartWhenAvailable = update.StartWhenAvailable.Value;
                    if (update.Triggers != null)
                    {
                        td.Triggers.Clear();
                        foreach (TriggerSpec triggerSpec in update.Triggers)
                            td.Triggers.Add(BuildTrigger(triggerSpec));
                        ApplyIdleSettingsFromTriggers(td, update.Triggers);
                    }
                    ApplyAdvancedSettingsUpdate(td, update);
                    if (update.Path != null || update.Arguments != null || update.WorkingDirectory != null)
                    {
                        ExecAction exec = td.Actions.OfType<ExecAction>().FirstOrDefault();
                        string path = update.Path ?? (exec != null ? exec.Path : null);
                        string args = update.Arguments ?? (exec != null ? exec.Arguments : null);
                        string wd = update.WorkingDirectory ?? (exec != null ? exec.WorkingDirectory : null);
                        td.Actions.Clear();
                        td.Actions.Add(new ExecAction(path, args, wd));
                    }

                    Task updated = RegisterTask(task.Folder, task.Name, td, IsSystemPrincipal(td));
                    using (updated) { return ToTaskInfo(updated); }
                }
            });
        }

        public void Delete(string name, bool force)
        {
            TaskValidator.ValidateName(name);
            if (!force)
                throw new TaskServiceException(ErrorCode.ConfirmRequired, "删除任务需要确认，请添加 --force 参数");

            Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    task.Folder.DeleteTask(task.Name);
                    return true;
                }
            });
        }

        public void SetEnabled(string name, bool enabled)
        {
            TaskValidator.ValidateName(name);
            Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    task.Enabled = enabled;
                    return true;
                }
            });
        }

        public void Run(string name)
        {
            TaskValidator.ValidateName(name);
            Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    task.Run();
                    return true;
                }
            });
        }

        public IReadOnlyList<HistoryEvent> GetHistory(string name, int? last, bool errorsOnly)
        {
            TaskValidator.ValidateName(name);
            return Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    return _history.GetHistory(task.Path, last, errorsOnly);
                }
            });
        }

        public string BuildAddCommand(TaskInfo task)
        {
            return CliCommandBuilder.BuildAddCommand(task);
        }

        // ---------- V2: XML 导入导出 ----------

        public string Export(string name)
        {
            TaskValidator.ValidateName(name);
            return Run(delegate
            {
                using (var ts = new TaskService())
                using (Task task = FindTaskOrThrow(ts, name))
                {
                    return task.Definition.XmlText;
                }
            });
        }

        public void ExportToFile(string name, string filePath)
        {
            string xml = Export(name);
            File.WriteAllText(filePath, xml, System.Text.Encoding.UTF8);
        }

        public TaskInfo Import(string xml, string name, bool force)
        {
            TaskValidator.ValidateName(name);
            if (string.IsNullOrWhiteSpace(xml))
                throw new TaskServiceException(ErrorCode.InvalidArguments, "XML 内容不能为空");

            return Run(delegate
            {
                TaskDefinition td;
                try
                {
                    using (var ts = new TaskService())
                    {
                        td = ts.NewTask();
                        td.XmlText = xml;
                    }
                }
                catch (Exception ex)
                {
                    throw new TaskServiceException(ErrorCode.XmlParseError, "XML 解析失败: " + ex.Message, ex);
                }

                ValidateImportedDefinition(td);

                using (var ts2 = new TaskService())
                {
                    if (!force)
                    {
                        using (Task existing = FindTask(ts2, name))
                        {
                            if (existing != null)
                                throw new TaskServiceException(ErrorCode.TaskExists, "任务 '" + name + "' 已存在");
                        }
                    }

                    string sub, leaf;
                    SplitRelativeName(name, out sub, out leaf);
                    TaskFolder folder = GetOrCreateFolder(ts2, sub);

                    bool runAsSystem = IsSystemPrincipal(td);
                    Task imported = RegisterTask(folder, leaf, td, runAsSystem);
                    using (imported) { return ToTaskInfo(imported); }
                }
            });
        }

        public TaskInfo ImportFromFile(string filePath, string name, bool force)
        {
            if (!File.Exists(filePath))
                throw new TaskServiceException(ErrorCode.InvalidPath, "文件不存在: " + filePath);
            string xml = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return Import(xml, name, force);
        }

        private static void ValidateImportedDefinition(TaskDefinition td)
        {
            // 仅允许 ExecAction
            for (int i = 0; i < td.Actions.Count; i++)
            {
                Microsoft.Win32.TaskScheduler.Action a = td.Actions[i];
                if (!(a is ExecAction))
                    throw new TaskServiceException(ErrorCode.InvalidArguments,
                        "不支持的动作类型: " + a.GetType().Name + "（仅支持 ExecAction）");
            }
            // 触发器类型过滤在 ReadTriggers 时自动跳过未知类型，此处不报错
        }

        // ---------- 文件夹与查找 ----------

        internal static void SplitRelativeName(string relativeName, out string subFolder, out string leafName)
        {
            int idx = relativeName.LastIndexOf('\\');
            if (idx < 0) { subFolder = ""; leafName = relativeName; }
            else { subFolder = relativeName.Substring(0, idx); leafName = relativeName.Substring(idx + 1); }
        }

        internal static TaskFolder TryGetFolder(TaskService ts, string fullPath)
        {
            try { return ts.GetFolder(fullPath); }
            catch (Exception) { return null; }
        }

        internal static TaskFolder GetOrCreateFolder(TaskService ts, string subFolder)
        {
            TaskFolder root = TryGetFolder(ts, RootFolderPath);
            if (root == null)
                root = ts.RootFolder.CreateFolder("ScheduleCenter", null, false);

            if (string.IsNullOrEmpty(subFolder)) return root;

            TaskFolder current = root;
            string path = RootFolderPath;
            foreach (string seg in subFolder.Split('\\'))
            {
                TaskFolder next = TryGetFolder(ts, path + "\\" + seg);
                if (next == null)
                    next = current.CreateFolder(seg, null, false);
                current = next;
                path += "\\" + seg;
            }
            return current;
        }

        internal static Task FindTask(TaskService ts, string relativeName)
        {
            string sub, leaf;
            SplitRelativeName(relativeName, out sub, out leaf);
            string full = string.IsNullOrEmpty(sub) ? RootFolderPath : RootFolderPath + "\\" + sub;
            TaskFolder folder = TryGetFolder(ts, full);
            if (folder == null) return null;
            try
            {
                foreach (Task t in folder.Tasks)
                {
                    if (string.Equals(t.Name, leaf, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
                return null;
            }
            catch (Exception) { return null; }
        }

        internal static Task FindTaskOrThrow(TaskService ts, string relativeName)
        {
            Task task = FindTask(ts, relativeName);
            if (task == null)
                throw new TaskServiceException(ErrorCode.TaskNotFound, "任务 '" + relativeName + "' 不存在");
            return task;
        }

        // ---------- 触发器 ----------

        internal static Trigger BuildTrigger(TriggerSpec spec)
        {
            switch (spec.Kind)
            {
                case TriggerKind.Once:
                    return new TimeTrigger(spec.Date.Value.Date + spec.Time.Value);
                case TriggerKind.Daily:
                    return new DailyTrigger(1) { StartBoundary = DateTime.Today + spec.Time.Value };
                case TriggerKind.Weekly:
                    return new WeeklyTrigger
                    {
                        StartBoundary = DateTime.Today + spec.Time.Value,
                        DaysOfWeek = ToDaysOfTheWeek(spec.Days),
                        WeeksInterval = 1
                    };
                case TriggerKind.Monthly:
                    return new MonthlyTrigger
                    {
                        StartBoundary = DateTime.Today + spec.Time.Value,
                        DaysOfMonth = new[] { spec.DayOfMonth.Value },
                        MonthsOfYear = MonthsOfTheYear.AllMonths
                    };
                case TriggerKind.Boot:
                    return new BootTrigger();
                case TriggerKind.Logon:
                    return new LogonTrigger();
                case TriggerKind.Idle:
                    return new IdleTrigger();
                case TriggerKind.Event:
                    return BuildEventTrigger(spec);
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型");
            }
        }

        private static EventTrigger BuildEventTrigger(TriggerSpec spec)
        {
            var et = new EventTrigger();
            if (!string.IsNullOrWhiteSpace(spec.EventSubscription))
            {
                et.Subscription = spec.EventSubscription;
            }
            else
            {
                et.Subscription = BuildEventSubscription(spec.EventLog, spec.EventSource, spec.EventId);
            }
            return et;
        }

        internal static string BuildEventSubscription(string log, string source, int? eventId)
        {
            string condition;
            if (!string.IsNullOrEmpty(source))
                condition = "*[System[Provider[@Name='" + source + "']";
            else
                condition = "*[System[";

            if (eventId.HasValue)
                condition += " and EventID=" + eventId.Value.ToString();

            condition += "]]";

            return "<QueryList><Query Id=\"0\" Path=\"" + log + "\"><Select Path=\"" + log + "\">" +
                   condition + "</Select></Query></QueryList>";
        }

        private static void ApplyIdleSettingsFromTriggers(TaskDefinition td, IList<TriggerSpec> triggers)
        {
            if (triggers == null) return;
            IdleSettingsSpec idleSpec = null;
            foreach (TriggerSpec ts in triggers)
            {
                if (ts.Kind == TriggerKind.Idle && ts.IdleSettings != null)
                {
                    idleSpec = ts.IdleSettings;
                    break;
                }
            }
            if (idleSpec == null) return;

            if (idleSpec.WaitTimeout.HasValue) td.Settings.IdleSettings.WaitTimeout = idleSpec.WaitTimeout.Value;
            td.Settings.IdleSettings.StopOnIdleEnd = idleSpec.StopOnIdleEnd;
            td.Settings.IdleSettings.RestartOnIdle = idleSpec.RestartOnIdle;
        }

        internal static IList<TriggerSpec> ReadTriggers(TaskDefinition def)
        {
            var list = new List<TriggerSpec>();
            if (def == null || def.Triggers == null) return list;

            IdleSettingsSpec idleSettings = null;
            foreach (Trigger t in def.Triggers)
            {
                if (t.TriggerType == TaskTriggerType.Idle && idleSettings == null)
                {
                    idleSettings = new IdleSettingsSpec
                    {
                        WaitTimeout = def.Settings.IdleSettings.WaitTimeout,
                        StopOnIdleEnd = def.Settings.IdleSettings.StopOnIdleEnd,
                        RestartOnIdle = def.Settings.IdleSettings.RestartOnIdle
                    };
                }
            }

            foreach (Trigger t in def.Triggers)
            {
                TriggerSpec spec = ReadOneTrigger(t);
                if (spec == null) continue;
                if (spec.Kind == TriggerKind.Idle) spec.IdleSettings = idleSettings;
                list.Add(spec);
            }
            return list;
        }

        internal static TriggerSpec ReadOneTrigger(Trigger t)
        {
            if (t == null) return null;
            switch (t.TriggerType)
            {
                case TaskTriggerType.Time:
                    return new TriggerSpec { Kind = TriggerKind.Once, Date = t.StartBoundary.Date, Time = t.StartBoundary.TimeOfDay };
                case TaskTriggerType.Daily:
                    return new TriggerSpec { Kind = TriggerKind.Daily, Time = t.StartBoundary.TimeOfDay };
                case TaskTriggerType.Weekly:
                    var w = (WeeklyTrigger)t;
                    return new TriggerSpec { Kind = TriggerKind.Weekly, Time = t.StartBoundary.TimeOfDay, Days = FromDaysOfTheWeek(w.DaysOfWeek) };
                case TaskTriggerType.Monthly:
                    var m = (MonthlyTrigger)t;
                    return new TriggerSpec { Kind = TriggerKind.Monthly, Time = t.StartBoundary.TimeOfDay, DayOfMonth = m.DaysOfMonth.Length > 0 ? (int?)m.DaysOfMonth[0] : null };
                case TaskTriggerType.Boot:
                    return new TriggerSpec { Kind = TriggerKind.Boot };
                case TaskTriggerType.Logon:
                    return new TriggerSpec { Kind = TriggerKind.Logon };
                case TaskTriggerType.Idle:
                    return new TriggerSpec { Kind = TriggerKind.Idle };
                case TaskTriggerType.Event:
                    var e = (EventTrigger)t;
                    return new TriggerSpec { Kind = TriggerKind.Event, EventSubscription = e.Subscription };
                default:
                    return null;
            }
        }

        private static DaysOfTheWeek ToDaysOfTheWeek(DayOfWeek[] days)
        {
            DaysOfTheWeek result = (DaysOfTheWeek)0;
            foreach (DayOfWeek d in days)
            {
                switch (d)
                {
                    case DayOfWeek.Sunday: result |= DaysOfTheWeek.Sunday; break;
                    case DayOfWeek.Monday: result |= DaysOfTheWeek.Monday; break;
                    case DayOfWeek.Tuesday: result |= DaysOfTheWeek.Tuesday; break;
                    case DayOfWeek.Wednesday: result |= DaysOfTheWeek.Wednesday; break;
                    case DayOfWeek.Thursday: result |= DaysOfTheWeek.Thursday; break;
                    case DayOfWeek.Friday: result |= DaysOfTheWeek.Friday; break;
                    case DayOfWeek.Saturday: result |= DaysOfTheWeek.Saturday; break;
                }
            }
            return result;
        }

        private static DayOfWeek[] FromDaysOfTheWeek(DaysOfTheWeek days)
        {
            var list = new List<DayOfWeek>();
            if ((days & DaysOfTheWeek.Sunday) != 0) list.Add(DayOfWeek.Sunday);
            if ((days & DaysOfTheWeek.Monday) != 0) list.Add(DayOfWeek.Monday);
            if ((days & DaysOfTheWeek.Tuesday) != 0) list.Add(DayOfWeek.Tuesday);
            if ((days & DaysOfTheWeek.Wednesday) != 0) list.Add(DayOfWeek.Wednesday);
            if ((days & DaysOfTheWeek.Thursday) != 0) list.Add(DayOfWeek.Thursday);
            if ((days & DaysOfTheWeek.Friday) != 0) list.Add(DayOfWeek.Friday);
            if ((days & DaysOfTheWeek.Saturday) != 0) list.Add(DayOfWeek.Saturday);
            return list.ToArray();
        }

        // ---------- 其他辅助 ----------

        internal static TaskInfo ToTaskInfo(Task task)
        {
            ExecAction exec = task.Definition.Actions.OfType<ExecAction>().FirstOrDefault();
            return new TaskInfo
            {
                Name = task.Name,
                RelativeName = task.Path.StartsWith(RootFolderPath + "\\")
                    ? task.Path.Substring(RootFolderPath.Length + 1)
                    : task.Name,
                Folder = task.Folder.Path,
                Path = exec != null ? exec.Path : null,
                Arguments = exec != null ? exec.Arguments : null,
                WorkingDirectory = exec != null ? exec.WorkingDirectory : null,
                Description = task.Definition.RegistrationInfo.Description,
                Enabled = task.Enabled,
                State = task.State.ToString(),
                RunAsSystem = IsSystemPrincipal(task.Definition),
                Highest = task.Definition.Principal.RunLevel == TaskRunLevel.Highest,
                Triggers = ReadTriggers(task.Definition),
                NextRunTime = task.NextRunTime == DateTime.MinValue ? (DateTime?)null : task.NextRunTime,
                LastRunTime = task.LastRunTime == DateTime.MinValue ? (DateTime?)null : task.LastRunTime,
                LastResult = task.LastTaskResult,
                ExecutionTimeLimit = ReadExecutionTimeLimit(task.Definition.Settings.ExecutionTimeLimit),
                DisallowStartIfOnBatteries = task.Definition.Settings.DisallowStartIfOnBatteries,
                StopIfGoingOnBatteries = task.Definition.Settings.StopIfGoingOnBatteries
            };
        }

        private static void ApplyAdvancedSettings(TaskDefinition td, TaskSpec spec)
        {
            td.Settings.ExecutionTimeLimit = spec.ExecutionTimeLimit ?? TimeSpan.Zero;
            td.Settings.DisallowStartIfOnBatteries = spec.DisallowStartIfOnBatteries;
            td.Settings.StopIfGoingOnBatteries = spec.StopIfGoingOnBatteries;
        }

        private static void ApplyAdvancedSettingsUpdate(TaskDefinition td, TaskUpdate update)
        {
            if (update.ExecutionTimeLimit.HasValue)
                td.Settings.ExecutionTimeLimit = update.ExecutionTimeLimit.Value;
            if (update.DisallowStartIfOnBatteries.HasValue)
                td.Settings.DisallowStartIfOnBatteries = update.DisallowStartIfOnBatteries.Value;
            if (update.StopIfGoingOnBatteries.HasValue)
                td.Settings.StopIfGoingOnBatteries = update.StopIfGoingOnBatteries.Value;
        }

        private static TimeSpan? ReadExecutionTimeLimit(TimeSpan settingsValue)
        {
            // TaskScheduler 库约定 TimeSpan.Zero 表示无限制
            return settingsValue == TimeSpan.Zero ? (TimeSpan?)null : settingsValue;
        }

        private static bool IsSystemPrincipal(TaskDefinition td)
        {
            return string.Equals(td.Principal.UserId, "SYSTEM", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyPrincipal(TaskDefinition td, bool runAsSystem, bool highest)
        {
            td.Principal.RunLevel = highest ? TaskRunLevel.Highest : TaskRunLevel.LUA;
            if (runAsSystem)
            {
                td.Principal.UserId = "SYSTEM";
                td.Principal.LogonType = TaskLogonType.ServiceAccount;
            }
            else
            {
                td.Principal.UserId = null;
                td.Principal.LogonType = TaskLogonType.InteractiveToken;
            }
        }

        private static Task RegisterTask(TaskFolder folder, string name, TaskDefinition td, bool runAsSystem)
        {
            if (runAsSystem)
                return folder.RegisterTaskDefinition(name, td, TaskCreation.CreateOrUpdate, "SYSTEM", null, TaskLogonType.ServiceAccount, null);
            return folder.RegisterTaskDefinition(name, td, TaskCreation.CreateOrUpdate, null, null, TaskLogonType.InteractiveToken, null);
        }

        private static Regex WildcardToRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return null;
            string escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
            return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase);
        }

        private static T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (TaskServiceException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new TaskServiceException(ErrorCode.AccessDenied, "权限不足，请以管理员身份运行", ex);
            }
            catch (COMException ex)
            {
                if (ex.HResult == unchecked((int)0x80070005))
                    throw new TaskServiceException(ErrorCode.AccessDenied, "权限不足，请以管理员身份运行", ex);
                throw new TaskServiceException(ErrorCode.InternalError,
                    string.Format("任务计划服务错误 (0x{0:X8}): {1}", ex.HResult, ex.Message), ex);
            }
            catch (Exception ex)
            {
                throw new TaskServiceException(ErrorCode.InternalError, ex.Message, ex);
            }
        }
    }
}
