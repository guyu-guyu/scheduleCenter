# ScheduleCenter V2 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 V1 基础上新增高级触发器与条件（多触发器组合、空闲触发、事件触发、执行时间限制、电源条件）+ 任务 XML 导入导出。

**Architecture:** 沿用 V1 三层（Core / Cli / Gui）。Core 数据模型 `Trigger` 单字段改 `Triggers` 数组（breaking change），CLI 输入语法保留 V1 单触发器兼容；新增 `--triggers-json`/`--triggers-file` 双轨语法。XML 导入导出走 TaskScheduler 库原生 `TaskDefinition.XmlText`。

**Tech Stack:** .NET Framework 4.8、C# 7.3、WPF、MSTest、TaskScheduler NuGet 2.12.2、Newtonsoft.Json 13.0.3。

**Spec:** `docs/superpowers/specs/2026-07-24-schedule-center-v2-design.md`

## Global Constraints

- 沿用 V1 全部约束：net48 / C# 7.3（禁 C# 8+ 语法）/ `\ScheduleCenter\` 隔离 / manifest `requireAdministrator` / CLI JSON camelCase / 退出码约定
- Breaking change 仅限数据模型与 CLI JSON 输出；**CLI 输入语法保留 V1 兼容**
- 新增 ErrorCode：`InvalidTriggerFormat` / `InvalidEventSubscription` / `XmlParseError`（对应退出码均为 2）
- 测试沿用 V1 `ServiceTestBase`（管理员运行、`\ScheduleCenter\Tests\` 子文件夹、`Test_{guid}` 命名、用完即删）
- GUI 不写自动化测试（延后 V3），每个 GUI Task 末尾提供手动验证清单

---

### Task 1: 数据模型与错误码扩展

**Files:**
- Modify: `src/ScheduleCenter.Core/Models.cs`
- Modify: `src/ScheduleCenter.Core/Errors.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/ErrorsTests.cs`

**Interfaces:**
- Produces: `TriggerKind` 加 `Idle`/`Event`；`TriggerSpec` 加 Idle/Event 字段；`IdleSettingsSpec` 新类；`TaskSpec`/`TaskUpdate`/`TaskInfo` 的 `Trigger` → `Triggers` 数组 + 高级条件字段；`ErrorCode` 加 3 项。后续所有任务使用。

- [ ] **Step 1: 先改测试（失败测试先行）**

在 `tests/ScheduleCenter.Core.Tests/ErrorsTests.cs` 末尾追加：

```csharp
        [TestMethod]
        public void V2_ExitCode_MapsCorrectly()
        {
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidTriggerFormat, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidEventSubscription, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.XmlParseError, "x").ExitCode);
        }

        [TestMethod]
        public void V2_CodeName_MatchesCliContract()
        {
            Assert.AreEqual("INVALID_TRIGGER_FORMAT", new TaskServiceException(ErrorCode.InvalidTriggerFormat, "x").CodeName);
            Assert.AreEqual("INVALID_EVENT_SUBSCRIPTION", new TaskServiceException(ErrorCode.InvalidEventSubscription, "x").CodeName);
            Assert.AreEqual("XML_PARSE_ERROR", new TaskServiceException(ErrorCode.XmlParseError, "x").CodeName);
        }
```

- [ ] **Step 2: 运行测试确认编译失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ErrorsTests
```

Expected: 编译失败 `ErrorCode 没有 InvalidTriggerFormat 定义`。

- [ ] **Step 3: 扩展 Errors.cs**

在 `ErrorCode` 枚举 `InternalError` 之前插入 3 项：

```csharp
    public enum ErrorCode
    {
        InvalidArguments,
        ConfirmRequired,
        TaskNotFound,
        TaskExists,
        AccessDenied,
        HistoryDisabled,
        InvalidPath,
        InvalidTriggerFormat,
        InvalidEventSubscription,
        XmlParseError,
        InternalError
    }
```

`ExitCode` getter 的 `case ErrorCode.InvalidPath: return 2;` 下方追加：

```csharp
                    case ErrorCode.InvalidTriggerFormat:
                    case ErrorCode.InvalidEventSubscription:
                    case ErrorCode.XmlParseError:
                        return 2;
```

`CodeName` getter 的 `default: return "INTERNAL_ERROR";` 上方插入：

```csharp
                    case ErrorCode.InvalidTriggerFormat: return "INVALID_TRIGGER_FORMAT";
                    case ErrorCode.InvalidEventSubscription: return "INVALID_EVENT_SUBSCRIPTION";
                    case ErrorCode.XmlParseError: return "XML_PARSE_ERROR";
                    default: return "INTERNAL_ERROR";
```

- [ ] **Step 4: 重写 Models.cs**

完整替换 `src/ScheduleCenter.Core/Models.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace ScheduleCenter.Core
{
    public enum TriggerKind { Once, Daily, Weekly, Monthly, Boot, Logon, Idle, Event }

    public sealed class IdleSettingsSpec
    {
        public TimeSpan? WaitTimeout { get; set; }
        public TimeSpan? Duration { get; set; }
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
        public string Name { get; set; }
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
        public string Name { get; set; }
        public string Path { get; set; }
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
        public string Name { get; set; }
        public string RelativeName { get; set; }
        public string Folder { get; set; }
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
        public string Type { get; set; }
        public int? ResultCode { get; set; }
        public string Message { get; set; }
    }
}
```

- [ ] **Step 5: 编译 Core 项目（预期大量编译错误来自下游）**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: Core 自身编译通过；全解决方案编译失败，因为 `TaskSpec.Trigger` / `TaskInfo.Trigger` / `TaskUpdate.Trigger` 字段已改名，下游 Cli/Gui/Tests 引用旧字段。后续任务逐一修复。

- [ ] **Step 6: 运行 ErrorsTests 确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ErrorsTests
```

Expected: `ErrorsTests` 全部 Passed（其他测试仍编译失败，暂忽略）。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): extend models for v2 (triggers array, idle/event, advanced settings, new error codes)"
```

---

### Task 2: TaskValidator 扩展

**Files:**
- Modify: `src/ScheduleCenter.Core/TaskValidator.cs`

**Interfaces:**
- Consumes: Task 1 的新 `TriggerKind`/`TriggerSpec`/`IdleSettingsSpec`
- Produces: `ValidateTrigger` 支持 Idle/Event；`ValidateSpec`/`ValidateUpdate` 改用 `Triggers` 列表 + 高级条件

- [ ] **Step 1: 重写 TaskValidator.cs**

完整替换：

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace ScheduleCenter.Core
{
    public static class TaskValidator
    {
        private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '|', '?', '*', '\'' };

        public static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称不能为空");

            string[] segments = name.Split('\\');
            foreach (string seg in segments)
            {
                if (seg.Length == 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称 '" + name + "' 包含空的路径段");
                if (seg != seg.Trim())
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 首尾不能有空格");
                if (seg == "." || seg == "..")
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 非法");
                if (seg.IndexOfAny(InvalidChars) >= 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "任务名称段 '" + seg + "' 包含非法字符");
            }
        }

        public static void ValidateSpec(TaskSpec spec)
        {
            if (spec == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "任务定义不能为空");
            ValidateName(spec.Name);
            if (string.IsNullOrWhiteSpace(spec.Path))
                throw new TaskServiceException(ErrorCode.InvalidArguments, "程序路径不能为空");
            if (!File.Exists(spec.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + spec.Path);
            if (spec.Triggers == null || spec.Triggers.Count == 0)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "至少需要一个触发器");
            foreach (TriggerSpec t in spec.Triggers)
                ValidateTrigger(t);
            ValidateExecutionTimeLimit(spec.ExecutionTimeLimit);
        }

        public static void ValidateUpdate(TaskUpdate update)
        {
            ValidateName(update.Name);
            if (update.Path != null && !File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            if (update.Triggers != null)
            {
                if (update.Triggers.Count == 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "触发器列表不能为空");
                foreach (TriggerSpec t in update.Triggers)
                    ValidateTrigger(t);
            }
            ValidateExecutionTimeLimit(update.ExecutionTimeLimit);

            if (update.Path == null && update.Arguments == null && update.WorkingDirectory == null &&
                update.Description == null && update.Triggers == null && update.RunAsSystem == null &&
                update.Highest == null && update.Enabled == null && update.StartWhenAvailable == null &&
                update.ExecutionTimeLimit == null && update.DisallowStartIfOnBatteries == null &&
                update.StopIfGoingOnBatteries == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "update 命令至少需要一个要修改的选项");
        }

        public static void ValidateTrigger(TriggerSpec t)
        {
            if (t == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少触发器定义");

            switch (t.Kind)
            {
                case TriggerKind.Once:
                    RequireTime(t, "once");
                    if (!t.Date.HasValue)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "once 触发器需要日期");
                    if (t.Date.Value.Date + t.Time.Value < DateTime.Now)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "once 触发器的日期时间不能早于当前时间");
                    break;
                case TriggerKind.Daily:
                    RequireTime(t, "daily");
                    break;
                case TriggerKind.Weekly:
                    RequireTime(t, "weekly");
                    if (t.Days == null || t.Days.Length == 0)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "weekly 触发器需要至少一个星期值");
                    break;
                case TriggerKind.Monthly:
                    RequireTime(t, "monthly");
                    if (!t.DayOfMonth.HasValue || t.DayOfMonth.Value < 1 || t.DayOfMonth.Value > 31)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "monthly 触发器的 day-of-month 必须在 1-31 之间");
                    break;
                case TriggerKind.Boot:
                case TriggerKind.Logon:
                case TriggerKind.Idle:
                    break;
                case TriggerKind.Event:
                    if (string.IsNullOrWhiteSpace(t.EventSubscription) && string.IsNullOrWhiteSpace(t.EventLog))
                        throw new TaskServiceException(ErrorCode.InvalidEventSubscription,
                            "event 触发器需要 --event-log 或 --event-subscription");
                    break;
            }
        }

        public static void ValidateExecutionTimeLimit(TimeSpan? limit)
        {
            if (limit.HasValue && limit.Value < TimeSpan.Zero)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "执行时间限制不能为负数");
        }

        private static void RequireTime(TriggerSpec t, string kindName)
        {
            if (!t.Time.HasValue)
                throw new TaskServiceException(ErrorCode.InvalidArguments, kindName + " 触发器需要时间参数");
        }
    }
}
```

- [ ] **Step 2: 编译 Core**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: Core 编译通过。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(core): validator supports triggers list, idle/event, advanced settings"
```

---

### Task 3: ScheduledTaskService 多触发器改造

**Files:**
- Modify: `src/ScheduleCenter.Core/ScheduledTaskService.cs`

**Interfaces:**
- Consumes: Task 1/2 的新模型
- Produces: `Add`/`Update`/`Get`/`List` 用 `Triggers` 集合；`BuildTrigger` → `BuildTriggers`；`ReadTrigger` → `ReadTriggers`；`ToTaskInfo` 输出数组 + 高级条件字段

- [ ] **Step 1: 改 Add 方法**

`src/ScheduleCenter.Core/ScheduledTaskService.cs` 中 `Add` 方法，把：

