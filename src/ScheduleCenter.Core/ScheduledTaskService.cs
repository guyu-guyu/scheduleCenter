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
                    td.Triggers.Add(BuildTrigger(spec.Trigger));
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
            if (update.Path != null && !File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            if (update.Trigger != null)
                TaskValidator.ValidateTrigger(update.Trigger);

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
                    if (update.Trigger != null)
                    {
                        td.Triggers.Clear();
                        td.Triggers.Add(BuildTrigger(update.Trigger));
                    }
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
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型");
            }
        }

        internal static TriggerSpec ReadTrigger(Trigger t)
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
                Trigger = ReadTrigger(task.Definition.Triggers.Count > 0 ? task.Definition.Triggers[0] : null),
                NextRunTime = task.NextRunTime == DateTime.MinValue ? (DateTime?)null : task.NextRunTime,
                LastRunTime = task.LastRunTime == DateTime.MinValue ? (DateTime?)null : task.LastRunTime,
                LastResult = task.LastTaskResult
            };
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