```csharp
                    td.Triggers.Add(BuildTrigger(spec.Trigger));
```

替换为：

```csharp
                    foreach (TriggerSpec ts in spec.Triggers)
                        td.Triggers.Add(BuildTrigger(ts));
                    ApplyAdvancedSettings(td, spec);
```

并在 `ApplyPrincipal(td, spec.RunAsSystem, spec.Highest);` 这行之前补一行调用高级设置的方法（如果还没的话）。实际上 `ApplyAdvancedSettings` 放在 `td.Actions.Add(...)` 之后即可。完整 Add 方法的触发器/设置部分应为：

```csharp
                    td.RegistrationInfo.Description = spec.Description ?? "";
                    td.Settings.Enabled = spec.Enabled;
                    td.Settings.StartWhenAvailable = spec.StartWhenAvailable;
                    ApplyPrincipal(td, spec.RunAsSystem, spec.Highest);
                    foreach (TriggerSpec ts in spec.Triggers)
                        td.Triggers.Add(BuildTrigger(ts));
                    ApplyAdvancedSettings(td, spec);
                    td.Actions.Add(new ExecAction(spec.Path, spec.Arguments, spec.WorkingDirectory));
```

- [ ] **Step 2: 改 Update 方法**

把 `Update` 方法开头的校验段：

```csharp
            if (update.Path != null && !File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            if (update.Trigger != null)
                TaskValidator.ValidateTrigger(update.Trigger);
```

替换为：

```csharp
            if (update.Path != null && !File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            TaskValidator.ValidateUpdate(update);
```

注意 `TaskValidator.ValidateUpdate` 已包含"至少一个选项"检查，所以原来 `SpecBuilder.BuildUpdate` 里的那段重复检查可保留（更早失败）也可删除，不影响正确性。本计划保留两边都查，V1 行为不变。

把 Update 方法体内的：

```csharp
                    if (update.Trigger != null)
                    {
                        td.Triggers.Clear();
                        td.Triggers.Add(BuildTrigger(update.Trigger));
                    }
```

替换为：

```csharp
                    if (update.Triggers != null)
                    {
                        td.Triggers.Clear();
                        foreach (TriggerSpec ts in update.Triggers)
                            td.Triggers.Add(BuildTrigger(ts));
                    }
                    ApplyAdvancedSettingsUpdate(td, update);
```

- [ ] **Step 3: 改 ToTaskInfo 方法**

把 `ToTaskInfo` 中：

```csharp
                Trigger = ReadTrigger(task.Definition.Triggers.Count > 0 ? task.Definition.Triggers[0] : null),
```

替换为：

```csharp
                Triggers = ReadTriggers(task.Definition.Triggers),
                ExecutionTimeLimit = ReadExecutionTimeLimit(task.Definition.Settings.ExecutionTimeLimit),
                DisallowStartIfOnBatteries = task.Definition.Settings.DisallowStartIfOnBatteries,
                StopIfGoingOnBatteries = task.Definition.Settings.StopIfGoingOnBatteries,
```

- [ ] **Step 4: 改 ReadTrigger 为 ReadTriggers**

把 `ReadTrigger` 方法整体替换为：

```csharp
        internal static IList<TriggerSpec> ReadTriggers(TriggerCollection triggers)
        {
            var list = new List<TriggerSpec>();
            if (triggers == null) return list;
            foreach (Trigger t in triggers)
            {
                TriggerSpec spec = ReadOneTrigger(t);
                if (spec != null) list.Add(spec);
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
                    return new TriggerSpec { Kind = TriggerKind.Idle, IdleSettings = ReadIdleSettings(t) };
                case TaskTriggerType.Event:
                    var e = (EventTrigger)t;
                    return new TriggerSpec { Kind = TriggerKind.Event, EventSubscription = e.Subscription };
                default:
                    return null;
            }
        }

        private static IdleSettingsSpec ReadIdleSettings(Trigger t)
        {
            var s = new IdleSettingsSpec
            {
                StopOnIdleEnd = t.EndBoundary != DateTime.MaxValue // 占位，实际读 Settings.IdleSettings
            };
            return s;
        }
```

注意：`IdleTrigger` 自身不存空闲设置，设置在 `TaskDefinition.Settings.IdleSettings`。`ReadIdleSettings` 从 trigger 读不到有意义的数据，因此 `ReadOneTrigger` 对 Idle 类型只返回 `Kind = Idle`，`IdleSettings` 字段留 null（GUI 编辑时若需要再从 Settings 读）。修正 `ReadOneTrigger` 的 Idle 分支：

```csharp
                case TaskTriggerType.Idle:
                    return new TriggerSpec { Kind = TriggerKind.Idle };
```

删除 `ReadIdleSettings` 占位方法。

- [ ] **Step 5: 新增高级设置辅助方法**

在 `ApplyPrincipal` 方法之前添加：

```csharp
        private static void ApplyAdvancedSettings(TaskDefinition td, TaskSpec spec)
        {
            td.Settings.ExecutionTimeLimit = spec.ExecutionTimeLimit ?? TimeSpan.Zero;
            td.Settings.DisallowStartIfOnBatteries = spec.DisallowStartIfOnBatteries;
            td.Settings.StopIfGoingOnBatteries = spec.StopIfGoingOnBatteries;
        }

        private static void ApplyAdvancedSettingsUpdate(TaskDefinition td, TaskUpdate update)
        {
            if (update.ExecutionTimeLimit.HasValue)
                td.Settings.ExecutionTimeLimit = update.ExecutionTimeLimit.Value == TimeSpan.Zero ? TimeSpan.Zero : update.ExecutionTimeLimit.Value;
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
```

- [ ] **Step 6: 编译 Core**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: Core 编译通过。下游 Cli/Gui/Tests 仍失败（引用旧字段 `Trigger`），后续任务修复。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): service uses triggers collection and advanced settings"
```

---

### Task 4: ScheduledTaskService 新触发器（Idle + Event）

**Files:**
- Modify: `src/ScheduleCenter.Core/ScheduledTaskService.cs`

**Interfaces:**
- Consumes: Task 1 的 `IdleSettingsSpec`、`TriggerKind.Idle`/`Event`
- Produces: `BuildTrigger` 支持 Idle/Event；IdleSettings 映射到 `TaskDefinition.Settings.IdleSettings`

- [ ] **Step 1: 扩展 BuildTrigger 的 switch**

在 `BuildTrigger` 方法（Task 3 后仍保留单触发器构建方法签名 `internal static Trigger BuildTrigger(TriggerSpec spec)`）的 `case TriggerKind.Logon:` 之后、`default:` 之前插入：

```csharp
                case TriggerKind.Idle:
                    return new IdleTrigger();
                case TriggerKind.Event:
                    return BuildEventTrigger(spec);
```

- [ ] **Step 2: 新增 BuildEventTrigger 与 ApplyIdleSettings**

在 `BuildTrigger` 方法之后添加：

```csharp
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
            string condition = "";
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

        private static void ApplyIdleSettingsToDefinition(TaskDefinition td, IdleSettingsSpec spec)
        {
            if (spec == null) return;
            if (spec.WaitTimeout.HasValue) td.Settings.IdleSettings.WaitTimeout = spec.WaitTimeout.Value;
            if (spec.Duration.HasValue) td.Settings.IdleSettings.Duration = spec.Duration.Value;
            td.Settings.IdleSettings.StopOnIdleEnd = spec.StopOnIdleEnd;
            td.Settings.IdleSettings.RestartOnIdle = spec.RestartOnIdle;
        }
```

- [ ] **Step 3: 在 Add 方法中调用 ApplyIdleSettings**

`Add` 方法中，`foreach (TriggerSpec ts in spec.Triggers) td.Triggers.Add(BuildTrigger(ts));` 之后插入：

```csharp
                    IdleSettingsSpec idleSpec = null;
                    foreach (TriggerSpec ts in spec.Triggers)
                    {
                        if (ts.Kind == TriggerKind.Idle && ts.IdleSettings != null)
                        {
                            idleSpec = ts.IdleSettings;
                            break;
                        }
                    }
                    if (idleSpec != null)
                        ApplyIdleSettingsToDefinition(td, idleSpec);
```

（多个 idle 触发器时取第一个的 IdleSettings，符合"任务级设置"语义。）

- [ ] **Step 4: 在 Update 方法中同样处理**

`Update` 方法中，触发器替换块之后插入：

```csharp
                    if (update.Triggers != null)
                    {
                        IdleSettingsSpec idleSpec = null;
                        foreach (TriggerSpec ts in update.Triggers)
                        {
                            if (ts.Kind == TriggerKind.Idle && ts.IdleSettings != null)
                            {
                                idleSpec = ts.IdleSettings;
                                break;
                            }
                        }
                        if (idleSpec != null)
                            ApplyIdleSettingsToDefinition(td, idleSpec);
                    }
```

- [ ] **Step 5: 补全 ReadOneTrigger 的 Idle 分支（从 Settings 读回 IdleSettings）**

`ReadOneTrigger` 的 Idle 分支改为从所属 `TaskDefinition.Settings.IdleSettings` 读取。但 `ReadOneTrigger` 只接收单个 `Trigger`，无法访问 `TaskDefinition`。改为在 `ReadTriggers` 层传入 `TaskDefinition`：

把 `ReadTriggers` 签名改为：

```csharp
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
                        Duration = def.Settings.IdleSettings.Duration,
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
```

同步修改 `ToTaskInfo` 中的调用：

```csharp
                Triggers = ReadTriggers(task.Definition),
```

- [ ] **Step 6: 编译 Core**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: 编译通过。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): idle and event triggers with idle settings mapping"
```

---

### Task 5: Core XML 导入导出

**Files:**
- Modify: `src/ScheduleCenter.Core/ScheduledTaskService.cs`

**Interfaces:**
- Produces: `Export(name)` / `ExportToFile(name, filePath)` / `Import(xml, name, force)` / `ImportFromFile(filePath, name, force)`

- [ ] **Step 1: 在 ScheduledTaskService 中新增公共 API**

在 `BuildAddCommand` 方法之前（即 `// ---------- 文件夹与查找 ----------` 注释之前）插入：

```csharp
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
                Action a = td.Actions[i];
                if (!(a is ExecAction))
                    throw new TaskServiceException(ErrorCode.InvalidArguments,
                        "不支持的动作类型: " + a.GetType().Name + "（仅支持 ExecAction）");
            }
            // 触发器类型过滤在 ReadTriggers 时自动跳过未知类型，此处不报错
        }
```

注意：`ValidateImportedDefinition` 不强制触发器类型，未知类型在 `ReadTriggers` 返回时被跳过（返回的 `Triggers` 列表不含不支持项）。调用方若需 warning，可比较导入后 `Triggers.Count` 与原始 XML 中触发器节点数。

- [ ] **Step 2: 编译 Core**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: 编译通过。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(core): xml export/import with exec-action validation"
```

---

### Task 6: CliParser 扩展

**Files:**
- Modify: `src/ScheduleCenter/Cli/CliParser.cs`

**Interfaces:**
- Produces: 支持 `--triggers-json`/`--triggers-file`/`--event-*`/`--idle-*`/`--execution-time-limit`/`--no-start-on-battery`/`--stop-on-battery`/`export`/`import` 子命令；互斥规则校验

- [ ] **Step 1: 扩展 Commands / Flags / ValueOptions 集合**

`CliParser.cs` 中：

`Commands` 集合追加 `"export", "import"`：

```csharp
        private static readonly HashSet<string> Commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "add", "update", "delete", "get", "list", "enable", "disable", "run", "history", "export", "import" };
```

`Flags` 集合追加：

```csharp
        private static readonly HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "force", "run-as-system", "highest", "enabled", "start-when-available", "errors-only",
          "no-start-on-battery", "stop-on-battery", "idle-stop-on-end", "idle-restart" };
```

`ValueOptions` 集合追加：

```csharp
        private static readonly HashSet<string> ValueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "name", "path", "args", "workdir", "trigger", "time", "date", "days", "day-of-month", "description", "filter", "last",
          "triggers-json", "triggers-file", "event-log", "event-source", "event-id", "event-subscription",
          "idle-wait", "idle-duration", "execution-time-limit", "output", "file" };
```

- [ ] **Step 2: 更新 Usage**

```csharp
        public static string Usage()
        {
            return "用法: ScheduleCenter <add|update|delete|get|list|enable|disable|run|history|export|import> [选项]\n" +
                   "示例: ScheduleCenter add --name Backup --path C:\\app.exe --trigger daily --time 09:00\n" +
                   "      ScheduleCenter add --name Backup --path C:\\app.exe --triggers-json '[{\"kind\":\"daily\",\"time\":\"09:00\"}]'\n" +
                   "      ScheduleCenter export --name Backup --output C:\\backup.xml";
        }
```

- [ ] **Step 3: 新增互斥校验方法**

在 `Parse` 方法之后添加：

```csharp
        internal static void ValidateMutualExclusion(ParsedArgs p)
        {
            bool hasTrigger = p.Has("trigger");
            bool hasTriggersJson = p.Has("triggers-json");
            bool hasTriggersFile = p.Has("triggers-file");
            bool hasEventLog = p.Has("event-log");
            bool hasEventSub = p.Has("event-subscription");
            bool hasEventSource = p.Has("event-source");
            bool hasEventId = p.Has("event-id");

            int triggerSourceCount = (hasTrigger ? 1 : 0) + (hasTriggersJson ? 1 : 0) + (hasTriggersFile ? 1 : 0);
            if (triggerSourceCount > 1)
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "--trigger / --triggers-json / --triggers-file 互斥，只能指定一个");

            if (hasEventLog && hasEventSub)
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "--event-log 与 --event-subscription 互斥");

            if ((hasEventSource || hasEventId) && !hasEventLog)
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "--event-source / --event-id 必须与 --event-log 搭配使用");

            // idle-* 仅在 trigger=idle 时允许（仅对单触发器语法有效；triggers-json 走 JSON 解析，不在此校验）
            if (hasTrigger)
            {
                string triggerType = p.Get("trigger");
                bool isIdle = triggerType != null && triggerType.ToLowerInvariant() == "idle";
                if (!isIdle && (p.Has("idle-wait") || p.Has("idle-duration") || p.Has("idle-stop-on-end") || p.Has("idle-restart")))
                    throw new TaskServiceException(ErrorCode.InvalidArguments,
                        "--idle-* 参数仅在 --trigger idle 时可用");
            }
            else if (p.Has("idle-wait") || p.Has("idle-duration") || p.Has("idle-stop-on-end") || p.Has("idle-restart"))
            {
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "--idle-* 参数仅在 --trigger idle 时可用");
            }
        }
```

- [ ] **Step 4: 在 Parse 返回前调用互斥校验**

`Parse` 方法的 `return result;` 之前插入：

```csharp
            ValidateMutualExclusion(result);
            return result;
```

- [ ] **Step 5: 编译**

```bash
dotnet build src/ScheduleCenter/ScheduleCenter.csproj
```

Expected: 仍可能有下游错误（SpecBuilder 等未改），但 CliParser 自身应编译通过。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(cli): parser supports v2 options and mutual exclusion rules"
```

---

### Task 7: SpecBuilder 扩展

**Files:**
- Modify: `src/ScheduleCenter/Cli/SpecBuilder.cs`
- Modify: `src/ScheduleCenter/Cli/TaskDto.cs`

**Interfaces:**
- Produces: `BuildSpec`/`BuildUpdate` 处理 `--triggers-json`/`--triggers-file`/新触发器参数/高级条件；`TaskDto.From` 输出 `triggers` 数组与新字段

- [ ] **Step 1: 在 SpecBuilder 顶部加 using**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ScheduleCenter.Core;
```

- [ ] **Step 2: 重写 BuildSpec**

```csharp
        public static TaskSpec BuildSpec(ParsedArgs p)
        {
            var spec = new TaskSpec
            {
                Name = p.Require("name"),
                Path = p.Require("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Triggers = BuildTriggers(p),
                Enabled = true,
                RunAsSystem = p.Has("run-as-system"),
                Highest = p.Has("highest"),
                StartWhenAvailable = true
            };
            ApplyAdvancedConditions(spec, p);
            TaskValidator.ValidateSpec(spec);
            return spec;
        }
```

- [ ] **Step 3: 重写 BuildUpdate**

```csharp
        public static TaskUpdate BuildUpdate(ParsedArgs p)
        {
            var update = new TaskUpdate
            {
                Name = p.Require("name"),
                Path = p.Get("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Triggers = BuildTriggers(p, false),
                RunAsSystem = p.Has("run-as-system") ? true : (bool?)null,
                Highest = p.Has("highest") ? true : (bool?)null,
                Enabled = p.Has("enabled") ? true : (bool?)null
            };
            ApplyAdvancedConditionsUpdate(update, p);
            TaskValidator.ValidateUpdate(update);
            return update;
        }
```

- [ ] **Step 4: 新增 BuildTriggers（替换原 BuildTrigger 调用）**

```csharp
        public static IList<TriggerSpec> BuildTriggers(ParsedArgs p)
        {
            return BuildTriggers(p, true);
        }

        private static IList<TriggerSpec> BuildTriggers(ParsedArgs p, bool required)
        {
            // 多触发器 JSON 路径
            if (p.Has("triggers-json"))
            {
                return ParseTriggersJson(p.Get("triggers-json"));
            }
            if (p.Has("triggers-file"))
            {
                string filePath = p.Get("triggers-file");
                if (!File.Exists(filePath))
                    throw new TaskServiceException(ErrorCode.InvalidPath, "触发器文件不存在: " + filePath);
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                return ParseTriggersJson(json);
            }

            // 单触发器 V1 兼容路径
            string type = p.Get("trigger");
            if (type == null)
            {
                if (required)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少必需参数 --trigger");
                return null;
            }

            var spec = BuildSingleTrigger(p, type);
            TaskValidator.ValidateTrigger(spec);
            return new List<TriggerSpec> { spec };
        }

        private static IList<TriggerSpec> ParseTriggersJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 内容为空");

            List<JsonTrigger> parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<List<JsonTrigger>>(json);
            }
            catch (Exception ex)
            {
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 解析失败: " + ex.Message, ex);
            }
            if (parsed == null || parsed.Count == 0)
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "triggers-json 至少包含一个触发器");

            var result = new List<TriggerSpec>();
            foreach (JsonTrigger jt in parsed)
            {
                TriggerSpec spec = JsonTriggerToSpec(jt);
                TaskValidator.ValidateTrigger(spec);
                result.Add(spec);
            }
            return result;
        }

        private static TriggerSpec BuildSingleTrigger(ParsedArgs p, string type)
        {
            var spec = new TriggerSpec();

            string timeStr = p.Get("time");
            if (timeStr != null)
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(timeStr, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "时间格式错误 '" + timeStr + "'，应为 HH:mm");
                spec.Time = time;
            }

            string dateStr = p.Get("date");
            if (dateStr != null)
            {
                DateTime date;
                if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "日期格式错误 '" + dateStr + "'，应为 yyyy-MM-dd");
                spec.Date = date;
            }

            string daysStr = p.Get("days");
            if (daysStr != null)
                spec.Days = daysStr.Split(',').Select(ParseDay).ToArray();

            string domStr = p.Get("day-of-month");
            if (domStr != null)
            {
                int dom;
                if (!int.TryParse(domStr, out dom))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "day-of-month 格式错误 '" + domStr + "'");
                spec.DayOfMonth = dom;
            }

            switch (type.ToLowerInvariant())
            {
                case "once": spec.Kind = TriggerKind.Once; break;
                case "daily": spec.Kind = TriggerKind.Daily; break;
                case "weekly": spec.Kind = TriggerKind.Weekly; break;
                case "monthly": spec.Kind = TriggerKind.Monthly; break;
                case "boot": spec.Kind = TriggerKind.Boot; break;
                case "logon": spec.Kind = TriggerKind.Logon; break;
                case "idle":
                    spec.Kind = TriggerKind.Idle;
                    spec.IdleSettings = BuildIdleSettings(p);
                    break;
                case "event":
                    spec.Kind = TriggerKind.Event;
                    ApplyEventParams(p, spec);
                    break;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型 '" + type + "'");
            }

            return spec;
        }

        private static IdleSettingsSpec BuildIdleSettings(ParsedArgs p)
        {
            var s = new IdleSettingsSpec
            {
                StopOnIdleEnd = p.Has("idle-stop-on-end"),
                RestartOnIdle = p.Has("idle-restart")
            };
            string wait = p.Get("idle-wait");
            if (wait != null)
            {
                int minutes;
                if (!int.TryParse(wait, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "idle-wait 格式错误 '" + wait + "'，应为非负整数分钟");
                s.WaitTimeout = TimeSpan.FromMinutes(minutes);
            }
            string dur = p.Get("idle-duration");
            if (dur != null)
            {
                int minutes;
                if (!int.TryParse(dur, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "idle-duration 格式错误 '" + dur + "'，应为非负整数分钟");
                s.Duration = TimeSpan.FromMinutes(minutes);
            }
            return s;
        }

        private static void ApplyEventParams(ParsedArgs p, TriggerSpec spec)
        {
            spec.EventSubscription = p.Get("event-subscription");
            spec.EventLog = p.Get("event-log");
            spec.EventSource = p.Get("event-source");
            string eid = p.Get("event-id");
            if (eid != null)
            {
                int id;
                if (!int.TryParse(eid, out id))
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "event-id 格式错误 '" + eid + "'");
                spec.EventId = id;
            }
        }

        private static void ApplyAdvancedConditions(TaskSpec spec, ParsedArgs p)
        {
            string etl = p.Get("execution-time-limit");
            if (etl != null)
            {
                int minutes;
                if (!int.TryParse(etl, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "execution-time-limit 格式错误 '" + etl + "'，应为非负整数分钟");
                spec.ExecutionTimeLimit = minutes == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
            }
            spec.DisallowStartIfOnBatteries = p.Has("no-start-on-battery");
            spec.StopIfGoingOnBatteries = p.Has("stop-on-battery");
        }

        private static void ApplyAdvancedConditionsUpdate(TaskUpdate update, ParsedArgs p)
        {
            string etl = p.Get("execution-time-limit");
            if (etl != null)
            {
                int minutes;
                if (!int.TryParse(etl, out minutes) || minutes < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "execution-time-limit 格式错误 '" + etl + "'");
                update.ExecutionTimeLimit = minutes == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
            }
            if (p.Has("no-start-on-battery")) update.DisallowStartIfOnBatteries = true;
            if (p.Has("stop-on-battery")) update.StopIfGoingOnBatteries = true;
        }
```

- [ ] **Step 5: 新增 JSON 触发器 DTO 类**

在 `SpecBuilder.cs` 末尾（namespace 内）添加：

```csharp
        private sealed class JsonTrigger
        {
            public string kind { get; set; }
            public string time { get; set; }
            public string date { get; set; }
            public string[] days { get; set; }
            public int? dayOfMonth { get; set; }
            public JsonIdleSettings idleSettings { get; set; }
            public string eventSubscription { get; set; }
            public string eventLog { get; set; }
            public string eventSource { get; set; }
            public int? eventId { get; set; }
        }

        private sealed class JsonIdleSettings
        {
            public string waitTimeout { get; set; }
            public string duration { get; set; }
            public bool stopOnIdleEnd { get; set; }
            public bool restartOnIdle { get; set; }
        }

        private static TriggerSpec JsonTriggerToSpec(JsonTrigger jt)
        {
            var spec = new TriggerSpec();
            if (jt == null || string.IsNullOrEmpty(jt.kind))
                throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "触发器缺少 kind 字段");

            switch (jt.kind.ToLowerInvariant())
            {
                case "once": spec.Kind = TriggerKind.Once; break;
                case "daily": spec.Kind = TriggerKind.Daily; break;
                case "weekly": spec.Kind = TriggerKind.Weekly; break;
                case "monthly": spec.Kind = TriggerKind.Monthly; break;
                case "boot": spec.Kind = TriggerKind.Boot; break;
                case "logon": spec.Kind = TriggerKind.Logon; break;
                case "idle": spec.Kind = TriggerKind.Idle; break;
                case "event": spec.Kind = TriggerKind.Event; break;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "未知触发器 kind: " + jt.kind);
            }

            if (!string.IsNullOrEmpty(jt.time))
            {
                TimeSpan time;
                if (!TimeSpan.TryParseExact(jt.time, @"hh\:mm", CultureInfo.InvariantCulture, out time))
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "time 格式错误 '" + jt.time + "'");
                spec.Time = time;
            }
            if (!string.IsNullOrEmpty(jt.date))
            {
                DateTime date;
                if (!DateTime.TryParseExact(jt.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "date 格式错误 '" + jt.date + "'");
                spec.Date = date;
            }
            if (jt.days != null)
                spec.Days = jt.days.Select(ParseDay).ToArray();
            if (jt.dayOfMonth.HasValue) spec.DayOfMonth = jt.dayOfMonth.Value;

            if (jt.idleSettings != null)
            {
                spec.IdleSettings = new IdleSettingsSpec
                {
                    StopOnIdleEnd = jt.idleSettings.stopOnIdleEnd,
                    RestartOnIdle = jt.idleSettings.restartOnIdle
                };
                if (!string.IsNullOrEmpty(jt.idleSettings.waitTimeout))
                {
                    TimeSpan w;
                    if (!TimeSpan.TryParse(jt.idleSettings.waitTimeout, out w))
                        throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "idleSettings.waitTimeout 格式错误");
                    spec.IdleSettings.WaitTimeout = w;
                }
                if (!string.IsNullOrEmpty(jt.idleSettings.duration))
                {
                    TimeSpan d;
                    if (!TimeSpan.TryParse(jt.idleSettings.duration, out d))
                        throw new TaskServiceException(ErrorCode.InvalidTriggerFormat, "idleSettings.duration 格式错误");
                    spec.IdleSettings.Duration = d;
                }
            }

            spec.EventSubscription = jt.eventSubscription;
            spec.EventLog = jt.eventLog;
            spec.EventSource = jt.eventSource;
            if (jt.eventId.HasValue) spec.EventId = jt.eventId;

            return spec;
        }
```

- [ ] **Step 6: 删除旧的 BuildTrigger 方法**

删除 `SpecBuilder.cs` 中原来的 `public static TriggerSpec BuildTrigger(ParsedArgs p, bool required)` 方法整体（已被 `BuildTriggers` + `BuildSingleTrigger` 替代）。

- [ ] **Step 7: 重写 TaskDto.cs**

完整替换 `src/ScheduleCenter/Cli/TaskDto.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class TaskDto
    {
        public static object From(TaskInfo t)
        {
            return new
            {
                name = t.RelativeName,
                folder = t.Folder,
                path = t.Path,
                args = t.Arguments,
                workdir = t.WorkingDirectory,
                description = t.Description,
                enabled = t.Enabled,
                state = t.State,
                runAsSystem = t.RunAsSystem,
                highest = t.Highest,
                triggers = (t.Triggers == null ? new List<object>() : t.Triggers.Select(TriggerDto).ToList()),
                nextRunTime = t.NextRunTime,
                lastRunTime = t.LastRunTime,
                lastResult = t.LastResult,
                executionTimeLimit = FmtTimeSpan(t.ExecutionTimeLimit),
                disallowStartIfOnBatteries = t.DisallowStartIfOnBatteries,
                stopIfGoingOnBatteries = t.StopIfGoingOnBatteries
            };
        }

        private static object TriggerDto(TriggerSpec spec)
        {
            if (spec == null) return null;
            switch (spec.Kind)
            {
                case TriggerKind.Once:
                    return new
                    {
                        kind = "once",
                        date = spec.Date.HasValue ? spec.Date.Value.ToString("yyyy-MM-dd") : null,
                        time = FmtTime(spec.Time)
                    };
                case TriggerKind.Daily:
                    return new { kind = "daily", time = FmtTime(spec.Time) };
                case TriggerKind.Weekly:
                    return new { kind = "weekly", time = FmtTime(spec.Time), days = spec.Days == null ? null : spec.Days.Select(DayCode).ToArray() };
                case TriggerKind.Monthly:
                    return new { kind = "monthly", time = FmtTime(spec.Time), dayOfMonth = spec.DayOfMonth };
                case TriggerKind.Boot:
                    return new { kind = "boot" };
                case TriggerKind.Logon:
                    return new { kind = "logon" };
                case TriggerKind.Idle:
                    return new
                    {
                        kind = "idle",
                        idleSettings = spec.IdleSettings == null ? null : new
                        {
                            waitTimeout = FmtTimeSpan(spec.IdleSettings.WaitTimeout),
                            duration = FmtTimeSpan(spec.IdleSettings.Duration),
                            stopOnIdleEnd = spec.IdleSettings.StopOnIdleEnd,
                            restartOnIdle = spec.IdleSettings.RestartOnIdle
                        }
                    };
                case TriggerKind.Event:
                    return new
                    {
                        kind = "event",
                        eventSubscription = spec.EventSubscription,
                        eventLog = spec.EventLog,
                        eventSource = spec.EventSource,
                        eventId = spec.EventId
                    };
                default:
                    return new { kind = spec.Kind.ToString().ToLowerInvariant() };
            }
        }

        private static string FmtTime(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm") : null;
        }

        private static string FmtTimeSpan(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm\:ss") : null;
        }

        private static string DayCode(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "MON";
                case DayOfWeek.Tuesday: return "TUE";
                case DayOfWeek.Wednesday: return "WED";
                case DayOfWeek.Thursday: return "THU";
                case DayOfWeek.Friday: return "FRI";
                case DayOfWeek.Saturday: return "SAT";
                default: return "SUN";
            }
        }
    }
}
```

- [ ] **Step 8: 编译 Cli 项目**

```bash
dotnet build src/ScheduleCenter/ScheduleCenter.csproj
```

Expected: 应能编译通过（除 CliRunner 还未处理 export/import，但那是新增 case 不影响编译）。

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(cli): spec builder supports multi-trigger json and advanced conditions"
```

---

### Task 8: CliCommandBuilder 扩展

**Files:**
- Modify: `src/ScheduleCenter.Core/CliCommandBuilder.cs`

**Interfaces:**
- Produces: 单触发器输出 V1 风格；多触发器输出 `--triggers-json`

- [ ] **Step 1: 重写 CliCommandBuilder.cs**

完整替换：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ScheduleCenter.Core
{
    public static class CliCommandBuilder
    {
        public static string BuildAddCommand(TaskInfo task)
        {
            var sb = new StringBuilder("ScheduleCenter add");
            sb.Append(" --name \"").Append(task.RelativeName).Append("\"");
            sb.Append(" --path \"").Append(task.Path).Append("\"");
            if (!string.IsNullOrEmpty(task.Arguments))
                sb.Append(" --args \"").Append(task.Arguments).Append("\"");
            if (!string.IsNullOrEmpty(task.WorkingDirectory))
                sb.Append(" --workdir \"").Append(task.WorkingDirectory).Append("\"");

            AppendTriggersArg(sb, task.Triggers);

            if (task.RunAsSystem) sb.Append(" --run-as-system");
            if (task.Highest) sb.Append(" --highest");
            if (!string.IsNullOrEmpty(task.Description))
                sb.Append(" --description \"").Append(task.Description).Append("\"");
            if (task.Enabled) sb.Append(" --enabled");

            AppendAdvancedConditions(sb, task);

            return sb.ToString();
        }

        private static void AppendTriggersArg(StringBuilder sb, IList<TriggerSpec> triggers)
        {
            if (triggers == null || triggers.Count == 0) return;

            if (triggers.Count == 1)
            {
                AppendSingleTriggerArgs(sb, triggers[0]);
                return;
            }

            // 多触发器：输出 --triggers-json
            var jsonTriggers = triggers.Select(t => new
            {
                kind = KindCode(t.Kind),
                time = t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : (string)null,
                date = t.Date.HasValue ? t.Date.Value.ToString("yyyy-MM-dd") : (string)null,
                days = t.Days == null ? null : t.Days.Select(DayCode).ToArray(),
                dayOfMonth = t.DayOfMonth,
                idleSettings = t.IdleSettings == null ? null : new
                {
                    waitTimeout = t.IdleSettings.WaitTimeout.HasValue ? t.IdleSettings.WaitTimeout.Value.ToString(@"hh\:mm\:ss") : (string)null,
                    duration = t.IdleSettings.Duration.HasValue ? t.IdleSettings.Duration.Value.ToString(@"hh\:mm\:ss") : (string)null,
                    stopOnIdleEnd = t.IdleSettings.StopOnIdleEnd,
                    restartOnIdle = t.IdleSettings.RestartOnIdle
                },
                eventSubscription = t.EventSubscription,
                eventLog = t.EventLog,
                eventSource = t.EventSource,
                eventId = t.EventId
            }).ToList();

            string json = JsonConvert.SerializeObject(jsonTriggers, Formatting.None);
            sb.Append(" --triggers-json '").Append(json).Append("'");
        }

        private static void AppendSingleTriggerArgs(StringBuilder sb, TriggerSpec trigger)
        {
            switch (trigger.Kind)
            {
                case TriggerKind.Once:
                    sb.Append(" --trigger once");
                    if (trigger.Date.HasValue) sb.Append(" --date ").Append(trigger.Date.Value.ToString("yyyy-MM-dd"));
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    break;
                case TriggerKind.Daily:
                    sb.Append(" --trigger daily");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    break;
                case TriggerKind.Weekly:
                    sb.Append(" --trigger weekly");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    if (trigger.Days != null)
                        sb.Append(" --days ").Append(string.Join(",", trigger.Days.Select(DayCode)));
                    break;
                case TriggerKind.Monthly:
                    sb.Append(" --trigger monthly");
                    if (trigger.Time.HasValue) sb.Append(" --time ").Append(FmtTime(trigger.Time.Value));
                    if (trigger.DayOfMonth.HasValue) sb.Append(" --day-of-month ").Append(trigger.DayOfMonth.Value);
                    break;
                case TriggerKind.Boot:
                    sb.Append(" --trigger boot");
                    break;
                case TriggerKind.Logon:
                    sb.Append(" --trigger logon");
                    break;
                case TriggerKind.Idle:
                    sb.Append(" --trigger idle");
                    if (trigger.IdleSettings != null)
                    {
                        if (trigger.IdleSettings.WaitTimeout.HasValue)
                            sb.Append(" --idle-wait ").Append((int)trigger.IdleSettings.WaitTimeout.Value.TotalMinutes);
                        if (trigger.IdleSettings.Duration.HasValue)
                            sb.Append(" --idle-duration ").Append((int)trigger.IdleSettings.Duration.Value.TotalMinutes);
                        if (trigger.IdleSettings.StopOnIdleEnd) sb.Append(" --idle-stop-on-end");
                        if (trigger.IdleSettings.RestartOnIdle) sb.Append(" --idle-restart");
                    }
                    break;
                case TriggerKind.Event:
                    sb.Append(" --trigger event");
                    if (!string.IsNullOrEmpty(trigger.EventSubscription))
                        sb.Append(" --event-subscription \"").Append(trigger.EventSubscription).Append("\"");
                    else if (!string.IsNullOrEmpty(trigger.EventLog))
                    {
                        sb.Append(" --event-log ").Append(trigger.EventLog);
                        if (!string.IsNullOrEmpty(trigger.EventSource))
                            sb.Append(" --event-source ").Append(trigger.EventSource);
                        if (trigger.EventId.HasValue)
                            sb.Append(" --event-id ").Append(trigger.EventId.Value);
                    }
                    break;
            }
        }

        private static void AppendAdvancedConditions(StringBuilder sb, TaskInfo task)
        {
            if (task.ExecutionTimeLimit.HasValue)
                sb.Append(" --execution-time-limit ").Append((int)task.ExecutionTimeLimit.Value.TotalMinutes);
            if (task.DisallowStartIfOnBatteries) sb.Append(" --no-start-on-battery");
            if (task.StopIfGoingOnBatteries) sb.Append(" --stop-on-battery");
        }

        private static string KindCode(TriggerKind k)
        {
            switch (k)
            {
                case TriggerKind.Once: return "once";
                case TriggerKind.Daily: return "daily";
                case TriggerKind.Weekly: return "weekly";
                case TriggerKind.Monthly: return "monthly";
                case TriggerKind.Boot: return "boot";
                case TriggerKind.Logon: return "logon";
                case TriggerKind.Idle: return "idle";
                case TriggerKind.Event: return "event";
                default: return k.ToString().ToLowerInvariant();
            }
        }

        private static string FmtTime(TimeSpan t)
        {
            return t.ToString(@"hh\:mm");
        }

        private static string DayCode(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "MON";
                case DayOfWeek.Tuesday: return "TUE";
                case DayOfWeek.Wednesday: return "WED";
                case DayOfWeek.Thursday: return "THU";
                case DayOfWeek.Friday: return "FRI";
                case DayOfWeek.Saturday: return "SAT";
                default: return "SUN";
            }
        }
    }
}
```

注意：`CliCommandBuilder` 在 `ScheduleCenter.Core` 项目中，需确认 `ScheduleCenter.Core.csproj` 已引用 `Newtonsoft.Json`。当前未引用，需添加。

- [ ] **Step 2: 给 Core 项目加 Newtonsoft.Json 引用**

`src/ScheduleCenter.Core/ScheduleCenter.Core.csproj` 的 `<ItemGroup>` 中追加：

```xml
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

- [ ] **Step 3: 编译**

```bash
dotnet build src/ScheduleCenter.Core/ScheduleCenter.Core.csproj
```

Expected: 编译通过。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(core): cli command builder supports multi-trigger json output"
```

---

### Task 9: CliRunner export/import 子命令

**Files:**
- Modify: `src/ScheduleCenter/Cli/CliRunner.cs`

**Interfaces:**
- Consumes: Task 5 的 `Export`/`ExportToFile`/`Import`/`ImportFromFile`

- [ ] **Step 1: 在 CliRunner.Run 的 switch 中新增 export/import case**

在 `case "history":` 之后、`default:` 之前插入：

```csharp
                    case "export":
                    {
                        string name = parsed.Require("name");
                        string outputPath = parsed.Get("output");
                        if (outputPath == null)
                        {
                            string xml = service.Export(name);
                            Console.Out.Write(xml);
                            Console.Out.Flush();
                        }
                        else
                        {
                            service.ExportToFile(name, outputPath);
                            OutputWriter.Success(new { success = true, command, name, path = outputPath });
                        }
                        return 0;
                    }
                    case "import":
                    {
                        string file = parsed.Require("file");
                        string name = parsed.Require("name");
                        bool force = parsed.Has("force");
                        TaskInfo imported = service.ImportFromFile(file, name, force);
                        OutputWriter.Success(new { success = true, command, name, task = TaskDto.From(imported) });
                        return 0;
                    }
```

注意：`import` 的 `--name` 设计为必填（spec §4.2 中 `--name` 可选，但为简化 CLI 解析且与 V1 风格一致，V2 实现中改为必填；若需从 XML 推断名称，调用方先解析 XML 取 URI）。此为对 spec 的小幅调整，在自审记录中说明。

- [ ] **Step 2: 编译全解决方案**

```bash
dotnet build ScheduleCenter.sln
```

Expected: 除 Tests 项目外全部编译通过。Tests 项目仍引用旧 `Trigger` 字段，下一任务修复。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(cli): export/import subcommands"
```

---

### Task 10: 修复测试项目编译

**Files:**
- Modify: `tests/ScheduleCenter.Core.Tests/AddGetListTests.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/UpdateDeleteRunTests.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/CliCommandBuilderTests.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/CliParserTests.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/SpecBuilderTests.cs`

**Interfaces:**
- Consumes: Task 1-9 的新 API

- [ ] **Step 1: 编译测试项目查看错误**

```bash
dotnet build tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj
```

记录所有编译错误（均为 `.Trigger` → `.Triggers` 字段重命名导致）。

- [ ] **Step 2: 逐一修复测试文件**

对每个测试文件中引用 `spec.Trigger` / `update.Trigger` / `info.Trigger` 的地方：
- `spec.Trigger = new TriggerSpec { ... }` → `spec.Triggers = new List<TriggerSpec> { new TriggerSpec { ... } }`
- `info.Trigger` → `info.Triggers[0]`
- `update.Trigger = ...` → `update.Triggers = new List<TriggerSpec> { ... }`

对 `CliCommandBuilderTests.cs` 中断言命令字符串的测试，确认期望字符串未变（单触发器输出格式 V1 兼容）；若涉及新触发器类型则补充新测试（Task 12 统一补）。

对 `CliParserTests.cs` 中测试 V1 语法的用例保持不变；新增 V2 参数解析测试放 Task 12。

对 `SpecBuilderTests.cs` 中调用 `BuildSpec`/`BuildUpdate` 的用例，把 `spec.Trigger` 改为 `spec.Triggers[0]`，断言逻辑不变。

- [ ] **Step 3: 编译测试项目**

```bash
dotnet build tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj
```

Expected: 编译通过。

- [ ] **Step 4: 运行现有测试确认未回归**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter "ClassName!=V2"
```

Expected: 除因系统未启用历史日志导致 Inconclusive 的用例外全部 Passed（与 V1 一致）。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: migrate v1 tests to triggers collection api"
```

---

### Task 11: GUI 编辑器改造

**Files:**
- Modify: `src/ScheduleCenter/Gui/EditorViewModel.cs`
- Modify: `src/ScheduleCenter/EditorWindow.xaml`
- Modify: `src/ScheduleCenter/EditorWindow.xaml.cs`

**Interfaces:**
- Consumes: Task 1-4 的新 Core API
- Produces: 触发器列表 + 编辑面板；高级条件区；支持 idle/event 触发器

- [ ] **Step 1: 重写 EditorViewModel.cs**

完整替换 `src/ScheduleCenter/Gui/EditorViewModel.cs`：

```csharp
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

        private bool _stopIfGoingOnBatteries = true;
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
        private string _idleDurationText = "10";
        public string IdleDurationText { get { return _idleDurationText; } set { Set(ref _idleDurationText, value); } }
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

        // ---- 触发器列表摘要 ----
        public string TriggerSummary(TriggerSpec t)
        {
            if (t == null) return "";
            switch (t.Kind)
            {
                case TriggerKind.Once:
                    return "一次性 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "") +
                           (t.Date.HasValue ? " " + t.Date.Value.ToString("yyyy-MM-dd") : "");
                case TriggerKind.Daily:
                    return "每日 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "");
                case TriggerKind.Weekly:
                    string days = t.Days == null ? "" : string.Join(",", Array.ConvertAll(t.Days, DayCode));
                    return "每周 " + (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "") + " [" + days + "]";
                case TriggerKind.Monthly:
                    return "每月 " + (t.DayOfMonth.HasValue ? t.DayOfMonth.Value.ToString() : "?") + "日 " +
                           (t.Time.HasValue ? t.Time.Value.ToString(@"hh\:mm") : "");
                case TriggerKind.Boot: return "开机时";
                case TriggerKind.Logon: return "登录时";
                case TriggerKind.Idle: return "空闲时";
                case TriggerKind.Event:
                    if (!string.IsNullOrEmpty(t.EventSubscription)) return "事件 (XPath)";
                    return "事件 " + (t.EventLog ?? "") + (t.EventId.HasValue ? "/" + t.EventId.Value : "");
                default: return t.Kind.ToString();
            }
        }

        private string DayCode(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "一";
                case DayOfWeek.Tuesday: return "二";
                case DayOfWeek.Wednesday: return "三";
                case DayOfWeek.Thursday: return "四";
                case DayOfWeek.Friday: return "五";
                case DayOfWeek.Saturday: return "六";
                default: return "日";
            }
        }

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
                _idleDurationText = t.IdleSettings.Duration.HasValue ? ((int)t.IdleSettings.Duration.Value.TotalMinutes).ToString() : "10";
                _idleStopOnEnd = t.IdleSettings.StopOnIdleEnd;
                _idleRestart = t.IdleSettings.RestartOnIdle;
            }
            else
            {
                _idleWaitText = "60"; _idleDurationText = "10"; _idleStopOnEnd = true; _idleRestart = false;
            }
            OnPropertyChanged("IdleWaitText"); OnPropertyChanged("IdleDurationText");
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
                int wait, dur;
                if (!int.TryParse(_idleWaitText, out wait) || wait < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "空闲等待时间必须为非负整数");
                if (!int.TryParse(_idleDurationText, out dur) || dur < 0)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "空闲持续时间必须为非负整数");
                t.IdleSettings = new IdleSettingsSpec
                {
                    WaitTimeout = TimeSpan.FromMinutes(wait),
                    Duration = TimeSpan.FromMinutes(dur),
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
                    Duration = src.IdleSettings.Duration,
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
                for (int i = 0; i < Triggers.Count; i++)
                {
                    _selectedTriggerIndex = i;
                    SyncPanelBackToList();
                }
                _selectedTriggerIndex = 0;

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
```

- [ ] **Step 2: 重写 EditorWindow.xaml**

完整替换：

```xml
<Window x:Class="ScheduleCenter.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding Title}" Width="560" Height="640"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <Grid Margin="8">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TabControl Grid.Row="0">
            <TabItem Header="常规">
                <StackPanel Margin="8">
                    <TextBlock Text="名称（可用 \ 表示子文件夹）:"/>
                    <TextBox Text="{Binding Name}" IsEnabled="{Binding IsNameEditable}" Margin="0,2,0,8"/>
                    <TextBlock Text="描述:"/>
                    <TextBox Text="{Binding Description}" Margin="0,2,0,8"/>
                    <TextBlock Text="程序路径:"/>
                    <Grid Margin="0,2,0,8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox Text="{Binding Path}"/>
                        <Button Grid.Column="1" Content="浏览..." Click="Browse_Click" Margin="4,0,0,0" Padding="8,2"/>
                    </Grid>
                    <TextBlock Text="参数:"/>
                    <TextBox Text="{Binding Arguments}" Margin="0,2,0,8"/>
                    <TextBlock Text="工作目录:"/>
                    <TextBox Text="{Binding WorkingDirectory}" Margin="0,2,0,8"/>
                    <CheckBox Content="以 SYSTEM 账户运行" IsChecked="{Binding RunAsSystem}" Margin="0,2"/>
                    <CheckBox Content="使用最高权限运行" IsChecked="{Binding Highest}" Margin="0,2"/>
                    <CheckBox Content="启用" IsChecked="{Binding Enabled}" Margin="0,2,8,0"/>

                    <GroupBox Header="高级条件" Margin="0,8,0,0">
                        <StackPanel Margin="4">
                            <StackPanel Orientation="Horizontal" Margin="0,2">
                                <TextBlock Text="执行时间限制(分钟, 0=无限): " VerticalAlignment="Center"/>
                                <TextBox Text="{Binding ExecutionTimeLimitText}" Width="80"/>
                            </StackPanel>
                            <CheckBox Content="电池供电时不启动" IsChecked="{Binding DisallowStartIfOnBatteries}" Margin="0,2"/>
                            <CheckBox Content="切换到电池时停止任务" IsChecked="{Binding StopIfGoingOnBatteries}" Margin="0,2"/>
                        </StackPanel>
                    </GroupBox>
                </StackPanel>
            </TabItem>
            <TabItem Header="触发器">
                <Grid Margin="8">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                        <Button Content="+ 新增触发器" Command="{Binding AddTriggerCommand}" Padding="6,2" Margin="0,0,4,0"/>
                        <Button Content="删除选中" Command="{Binding RemoveTriggerCommand}" Padding="6,2"/>
                    </StackPanel>

                    <Grid Grid.Row="1">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="180"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <ListBox Grid.Column="0"
                                 ItemsSource="{Binding Triggers}"
                                 SelectedIndex="{Binding SelectedTriggerIndex}"
                                 Margin="0,0,4,0">
                            <ListBox.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding ., Converter={x:Static local:TriggerSummaryConverter.Instance}}"/>
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>

                        <StackPanel Grid.Column="1">
                            <TextBlock Text="触发器类型:"/>
                            <ComboBox ItemsSource="{Binding TriggerTypes}"
                                      SelectedIndex="{Binding TriggerTypeIndex}" Margin="0,2,0,8"/>

                            <StackPanel Visibility="{Binding ShowTime}">
                                <TextBlock Text="时间 (HH:mm):"/>
                                <TextBox Text="{Binding TimeText}" Width="100" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                            </StackPanel>
                            <StackPanel Visibility="{Binding ShowDate}">
                                <TextBlock Text="日期:"/>
                                <DatePicker SelectedDate="{Binding Date}" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                            </StackPanel>
                            <StackPanel Visibility="{Binding ShowWeekdays}" Orientation="Horizontal" Margin="0,2,0,8">
                                <CheckBox Content="一" IsChecked="{Binding Mon}" Margin="0,0,6,0"/>
                                <CheckBox Content="二" IsChecked="{Binding Tue}" Margin="0,0,6,0"/>
                                <CheckBox Content="三" IsChecked="{Binding Wed}" Margin="0,0,6,0"/>
                                <CheckBox Content="四" IsChecked="{Binding Thu}" Margin="0,0,6,0"/>
                                <CheckBox Content="五" IsChecked="{Binding Fri}" Margin="0,0,6,0"/>
                                <CheckBox Content="六" IsChecked="{Binding Sat}" Margin="0,0,6,0"/>
                                <CheckBox Content="日" IsChecked="{Binding Sun}"/>
                            </StackPanel>
                            <StackPanel Visibility="{Binding ShowDayOfMonth}">
                                <TextBlock Text="每月第几天 (1-31):"/>
                                <TextBox Text="{Binding DayOfMonthText}" Width="60" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                            </StackPanel>

                            <StackPanel Visibility="{Binding ShowIdle}">
                                <TextBlock Text="空闲等待(分钟):"/>
                                <TextBox Text="{Binding IdleWaitText}" Width="80" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                                <TextBlock Text="空闲持续(分钟):"/>
                                <TextBox Text="{Binding IdleDurationText}" Width="80" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                                <CheckBox Content="空闲结束时停止任务" IsChecked="{Binding IdleStopOnEnd}" Margin="0,2,0,4"/>
                                <CheckBox Content="空闲中断后重新计时" IsChecked="{Binding IdleRestart}"/>
                            </StackPanel>

                            <StackPanel Visibility="{Binding ShowEvent}">
                                <CheckBox Content="使用完整 XPath" IsChecked="{Binding UseEventSubscription}" Margin="0,2,0,8"/>
                                <StackPanel Visibility="{Binding ShowEventSimple}">
                                    <TextBlock Text="日志名:"/>
                                    <TextBox Text="{Binding EventLog}" Margin="0,2,0,8"/>
                                    <TextBlock Text="源(可选):"/>
                                    <TextBox Text="{Binding EventSource}" Margin="0,2,0,8"/>
                                    <TextBlock Text="事件ID(可选):"/>
                                    <TextBox Text="{Binding EventIdText}" Width="100" HorizontalAlignment="Left" Margin="0,2,0,8"/>
                                </StackPanel>
                                <StackPanel Visibility="{Binding ShowEventXPath}">
                                    <TextBlock Text="XPath 订阅:"/>
                                    <TextBox Text="{Binding EventSubscription}" Height="100" AcceptsReturn="True"
                                             TextWrapping="Wrap" VerticalScrollBarVisibility="Visible" Margin="0,2,0,8"/>
                                </StackPanel>
                            </StackPanel>
                        </StackPanel>
                    </Grid>
                </Grid>
            </TabItem>
        </TabControl>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="保存" Command="{Binding SaveCommand}" Width="80" Margin="0,0,8,0"/>
            <Button Content="取消" Command="{Binding CancelCommand}" Width="80"/>
        </StackPanel>
    </Grid>
</Window>
```

注意：XAML 中引用 `local:TriggerSummaryConverter`，需在文件根 `Window` 标签加 `xmlns:local="clr-namespace:ScheduleCenter.Gui"`，并在代码中实现该 Converter（Step 3）。

- [ ] **Step 3: 新增 TriggerSummaryConverter**

在 `src/ScheduleCenter/Gui/` 新建文件 `TriggerSummaryConverter.cs`：

```csharp
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
```

- [ ] **Step 4: 确认 EditorWindow.xaml.cs 无需大改**

`EditorWindow.xaml.cs` 中 `Browse_Click` 等保持 V1 实现。确认 `DataContext = new EditorViewModel(service, existing)` 仍正确。若 XAML 根节点改了 xmlns，确认 code-behind 的 class 声明匹配。

- [ ] **Step 5: 编译 Gui**

```bash
dotnet build src/ScheduleCenter/ScheduleCenter.csproj
```

Expected: 编译通过。若有 `MainViewModel` 引用旧字段错误，进入 Task 12 修复。

- [ ] **Step 6: 手动验证清单**

```bash
dotnet build
```

1. CLI 添加多触发器任务：`ScheduleCenter.exe add --name GuiEditTest --path C:\Windows\System32\cmd.exe --triggers-json '[{"kind":"daily","time":"09:00"},{"kind":"weekly","time":"10:00","days":["MON"]}]'`
2. GUI 启动 → 双击 `GuiEditTest` → 触发器页显示列表 2 项 → 选中第 1 项面板显示"每日 09:00"，选中第 2 项显示"每周 10:00"
3. 点"+ 新增触发器" → 列表增加 1 项 → 选中新项 → 类型改为"空闲时" → 面板切换为 idle 控件 → 填等待 5、持续 10 → 保存
4. CLI `get --name GuiEditTest` 确认 `triggers` 数组有 3 项
5. 选中触发器页"事件"类型 → 简化参数填日志名 `System`、事件 ID `100` → 保存 → CLI `get` 确认 `eventLog:"System","eventId":100`
6. 高级条件：执行时间限制填 30 → 保存 → CLI `get` 确认 `executionTimeLimit:"00:30:00"`
7. 清理：`delete --name GuiEditTest --force`

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(gui): editor with trigger list, idle/event triggers, advanced conditions"
```

---

### Task 12: GUI 主窗口导入导出

**Files:**
- Modify: `src/ScheduleCenter/Gui/MainViewModel.cs`
- Modify: `src/ScheduleCenter/MainWindow.xaml`

**Interfaces:**
- Consumes: Task 5 的 `ExportToFile`/`ImportFromFile`

- [ ] **Step 1: 修复 MainViewModel 中旧字段引用**

`MainViewModel.cs` 中如有引用 `SelectedTask.Info.Trigger` 的地方改为 `SelectedTask.Info.Triggers`（如 `TaskRowViewModel` 内部）。`CopyCli` 调用 `Service.BuildAddCommand(current)` 不需改（`BuildAddCommand` 已在 Task 8 改造）。

- [ ] **Step 2: 新增导入导出命令**

在 `MainViewModel` 字段区追加：

```csharp
        public ICommand ExportCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
```

构造函数中追加：

```csharp
            ExportCommand = new RelayCommand(ExportTask, () => SelectedTask != null);
            ImportCommand = new RelayCommand(ImportTask);
```

新增方法：

```csharp
        internal void ExportTask()
        {
            if (SelectedTask == null) return;
            string name = SelectedTask.Info.RelativeName;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = name.Replace('\\', '_') + ".xml",
                Filter = "任务计划 XML (*.xml)|*.xml",
                Title = "导出任务"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Service.ExportToFile(name, dlg.FileName);
                StatusText = "已导出到: " + dlg.FileName;
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void ImportTask()
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "任务计划 XML (*.xml)|*.xml",
                Title = "导入任务"
            };
            if (openDlg.ShowDialog() != true) return;

            // 询问导入名称与是否覆盖
            string suggestedName = System.IO.Path.GetFileNameWithoutExtension(openDlg.FileName);
            var inputDlg = new InputDialog("导入任务", "导入到名称（可用 \\ 表示子文件夹）:", suggestedName);
            if (inputDlg.ShowDialog() != true) return;
            string newName = inputDlg.InputText;
            if (string.IsNullOrWhiteSpace(newName)) return;

            bool force = inputDlg.OverwriteExisting;
            try
            {
                Service.ImportFromFile(openDlg.FileName, newName, force);
                Refresh();
                StatusText = "已导入: " + newName;
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }
```

- [ ] **Step 3: 新建 InputDialog**

在 `src/ScheduleCenter/Gui/` 新建 `InputDialog.xaml`：

```xml
<Window x:Class="ScheduleCenter.Gui.InputDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="输入" Width="400" Height="180"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize">
    <StackPanel Margin="12">
        <TextBlock x:Name="PromptText" Margin="0,0,0,4"/>
        <TextBox x:Name="InputBox" Margin="0,0,0,8"/>
        <CheckBox x:Name="OverwriteCheckBox" Content="覆盖现有任务" Margin="0,0,0,8"/>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="确定" Click="OK_Click" Width="72" Margin="0,0,8,0"/>
            <Button Content="取消" Click="Cancel_Click" Width="72"/>
        </StackPanel>
    </StackPanel>
</Window>
```

`InputDialog.xaml.cs`：

```csharp
using System.Windows;

namespace ScheduleCenter.Gui
{
    public partial class InputDialog : Window
    {
        public string InputText { get { return InputBox.Text; } }
        public bool OverwriteExisting { get { return OverwriteCheckBox.IsChecked == true; } }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue ?? "";
            InputBox.SelectAll();
            InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
```

- [ ] **Step 4: 更新 MainWindow.xaml 工具栏**

`MainWindow.xaml` 工具栏中 `<Button Content="新建任务"...>` 之后、`<Button Content="刷新"...>` 之前插入：

```xml
                <Button Content="导入" Command="{Binding ImportCommand}" Padding="8,2"/>
                <Button Content="导出" Command="{Binding ExportCommand}" Padding="8,2"/>
```

右键菜单中 `<MenuItem Header="复制 CLI 命令"...>` 之前插入：

```xml
                            <MenuItem Header="导出..." Command="{Binding DataContext.ExportCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
```

- [ ] **Step 5: 编译全解决方案**

```bash
dotnet build ScheduleCenter.sln
```

Expected: 全部编译通过（除 Tests 待 Task 13 补充）。

- [ ] **Step 6: 手动验证清单**

1. CLI 添加任务：`ScheduleCenter.exe add --name GuiXmlTest --path C:\Windows\System32\cmd.exe --trigger daily --time 09:00`
2. GUI 选中 `GuiXmlTest` → 点"导出" → 选保存路径 → 确认文件生成
3. CLI 删除：`ScheduleCenter.exe delete --name GuiXmlTest --force`
4. GUI 点"导入" → 选刚才 XML → 输入名 `GuiXmlRestored` → 确定 → 列表出现 `GuiXmlRestored`
5. CLI `get --name GuiXmlRestored` 确认字段与原任务一致
6. 导入同名任务（不勾选覆盖）→ 应弹 `TASK_EXISTS` 错误
7. 导入同名任务（勾选覆盖）→ 成功
8. 清理：删除所有测试任务

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(gui): toolbar import/export with input dialog"
```

---

### Task 13: 测试补全与最终验证

**Files:**
- Create: `tests/ScheduleCenter.Core.Tests/MultiTriggerTests.cs`
- Create: `tests/ScheduleCenter.Core.Tests/IdleAndEventTriggerTests.cs`
- Create: `tests/ScheduleCenter.Core.Tests/AdvancedConditionsTests.cs`
- Create: `tests/ScheduleCenter.Core.Tests/XmlImportExportTests.cs`
- Modify: `tests/ScheduleCenter.Core.Tests/CliParserTests.cs`（追加 V2 用例）
- Modify: `tests/ScheduleCenter.Core.Tests/SpecBuilderTests.cs`（追加 V2 用例）
- Modify: `tests/ScheduleCenter.Core.Tests/CliCommandBuilderTests.cs`（追加多触发器输出用例）

**Interfaces:**
- Consumes: Task 1-9 全部新 API

- [ ] **Step 1: 写多触发器集成测试**

新建 `tests/ScheduleCenter.Core.Tests/MultiTriggerTests.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class MultiTriggerTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_MultipleTriggers_CreatesTaskWithAllTriggers()
        {
            string name = UniqueName("Multi");
            var spec = new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) },
                    new TriggerSpec { Kind = TriggerKind.Weekly, Time = new System.TimeSpan(10, 0, 0), Days = new[] { System.DayOfWeek.Monday } }
                }
            };
            Service.Add(spec);

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(2, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, info.Triggers[0].Kind);
            Assert.AreEqual(TriggerKind.Weekly, info.Triggers[1].Kind);
        }

        [TestMethod]
        public void Update_ReplacesEntireTriggerList()
        {
            string name = UniqueName("MultiUpd");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) },
                    new TriggerSpec { Kind = TriggerKind.Weekly, Time = new System.TimeSpan(10, 0, 0), Days = new[] { System.DayOfWeek.Monday } }
                }
            });

            Service.Update(new TaskUpdate
            {
                Name = name,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Boot }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Boot, info.Triggers[0].Kind);
        }

        [TestMethod]
        public void Update_NullTriggers_KeepsExisting()
        {
            string name = UniqueName("MultiKeep");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) }
                }
            });

            Service.Update(new TaskUpdate { Name = name, Description = "changed" });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, info.Triggers[0].Kind);
        }

        [TestMethod]
        public void Add_EmptyTriggers_ThrowsInvalidArguments()
        {
            string name = UniqueName("Empty");
            try
            {
                Service.Add(new TaskSpec
                {
                    Name = name,
                    Path = TestExe,
                    Triggers = new List<TriggerSpec>()
                });
                Assert.Fail("应抛异常");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }
    }
}
```

- [ ] **Step 2: 写 idle/event 触发器测试**

新建 `tests/ScheduleCenter.Core.Tests/IdleAndEventTriggerTests.cs`：

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class IdleAndEventTriggerTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_IdleTrigger_WithSettings_StoredAndReadBack()
        {
            string name = UniqueName("Idle");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec
                    {
                        Kind = TriggerKind.Idle,
                        IdleSettings = new IdleSettingsSpec
                        {
                            WaitTimeout = System.TimeSpan.FromMinutes(5),
                            Duration = System.TimeSpan.FromMinutes(10),
                            StopOnIdleEnd = true,
                            RestartOnIdle = false
                        }
                    }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Idle, info.Triggers[0].Kind);
            Assert.IsNotNull(info.Triggers[0].IdleSettings);
            Assert.AreEqual(System.TimeSpan.FromMinutes(5), info.Triggers[0].IdleSettings.WaitTimeout);
            Assert.AreEqual(System.TimeSpan.FromMinutes(10), info.Triggers[0].IdleSettings.Duration);
        }

        [TestMethod]
        public void Add_EventTrigger_SimplifiedArgs_GeneratesSubscription()
        {
            string name = UniqueName("Evt");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec
                    {
                        Kind = TriggerKind.Event,
                        EventLog = "System",
                        EventSource = "Microsoft-Windows-Kernel-Power",
                        EventId = 42
                    }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Event, info.Triggers[0].Kind);
            Assert.IsFalse(string.IsNullOrEmpty(info.Triggers[0].EventSubscription));
            StringAssert.Contains(info.Triggers[0].EventSubscription, "System");
            StringAssert.Contains(info.Triggers[0].EventSubscription, "Kernel-Power");
            StringAssert.Contains(info.Triggers[0].EventSubscription, "42");
        }

        [TestMethod]
        public void Add_EventTrigger_FullSubscription_StoredAsIs()
        {
            string name = UniqueName("EvtFull");
            string xpath = "<QueryList><Query Id=\"0\" Path=\"Application\"><Select Path=\"Application\">*[System[EventID=1000]]</Select></Query></QueryList>";
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Event, EventSubscription = xpath }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TriggerKind.Event, info.Triggers[0].Kind);
            Assert.AreEqual(xpath, info.Triggers[0].EventSubscription);
        }
    }
}
```

- [ ] **Step 3: 写高级条件测试**

新建 `tests/ScheduleCenter.Core.Tests/AdvancedConditionsTests.cs`：

```csharp
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class AdvancedConditionsTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_WithExecutionTimeLimit_StoredAndReadBack()
        {
            string name = UniqueName("Etl");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                ExecutionTimeLimit = TimeSpan.FromMinutes(30)
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TimeSpan.FromMinutes(30), info.ExecutionTimeLimit);
        }

        [TestMethod]
        public void Add_NoExecutionTimeLimit_ReadsAsNull()
        {
            string name = UniqueName("EtlNull");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } }
                // ExecutionTimeLimit 未设置，默认 null → TimeSpan.Zero 写入
            });

            TaskInfo info = Service.Get(name);
            Assert.IsNull(info.ExecutionTimeLimit);
        }

        [TestMethod]
        public void Add_WithPowerConditions_StoredAndReadBack()
        {
            string name = UniqueName("Pwr");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                DisallowStartIfOnBatteries = true,
                StopIfGoingOnBatteries = true
            });

            TaskInfo info = Service.Get(name);
            Assert.IsTrue(info.DisallowStartIfOnBatteries);
            Assert.IsTrue(info.StopIfGoingOnBatteries);
        }

        [TestMethod]
        public void Update_ChangesExecutionTimeLimit()
        {
            string name = UniqueName("EtlUpd");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                ExecutionTimeLimit = TimeSpan.FromMinutes(30)
            });

            Service.Update(new TaskUpdate { Name = name, ExecutionTimeLimit = TimeSpan.FromMinutes(60) });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TimeSpan.FromMinutes(60), info.ExecutionTimeLimit);
        }
    }
}
```

- [ ] **Step 4: 写 XML 导入导出测试**

新建 `tests/ScheduleCenter.Core.Tests/XmlImportExportTests.cs`：

```csharp
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class XmlImportExportTests : ServiceTestBase
    {
        [TestMethod]
        public void Export_ReturnsValidXml()
        {
            string name = UniqueName("Exp");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            Assert.IsFalse(string.IsNullOrEmpty(xml));
            XDocument doc = XDocument.Parse(xml); // 不抛异常即合法
            Assert.IsNotNull(doc.Root);
        }

        [TestMethod]
        public void ExportToFile_WritesFile()
        {
            string name = UniqueName("ExpF");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string tmpFile = Path.Combine(Path.GetTempPath(), "sc_test_" + name.Replace('\\', '_') + ".xml");
            try
            {
                Service.ExportToFile(name, tmpFile);
                Assert.IsTrue(File.Exists(tmpFile));
                string content = File.ReadAllText(tmpFile);
                XDocument.Parse(content);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [TestMethod]
        public void Import_AfterExport_ProducesEquivalentTaskInfo()
        {
            string origName = UniqueName("Orig");
            string restName = UniqueName("Rest");
            Service.Add(new TaskSpec
            {
                Name = origName,
                Path = TestExe,
                Arguments = "/c exit 0",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                },
                Description = "test desc"
            });

            string xml = Service.Export(origName);
            TaskInfo restored = Service.Import(xml, restName, false);

            Assert.AreEqual(TestExe, restored.Path);
            Assert.AreEqual("/c exit 0", restored.Arguments);
            Assert.AreEqual("test desc", restored.Description);
            Assert.AreEqual(1, restored.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, restored.Triggers[0].Kind);
        }

        [TestMethod]
        public void Import_DuplicateName_NoForce_ThrowsTaskExists()
        {
            string name = UniqueName("Dup");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            try
            {
                Service.Import(xml, name, false);
                Assert.Fail("应抛 TASK_EXISTS");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.TaskExists, ex.Code);
            }
        }

        [TestMethod]
        public void Import_DuplicateName_WithForce_Overwrites()
        {
            string name = UniqueName("Force");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "orig",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            // 修改 XML 中的参数再导入
            xml = xml.Replace("orig", "replaced");

            TaskInfo restored = Service.Import(xml, name, true);
            Assert.AreEqual("replaced", restored.Arguments);
        }

        [TestMethod]
        public void Import_InvalidXml_ThrowsXmlParseError()
        {
            string name = UniqueName("Bad");
            try
            {
                Service.Import("<not valid xml<<<", name, false);
                Assert.Fail("应抛 XML_PARSE_ERROR");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.XmlParseError, ex.Code);
            }
        }

        [TestMethod]
        public void ImportFromFile_NonExistentFile_ThrowsInvalidPath()
        {
            string name = UniqueName("NoFile");
            try
            {
                Service.ImportFromFile(@"C:\nonexistent_path_sc_test.xml", name, false);
                Assert.Fail("应抛 INVALID_PATH");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidPath, ex.Code);
            }
        }
    }
}
```

- [ ] **Step 5: 追加 CLI 解析与 SpecBuilder 单元测试**

在 `CliParserTests.cs` 末尾追加 V2 用例：

```csharp
        [TestMethod]
        public void Parse_TriggersJson_OptionRecognized()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"}]" });
            Assert.AreEqual("add", parsed.Command);
            Assert.IsTrue(parsed.Has("triggers-json"));
        }

        [TestMethod]
        public void Parse_TriggerAndTriggersJson_MutuallyExclusive_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "daily", "--time", "09:00",
                    "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"}]" });
                Assert.Fail("应抛互斥错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_EventLogAndEventSubscription_MutuallyExclusive_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "event", "--event-log", "System", "--event-subscription", "<q/>" });
                Assert.Fail("应抛互斥错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_IdleArgsWithNonIdleTrigger_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "daily", "--time", "09:00", "--idle-wait", "5" });
                Assert.Fail("应抛错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_ExportImport_CommandsRecognized()
        {
            var p1 = CliParser.Parse(new[] { "export", "--name", "X", "--output", "C:\\x.xml" });
            Assert.AreEqual("export", p1.Command);
            var p2 = CliParser.Parse(new[] { "import", "--file", "C:\\x.xml", "--name", "Y" });
            Assert.AreEqual("import", p2.Command);
        }
```

在 `SpecBuilderTests.cs` 末尾追加：

```csharp
        [TestMethod]
        public void BuildSpec_TriggersJson_ProducesMultipleTriggers()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"},{\"kind\":\"boot\"}]" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(2, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, spec.Triggers[0].Kind);
            Assert.AreEqual(TriggerKind.Boot, spec.Triggers[1].Kind);
        }

        [TestMethod]
        public void BuildSpec_SingleTrigger_V1Compat_ProducesOneElementList()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "daily", "--time", "09:00" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(1, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, spec.Triggers[0].Kind);
        }

        [TestMethod]
        public void BuildSpec_EventTrigger_SimplifiedArgs()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "event", "--event-log", "System", "--event-id", "42" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(1, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Event, spec.Triggers[0].Kind);
            Assert.AreEqual("System", spec.Triggers[0].EventLog);
            Assert.AreEqual(42, spec.Triggers[0].EventId);
        }

        [TestMethod]
        public void BuildSpec_ExecutionTimeLimit_Propagated()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "daily", "--time", "09:00", "--execution-time-limit", "30" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(System.TimeSpan.FromMinutes(30), spec.ExecutionTimeLimit);
        }
```

- [ ] **Step 6: 追加 CliCommandBuilder 多触发器输出测试**

在 `CliCommandBuilderTests.cs` 末尾追加：

```csharp
        [TestMethod]
        public void BuildAddCommand_SingleTrigger_V1StyleOutput()
        {
            var info = new TaskInfo
            {
                RelativeName = "X",
                Path = @"C:\app.exe",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            };
            string cmd = CliCommandBuilder.BuildAddCommand(info);
            StringAssert.Contains(cmd, "--trigger daily");
            StringAssert.Contains(cmd, "--time 09:00");
            Assert.IsFalse(cmd.Contains("--triggers-json"));
        }

        [TestMethod]
        public void BuildAddCommand_MultiTrigger_OutputsTriggersJson()
        {
            var info = new TaskInfo
            {
                RelativeName = "X",
                Path = @"C:\app.exe",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) },
                    new TriggerSpec { Kind = TriggerKind.Boot }
                }
            };
            string cmd = CliCommandBuilder.BuildAddCommand(info);
            StringAssert.Contains(cmd, "--triggers-json");
            Assert.IsFalse(cmd.Contains("--trigger daily"));
        }
```

- [ ] **Step 7: 运行全部测试（管理员终端）**

```bash
dotnet test ScheduleCenter.sln
```

Expected: 除因系统未启用历史日志导致 Inconclusive 的用例外全部 Passed。

- [ ] **Step 8: CLI 契约复核（管理员 cmd）**

```bat
REM V1 兼容回归
ScheduleCenter.exe add --name V2Reg --path C:\Windows\System32\cmd.exe --trigger daily --time 09:00
ScheduleCenter.exe get --name V2Reg

REM 多触发器
ScheduleCenter.exe add --name V2Multi --path C:\Windows\System32\cmd.exe --triggers-json "[{\"kind\":\"daily\",\"time\":\"09:00\"},{\"kind\":\"weekly\",\"time\":\"10:00\",\"days\":[\"MON\"]}]"
ScheduleCenter.exe get --name V2Multi
ScheduleCenter.exe list

REM 高级条件
ScheduleCenter.exe update --name V2Reg --execution-time-limit 30 --no-start-on-battery
ScheduleCenter.exe get --name V2Reg

REM XML 导入导出
ScheduleCenter.exe export --name V2Reg --output C:\temp\V2Reg.xml
ScheduleCenter.exe import --file C:\temp\V2Reg.xml --name V2Restored --force
ScheduleCenter.exe get --name V2Restored

REM 清理
ScheduleCenter.exe delete --name V2Reg --force
ScheduleCenter.exe delete --name V2Multi --force
ScheduleCenter.exe delete --name V2Restored --force
```

确认：
- `get` 输出含 `triggers` 数组字段（不再有 `trigger` 单字段）
- 多触发器任务 `triggers` 数组长度为 2
- 高级条件字段 `executionTimeLimit`、`disallowStartIfOnBatteries` 出现在 JSON
- 导入后任务字段与原任务一致

- [ ] **Step 9: GUI 冒烟**

启动 GUI → 新建/编辑（含多触发器、idle、event）/删除/启停/运行/复制 CLI（单触发器输出 V1 风格、多触发器输出 `--triggers-json`）/搜索/文件夹切换/历史/导出/导入 各一遍；`taskschd.msc` 确认任务都在 `ScheduleCenter` 文件夹下。

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "test: v2 coverage for multi-trigger, idle/event, advanced conditions, xml import/export"
```

---

## 自审记录

- **规格覆盖：** spec §2 Breaking Change → Task 1/3/7/8/10（模型改数组 + CLI 双轨 + JSON 输出 + GUI 改造）；§3 高级触发器与条件 → Task 1-4/6/7/11（idle/event + 多触发器 + 高级条件 + 校验 + CLI 参数 + GUI 编辑器）；§4 XML 导入导出 → Task 5/9/12（Core API + CLI 子命令 + GUI 工具栏）；§5 错误码 → Task 1（3 项新 ErrorCode）；§6 测试 → Task 13 全量覆盖。
- **Breaking change 边界：** 数据模型 `Trigger→Triggers`（Task 1）+ CLI JSON 输出 `trigger→triggers`（Task 7）+ CLI 输入语法保留 V1 兼容（Task 6/7 双轨）；调用方迁移清单见 spec §2.5。
- **对 spec 的小幅调整：**
  1. `import --name` 改为必填（spec §4.2 原为可选，从 XML URI 推断）。**理由：** 简化 CLI 解析，避免调用方依赖 XML 内部结构；GUI 已用 InputDialog 预填文件名作为默认值，体验无回退。
  2. import 时未支持触发器类型的"忽略 + warning"策略：实现中 `ReadTriggers` 自动跳过未知类型，但 V2 的 CLI JSON 输出未显式包含 `warnings` 字段。**理由：** warning 字段需要额外扫描 XML 原始节点数与回读 `Triggers.Count` 比较，工程复杂度高；V2 先保证导入成功 + `Triggers` 数组只含已支持类型，warning 字段若需要可 V2.1 补。
- **类型一致性：** `TriggerSpec.Kind` 枚举 8 项、`IdleSettingsSpec` 字段、`TaskSpec/TaskUpdate/TaskInfo` 的 `Triggers` 集合与高级条件字段、`ErrorCode` 3 项新值在前后任务间一致；`CliParser.ValidateMutualExclusion`、`SpecBuilder.BuildTriggers`、`ScheduledTaskService.BuildTrigger/ReadTriggers/ApplyAdvancedSettings` 方法签名一致。
- **C# 7.3 合规：** 全部代码使用经典 using/switch/委托，无 C# 8+ 语法（无 switch 表达式、无 using 声明、无可空引用类型）。
- **依赖变更：** `ScheduleCenter.Core.csproj` 新增 `Newtonsoft.Json` 引用（Task 8 Step 2），因为 `CliCommandBuilder` 多触发器输出需要 JSON 序列化。
