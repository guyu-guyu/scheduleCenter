# ScheduleCenter 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建 ScheduleCenter —— 单 exe 双模式（WPF GUI / CLI）Windows 任务计划管理工具，只管理 `\ScheduleCenter\` 文件夹内的任务。

**Architecture:** Core 类库封装 TaskScheduler NuGet（原生 Task Scheduler 2.0 COM API），CLI 与 WPF GUI 共用 Core；GUI 走 MVVM；CLI 输出 JSON + 退出码。

**Tech Stack:** .NET Framework 4.8（SDK 风格项目，构建需 .NET SDK 8+，运行无需安装运行时）、WPF、MSTest、TaskScheduler NuGet、Newtonsoft.Json。

**Spec:** `docs/superpowers/specs/2026-07-23-schedule-center-design.md`

## Global Constraints

- 目标框架全部项目 `net48`；C# 语言版本 7.3（禁止 switch 表达式、using 声明等 C# 8+ 语法）
- 所有任务操作限定 `\ScheduleCenter\` 文件夹（含子文件夹），系统任务不可见不可操作
- CLI 成功 → stdout 单行 JSON（camelCase）+ 退出码 0；失败 → stderr 单行 JSON + 非零退出码（2 参数错/3 权限/4 不存在/5 已存在/1 其他）
- ErrorCode 字符串：`INVALID_ARGUMENTS` `CONFIRM_REQUIRED` `TASK_NOT_FOUND` `TASK_EXISTS` `ACCESS_DENIED` `HISTORY_DISABLED` `INVALID_PATH` `INTERNAL_ERROR`
- 时间：本地时间 ISO 8601；`--time` 格式 `HH:mm`；`--date` 格式 `yyyy-MM-dd`
- 程序 manifest 声明 `requireAdministrator`
- 界面语言中文；程序不自动修改系统设置（历史日志禁用时只提示）
- 测试项目用真实 Task Scheduler 做集成测试，测试任务在 `\ScheduleCenter\Tests\` 下，命名 `Test_{guid}`，用完即删；非管理员运行时测试 Inconclusive

---

### Task 1: 解决方案与项目脚手架

**Files:**
- Create: `ScheduleCenter.sln`、`.gitignore`
- Create: `src/ScheduleCenter.Core/ScheduleCenter.Core.csproj`
- Create: `src/ScheduleCenter/ScheduleCenter.csproj`、`src/ScheduleCenter/Properties/app.manifest`、`src/ScheduleCenter/App.xaml`、`src/ScheduleCenter/App.xaml.cs`
- Create: `tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj`

**Interfaces:**
- Produces: 可构建的解决方案骨架；后续所有任务在其中添加代码文件。

- [ ] **Step 1: 创建 .gitignore**

```gitignore
bin/
obj/
.vs/
*.user
packages/
```

- [ ] **Step 2: 创建 Core 类库项目**

`src/ScheduleCenter.Core/ScheduleCenter.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>ScheduleCenter.Core</AssemblyName>
    <RootNamespace>ScheduleCenter.Core</RootNamespace>
    <LangVersion>7.3</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="TaskScheduler" Version="2.12.2" />
  </ItemGroup>
</Project>
```

> 注：若 `TaskScheduler 2.12.2` 还原失败，用 `dotnet add package TaskScheduler`（不带版本）安装最新 2.x 稳定版。

- [ ] **Step 3: 创建 WPF 主程序项目**

`src/ScheduleCenter/ScheduleCenter.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>ScheduleCenter</AssemblyName>
    <RootNamespace>ScheduleCenter</RootNamespace>
    <LangVersion>7.3</LangVersion>
    <ApplicationManifest>Properties\app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ScheduleCenter.Core\ScheduleCenter.Core.csproj" />
  </ItemGroup>
</Project>
```

`src/ScheduleCenter/Properties/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="ScheduleCenter.app"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

`src/ScheduleCenter/App.xaml`:

```xml
<Application x:Class="ScheduleCenter.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

`src/ScheduleCenter/App.xaml.cs`:

```csharp
using System.Windows;

namespace ScheduleCenter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }
    }
}
```

- [ ] **Step 4: 创建 MSTest 测试项目**

`tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="MSTest.TestAdapter" Version="2.2.10" />
    <PackageReference Include="MSTest.TestFramework" Version="2.2.10" />
    <PackageReference Include="TaskScheduler" Version="2.12.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ScheduleCenter.Core\ScheduleCenter.Core.csproj" />
    <ProjectReference Include="..\..\src\ScheduleCenter\ScheduleCenter.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: 创建解决方案并验证构建**

```bash
cd f:/Projects/scheduleCenter
dotnet new sln -n ScheduleCenter
dotnet sln add src/ScheduleCenter.Core/ScheduleCenter.Core.csproj src/ScheduleCenter/ScheduleCenter.csproj tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj
dotnet restore
dotnet build
```

Expected: `Build succeeded`，3 个项目全部编译通过。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution (Core lib, WPF app, MSTest project)"
```

---

### Task 2: Core 错误模型与数据模型

**Files:**
- Create: `src/ScheduleCenter.Core/Errors.cs`、`src/ScheduleCenter.Core/Models.cs`
- Test: `tests/ScheduleCenter.Core.Tests/ErrorsTests.cs`

**Interfaces:**
- Produces: `ErrorCode` 枚举、`TaskServiceException(Code, Message, ExitCode, CodeName)`、`TriggerKind`、`TriggerSpec`、`TaskSpec`、`TaskUpdate`、`TaskInfo`、`HistoryEvent` —— 后续所有任务使用。

- [ ] **Step 1: 写失败测试**

`tests/ScheduleCenter.Core.Tests/ErrorsTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class ErrorsTests
    {
        [TestMethod]
        public void ExitCode_MapsCorrectly()
        {
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidArguments, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.ConfirmRequired, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidPath, "x").ExitCode);
            Assert.AreEqual(3, new TaskServiceException(ErrorCode.AccessDenied, "x").ExitCode);
            Assert.AreEqual(4, new TaskServiceException(ErrorCode.TaskNotFound, "x").ExitCode);
            Assert.AreEqual(5, new TaskServiceException(ErrorCode.TaskExists, "x").ExitCode);
            Assert.AreEqual(1, new TaskServiceException(ErrorCode.HistoryDisabled, "x").ExitCode);
            Assert.AreEqual(1, new TaskServiceException(ErrorCode.InternalError, "x").ExitCode);
        }

        [TestMethod]
        public void CodeName_MatchesCliContract()
        {
            Assert.AreEqual("INVALID_ARGUMENTS", new TaskServiceException(ErrorCode.InvalidArguments, "x").CodeName);
            Assert.AreEqual("CONFIRM_REQUIRED", new TaskServiceException(ErrorCode.ConfirmRequired, "x").CodeName);
            Assert.AreEqual("TASK_NOT_FOUND", new TaskServiceException(ErrorCode.TaskNotFound, "x").CodeName);
            Assert.AreEqual("TASK_EXISTS", new TaskServiceException(ErrorCode.TaskExists, "x").CodeName);
            Assert.AreEqual("ACCESS_DENIED", new TaskServiceException(ErrorCode.AccessDenied, "x").CodeName);
            Assert.AreEqual("HISTORY_DISABLED", new TaskServiceException(ErrorCode.HistoryDisabled, "x").CodeName);
            Assert.AreEqual("INVALID_PATH", new TaskServiceException(ErrorCode.InvalidPath, "x").CodeName);
            Assert.AreEqual("INTERNAL_ERROR", new TaskServiceException(ErrorCode.InternalError, "x").CodeName);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ErrorsTests
```

Expected: 编译失败 `TaskServiceException 未定义`。

- [ ] **Step 3: 实现 Errors.cs**

`src/ScheduleCenter.Core/Errors.cs`:

```csharp
using System;

namespace ScheduleCenter.Core
{
    public enum ErrorCode
    {
        InvalidArguments,
        ConfirmRequired,
        TaskNotFound,
        TaskExists,
        AccessDenied,
        HistoryDisabled,
        InvalidPath,
        InternalError
    }

    public sealed class TaskServiceException : Exception
    {
        public ErrorCode Code { get; private set; }

        public TaskServiceException(ErrorCode code, string message, Exception inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        public int ExitCode
        {
            get
            {
                switch (Code)
                {
                    case ErrorCode.InvalidArguments:
                    case ErrorCode.ConfirmRequired:
                    case ErrorCode.InvalidPath:
                        return 2;
                    case ErrorCode.AccessDenied:
                        return 3;
                    case ErrorCode.TaskNotFound:
                        return 4;
                    case ErrorCode.TaskExists:
                        return 5;
                    default:
                        return 1;
                }
            }
        }

        public string CodeName
        {
            get
            {
                switch (Code)
                {
                    case ErrorCode.InvalidArguments: return "INVALID_ARGUMENTS";
                    case ErrorCode.ConfirmRequired: return "CONFIRM_REQUIRED";
                    case ErrorCode.TaskNotFound: return "TASK_NOT_FOUND";
                    case ErrorCode.TaskExists: return "TASK_EXISTS";
                    case ErrorCode.AccessDenied: return "ACCESS_DENIED";
                    case ErrorCode.HistoryDisabled: return "HISTORY_DISABLED";
                    case ErrorCode.InvalidPath: return "INVALID_PATH";
                    default: return "INTERNAL_ERROR";
                }
            }
        }
    }
}
```

- [ ] **Step 4: 实现 Models.cs**

`src/ScheduleCenter.Core/Models.cs`:

```csharp
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
```

- [ ] **Step 5: 运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ErrorsTests
```

Expected: 2 个测试全部 Passed。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): error model and data models"
```

---

### Task 3: Core 参数校验（TDD）

**Files:**
- Create: `src/ScheduleCenter.Core/TaskValidator.cs`
- Test: `tests/ScheduleCenter.Core.Tests/ValidatorTests.cs`

**Interfaces:**
- Consumes: `TaskServiceException`、`TaskSpec`、`TriggerSpec`（Task 2）
- Produces: `TaskValidator.ValidateName(string)`、`TaskValidator.ValidateSpec(TaskSpec)`、`TaskValidator.ValidateTrigger(TriggerSpec)` —— 被 Task 4 服务层与 Task 8 SpecBuilder 调用。

- [ ] **Step 1: 写失败测试**

`tests/ScheduleCenter.Core.Tests/ValidatorTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class ValidatorTests
    {
        private static void ExpectInvalid(Action action)
        {
            var ex = Assert.ThrowsException<TaskServiceException>(action);
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void ValidateName_Valid_Passes()
        {
            TaskValidator.ValidateName("Backup");
            TaskValidator.ValidateName("MyApp\\Backup");
        }

        [TestMethod]
        public void ValidateName_Invalid_Throws()
        {
            ExpectInvalid(() => TaskValidator.ValidateName(null));
            ExpectInvalid(() => TaskValidator.ValidateName(""));
            ExpectInvalid(() => TaskValidator.ValidateName("  "));
            ExpectInvalid(() => TaskValidator.ValidateName("a\\\\b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a/b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a*b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a'b"));
            ExpectInvalid(() => TaskValidator.ValidateName(" a "));
            ExpectInvalid(() => TaskValidator.ValidateName(".."));
        }

        [TestMethod]
        public void ValidateTrigger_Once_RequiresFutureDateTime()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Once }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Once,
                Date = DateTime.Today.AddDays(-1),
                Time = new TimeSpan(9, 0, 0)
            }));
            TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Once,
                Date = DateTime.Today.AddDays(1),
                Time = new TimeSpan(9, 0, 0)
            });
        }

        [TestMethod]
        public void ValidateTrigger_Weekly_RequiresTimeAndDays()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Weekly }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(9, 0, 0) }));
            TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Weekly,
                Time = new TimeSpan(9, 0, 0),
                Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday }
            });
        }

        [TestMethod]
        public void ValidateTrigger_Monthly_RequiresDayOfMonth1To31()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0) }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 0 }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 32 }));
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 15 });
        }

        [TestMethod]
        public void ValidateTrigger_BootAndLogon_NoExtraParams()
        {
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Boot });
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Logon });
        }

        [TestMethod]
        public void ValidateSpec_InvalidPath_ThrowsInvalidPath()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => TaskValidator.ValidateSpec(new TaskSpec
            {
                Name = "X",
                Path = @"C:\definitely\not\exists_12345.exe",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            }));
            Assert.AreEqual(ErrorCode.InvalidPath, ex.Code);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ValidatorTests
```

Expected: 编译失败 `TaskValidator 未定义`。

- [ ] **Step 3: 实现 TaskValidator.cs**

`src/ScheduleCenter.Core/TaskValidator.cs`:

```csharp
using System;
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
            ValidateTrigger(spec.Trigger);
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
                    break;
            }
        }

        private static void RequireTime(TriggerSpec t, string kindName)
        {
            if (!t.Time.HasValue)
                throw new TaskServiceException(ErrorCode.InvalidArguments, kindName + " 触发器需要时间参数");
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter ValidatorTests
```

Expected: 7 个测试全部 Passed。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): task name/spec/trigger validation"
```

---

### Task 4: Core 任务服务 — 创建/查询/列表

**Files:**
- Create: `src/ScheduleCenter.Core/ScheduledTaskService.cs`
- Create（桩，Task 6/7 完善）: `src/ScheduleCenter.Core/HistoryService.cs`、`src/ScheduleCenter.Core/CliCommandBuilder.cs`
- Test: `tests/ScheduleCenter.Core.Tests/ServiceTestBase.cs`、`tests/ScheduleCenter.Core.Tests/AddGetListTests.cs`

**Interfaces:**
- Consumes: Task 2 模型、Task 3 校验
- Produces: `ScheduledTaskService`（`const string RootFolderPath = @"\ScheduleCenter"`）含全部方法签名：`Add/Get/List/Update/Delete/SetEnabled/Run/GetHistory/BuildAddCommand`（本任务一次写出完整文件；Update/Delete 等在 Task 5 验收）

- [ ] **Step 1: 写测试基类**

`tests/ScheduleCenter.Core.Tests/ServiceTestBase.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public abstract class ServiceTestBase
    {
        protected const string TestFolder = "Tests";
        protected ScheduledTaskService Service;
        protected readonly List<string> Created = new List<string>();
        protected static readonly string TestExe = @"C:\Windows\System32\cmd.exe";

        [TestInitialize]
        public void BaseSetup()
        {
            using (var id = WindowsIdentity.GetCurrent())
            {
                if (!new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator))
                    Assert.Inconclusive("需要以管理员身份运行测试");
            }
            Service = new ScheduledTaskService();
        }

        protected string UniqueName(string prefix)
        {
            string name = TestFolder + "\\" + prefix + "_" + Guid.NewGuid().ToString("N");
            Created.Add(name);
            return name;
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            foreach (string name in Created)
            {
                try { Service.Delete(name, true); } catch { }
            }
        }
    }
}
```

- [ ] **Step 2: 写失败测试**

`tests/ScheduleCenter.Core.Tests/AddGetListTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class AddGetListTests : ServiceTestBase
    {
        private TaskSpec Spec(string name, TriggerSpec trigger)
        {
            return new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Description = "集成测试任务",
                Trigger = trigger,
                Highest = true
            };
        }

        [TestMethod]
        public void Add_AllTriggerKinds_CreatesTasks()
        {
            var triggers = new[]
            {
                new TriggerSpec { Kind = TriggerKind.Once, Date = DateTime.Today.AddDays(1), Time = new TimeSpan(9, 0, 0) },
                new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) },
                new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(9, 0, 0), Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday } },
                new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 15 },
                new TriggerSpec { Kind = TriggerKind.Boot },
                new TriggerSpec { Kind = TriggerKind.Logon }
            };

            foreach (var trigger in triggers)
            {
                string name = UniqueName(trigger.Kind.ToString());
                TaskInfo info = Service.Add(Spec(name, trigger));
                Assert.AreEqual(name, info.RelativeName);
                Assert.AreEqual(trigger.Kind, info.Trigger.Kind);
                Assert.IsTrue(info.Highest);

                TaskInfo got = Service.Get(name);
                Assert.AreEqual(TestExe, got.Path);
                Assert.AreEqual("/c exit 0", got.Arguments);
                Assert.AreEqual("集成测试任务", got.Description);
            }
        }

        [TestMethod]
        public void Add_DuplicateName_ThrowsTaskExists()
        {
            string name = UniqueName("Dup");
            Service.Add(Spec(name, new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }));
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                Service.Add(Spec(name, new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(10, 0, 0) })));
            Assert.AreEqual(ErrorCode.TaskExists, ex.Code);
        }

        [TestMethod]
        public void Get_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get("NoSuchTask_12345"));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void List_WithWildcardFilter_ReturnsMatches()
        {
            string nameA = UniqueName("FilterAA");
            string nameB = UniqueName("FilterAB");
            Service.Add(Spec(nameA, new TriggerSpec { Kind = TriggerKind.Boot }));
            Service.Add(Spec(nameB, new TriggerSpec { Kind = TriggerKind.Boot }));

            var all = Service.List(null);
            Assert.IsTrue(all.Any(t => t.RelativeName == nameA));
            Assert.IsTrue(all.Any(t => t.RelativeName == nameB));

            string leafPrefix = nameA.Split('\\')[1];
            var filtered = Service.List(leafPrefix.Substring(0, 8) + "*");
            Assert.IsTrue(filtered.Any(t => t.RelativeName == nameA));
        }
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter AddGetListTests
```

Expected: 编译失败 `ScheduledTaskService 未定义`。

- [ ] **Step 4: 实现 ScheduledTaskService.cs（完整文件）**

`src/ScheduleCenter.Core/ScheduledTaskService.cs`:

```csharp
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
            try { return folder.GetTask(leaf); }
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
```

- [ ] **Step 5: 补齐编译桩（Task 6/7 完善）**

`src/ScheduleCenter.Core/HistoryService.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace ScheduleCenter.Core
{
    public sealed class HistoryService
    {
        public IReadOnlyList<HistoryEvent> GetHistory(string taskFullPath, int? last, bool errorsOnly)
        {
            throw new NotImplementedException();
        }

        public bool IsLogEnabled()
        {
            throw new NotImplementedException();
        }
    }
}
```

`src/ScheduleCenter.Core/CliCommandBuilder.cs`:

```csharp
using System;

namespace ScheduleCenter.Core
{
    public static class CliCommandBuilder
    {
        public static string BuildAddCommand(TaskInfo task)
        {
            throw new NotImplementedException();
        }
    }
}
```

- [ ] **Step 6: 以管理员身份运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter AddGetListTests
```

Expected: 4 个测试全部 Passed（非管理员终端会全部 Inconclusive —— 需换管理员终端重跑）。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(core): scheduled task service with folder scoping"
```

---

### Task 5: 服务层验收 — 更新/删除/启停/运行/隔离

**Files:**
- Test: `tests/ScheduleCenter.Core.Tests/UpdateDeleteRunTests.cs`

**Interfaces:**
- Consumes: Task 4 已实现的 `Update(TaskUpdate)`、`Delete(string,bool)`、`SetEnabled(string,bool)`、`Run(string)`

- [ ] **Step 1: 写测试（实现已在 Task 4 完成，直接验证）**

`tests/ScheduleCenter.Core.Tests/UpdateDeleteRunTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.TaskScheduler;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class UpdateDeleteRunTests : ServiceTestBase
    {
        private TaskSpec DailySpec(string name)
        {
            return new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            };
        }

        [TestMethod]
        public void Update_ChangesTimeAndTriggerKind()
        {
            string name = UniqueName("Upd");
            Service.Add(DailySpec(name));

            Service.Update(new TaskUpdate
            {
                Name = name,
                Trigger = new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(10, 30, 0), Days = new[] { DayOfWeek.Wednesday } }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TriggerKind.Weekly, info.Trigger.Kind);
            Assert.AreEqual(new TimeSpan(10, 30, 0), info.Trigger.Time);
            CollectionAssert.AreEqual(new[] { DayOfWeek.Wednesday }, info.Trigger.Days);
        }

        [TestMethod]
        public void Update_ChangesPathAndSystemFlag()
        {
            string name = UniqueName("Upd2");
            Service.Add(DailySpec(name));

            Service.Update(new TaskUpdate
            {
                Name = name,
                Arguments = "/c exit 1",
                RunAsSystem = true,
                Highest = true
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual("/c exit 1", info.Arguments);
            Assert.IsTrue(info.RunAsSystem);
            Assert.IsTrue(info.Highest);
        }

        [TestMethod]
        public void Delete_WithoutForce_ThrowsConfirmRequired()
        {
            string name = UniqueName("Del");
            Service.Add(DailySpec(name));
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Delete(name, false));
            Assert.AreEqual(ErrorCode.ConfirmRequired, ex.Code);
            Service.Get(name); // 任务仍在
        }

        [TestMethod]
        public void Delete_WithForce_RemovesTask()
        {
            string name = UniqueName("Del2");
            Service.Add(DailySpec(name));
            Service.Delete(name, true);
            Created.Remove(name);
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get(name));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void SetEnabled_TogglesState()
        {
            string name = UniqueName("En");
            Service.Add(DailySpec(name));
            Service.SetEnabled(name, false);
            Assert.IsFalse(Service.Get(name).Enabled);
            Service.SetEnabled(name, true);
            Assert.IsTrue(Service.Get(name).Enabled);
        }

        [TestMethod]
        public void Run_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Run("NoSuch_12345"));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void Isolation_TaskOutsideScope_NotVisible()
        {
            string outsideName = "ScOutside_" + Guid.NewGuid().ToString("N");
            using (var ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.Triggers.Add(new DailyTrigger(1) { StartBoundary = DateTime.Today.AddHours(9) });
                td.Actions.Add(new ExecAction(TestExe, "/c exit 0", null));
                try
                {
                    ts.RootFolder.RegisterTaskDefinition(outsideName, td, TaskCreation.CreateOrUpdate,
                        null, null, TaskLogonType.InteractiveToken, null);

                    var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get(outsideName));
                    Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
                    Assert.IsFalse(Service.List(null).Any(t => t.Name == outsideName));

                    var delEx = Assert.ThrowsException<TaskServiceException>(() => Service.Delete(outsideName, true));
                    Assert.AreEqual(ErrorCode.TaskNotFound, delEx.Code);
                }
                finally
                {
                    ts.RootFolder.DeleteTask(outsideName, false);
                }
            }
        }
    }
}
```

- [ ] **Step 2: 以管理员身份运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter UpdateDeleteRunTests
```

Expected: 7 个测试全部 Passed。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "test(core): update/delete/enable/run/isolation coverage"
```

---

### Task 6: Core 历史记录服务

**Files:**
- Modify: `src/ScheduleCenter.Core/HistoryService.cs`（替换 Task 4 的桩实现）
- Test: `tests/ScheduleCenter.Core.Tests/HistoryTests.cs`

**Interfaces:**
- Produces: `HistoryService.GetHistory(string taskFullPath, int? last, bool errorsOnly)`（taskFullPath 形如 `\ScheduleCenter\Backup`）、`HistoryService.IsLogEnabled()` —— GUI 历史页签（Task 14）也使用。

- [ ] **Step 1: 写失败测试**

`tests/ScheduleCenter.Core.Tests/HistoryTests.cs`:

```csharp
using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class HistoryTests : ServiceTestBase
    {
        [TestMethod]
        public void GetHistory_AfterRun_ReturnsEvents()
        {
            var history = new HistoryService();
            if (!history.IsLogEnabled())
                Assert.Inconclusive("系统未启用任务历史记录日志");

            string name = UniqueName("Hist");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            });
            Service.Run(name);

            // 事件日志写入有延迟，最多等 15 秒
            bool found = false;
            for (int i = 0; i < 15 && !found; i++)
            {
                Thread.Sleep(1000);
                var events = Service.GetHistory(name, 10, false);
                found = events.Count > 0;
            }
            Assert.IsTrue(found, "运行任务后 15 秒内应能读到历史事件");
        }

        [TestMethod]
        public void GetHistory_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.GetHistory("NoSuch_12345", null, false));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter HistoryTests
```

Expected: `NotImplementedException`。

- [ ] **Step 3: 实现 HistoryService.cs（完整替换）**

```csharp
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
```

- [ ] **Step 4: 以管理员身份运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter HistoryTests
```

Expected: 2 个测试 Passed（若系统未启用历史日志，第一个 Inconclusive，属预期）。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): task history via TaskScheduler operational event log"
```

---

### Task 7: Core CLI 命令生成（复制 CLI 命令功能）

**Files:**
- Modify: `src/ScheduleCenter.Core/CliCommandBuilder.cs`（替换桩实现）
- Test: `tests/ScheduleCenter.Core.Tests/CliCommandBuilderTests.cs`

**Interfaces:**
- Produces: `CliCommandBuilder.BuildAddCommand(TaskInfo) -> string` —— GUI "复制 CLI 命令"（Task 13）使用。

- [ ] **Step 1: 写失败测试**

`tests/ScheduleCenter.Core.Tests/CliCommandBuilderTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class CliCommandBuilderTests
    {
        [TestMethod]
        public void BuildAddCommand_WeeklyTask_ProducesFullCommand()
        {
            var info = new TaskInfo
            {
                RelativeName = "MyApp\\Backup",
                Path = @"C:\app.exe",
                Arguments = "--full",
                WorkingDirectory = @"C:\work",
                Description = "备份任务",
                Enabled = true,
                Highest = true,
                RunAsSystem = false,
                Trigger = new TriggerSpec
                {
                    Kind = TriggerKind.Weekly,
                    Time = new TimeSpan(9, 0, 0),
                    Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday }
                }
            };

            string cmd = CliCommandBuilder.BuildAddCommand(info);

            StringAssert.Contains(cmd, "add");
            StringAssert.Contains(cmd, "--name \"MyApp\\Backup\"");
            StringAssert.Contains(cmd, "--path \"C:\\app.exe\"");
            StringAssert.Contains(cmd, "--args \"--full\"");
            StringAssert.Contains(cmd, "--workdir \"C:\\work\"");
            StringAssert.Contains(cmd, "--trigger weekly");
            StringAssert.Contains(cmd, "--time 09:00");
            StringAssert.Contains(cmd, "--days MON,FRI");
            StringAssert.Contains(cmd, "--description \"备份任务\"");
            StringAssert.Contains(cmd, "--highest");
            Assert.IsFalse(cmd.Contains("--run-as-system"));
        }

        [TestMethod]
        public void BuildAddCommand_BootSystemTask_OmitsTimeParams()
        {
            var info = new TaskInfo
            {
                RelativeName = "Svc",
                Path = @"C:\svc.exe",
                Enabled = true,
                RunAsSystem = true,
                Trigger = new TriggerSpec { Kind = TriggerKind.Boot }
            };

            string cmd = CliCommandBuilder.BuildAddCommand(info);

            StringAssert.Contains(cmd, "--trigger boot");
            StringAssert.Contains(cmd, "--run-as-system");
            Assert.IsFalse(cmd.Contains("--time"));
            Assert.IsFalse(cmd.Contains("--days"));
        }

        [TestMethod]
        public void BuildAddCommand_OnceTask_IncludesDate()
        {
            var info = new TaskInfo
            {
                RelativeName = "Once",
                Path = @"C:\app.exe",
                Trigger = new TriggerSpec
                {
                    Kind = TriggerKind.Once,
                    Date = new DateTime(2026, 8, 1),
                    Time = new TimeSpan(8, 30, 0)
                }
            };

            string cmd = CliCommandBuilder.BuildAddCommand(info);

            StringAssert.Contains(cmd, "--trigger once");
            StringAssert.Contains(cmd, "--date 2026-08-01");
            StringAssert.Contains(cmd, "--time 08:30");
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter CliCommandBuilderTests
```

Expected: `NotImplementedException`。

- [ ] **Step 3: 实现 CliCommandBuilder.cs（完整替换）**

```csharp
using System;
using System.Linq;
using System.Text;

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
            AppendTriggerArgs(sb, task.Trigger);
            if (task.RunAsSystem) sb.Append(" --run-as-system");
            if (task.Highest) sb.Append(" --highest");
            if (!string.IsNullOrEmpty(task.Description))
                sb.Append(" --description \"").Append(task.Description).Append("\"");
            if (task.Enabled) sb.Append(" --enabled");
            return sb.ToString();
        }

        private static void AppendTriggerArgs(StringBuilder sb, TriggerSpec trigger)
        {
            if (trigger == null) return;
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

- [ ] **Step 4: 运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter CliCommandBuilderTests
```

Expected: 3 个测试全部 Passed。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): build equivalent add command from task info"
```

---

### Task 8: CLI 参数解析与规格构建（TDD）

**Files:**
- Create: `src/ScheduleCenter/Cli/CliParser.cs`、`src/ScheduleCenter/Cli/SpecBuilder.cs`
- Test: `tests/ScheduleCenter.Core.Tests/CliParserTests.cs`、`tests/ScheduleCenter.Core.Tests/SpecBuilderTests.cs`

**Interfaces:**
- Consumes: Task 2/3 的模型与校验
- Produces:
  - `CliParser.Parse(string[] args) -> ParsedArgs`；`ParsedArgs.Command`、`Has(string)`、`Get(string)`、`Require(string)`；`CliParser.Usage()`
  - `SpecBuilder.BuildSpec(ParsedArgs) -> TaskSpec`、`SpecBuilder.BuildUpdate(ParsedArgs) -> TaskUpdate`、`SpecBuilder.BuildTrigger(ParsedArgs, bool required) -> TriggerSpec`、`SpecBuilder.ParseDay(string) -> DayOfWeek`
  - 供 Task 9 的 CliRunner 调用。

- [ ] **Step 1: 写失败测试**

`tests/ScheduleCenter.Core.Tests/CliParserTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Cli;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class CliParserTests
    {
        [TestMethod]
        public void Parse_ValidAdd_AllOptionsCaptured()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "MyApp\\Backup", "--path", @"C:\app.exe",
                "--trigger", "weekly", "--time", "09:00", "--days", "MON,FRI",
                "--highest", "--enabled"
            });
            Assert.AreEqual("add", p.Command);
            Assert.AreEqual("MyApp\\Backup", p.Get("name"));
            Assert.AreEqual(@"C:\app.exe", p.Get("path"));
            Assert.AreEqual("weekly", p.Get("trigger"));
            Assert.AreEqual("09:00", p.Get("time"));
            Assert.AreEqual("MON,FRI", p.Get("days"));
            Assert.IsTrue(p.Has("highest"));
            Assert.IsTrue(p.Has("enabled"));
            Assert.IsFalse(p.Has("force"));
        }

        [TestMethod]
        public void Parse_ValueStartingWithDoubleDash_AllowedWhenNotKnownOption()
        {
            var p = CliParser.Parse(new[] { "add", "--name", "X", "--args", "--full" });
            Assert.AreEqual("--full", p.Get("args"));
        }

        [TestMethod]
        public void Parse_UnknownCommand_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => CliParser.Parse(new[] { "explode" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Parse_MissingValue_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                CliParser.Parse(new[] { "add", "--name", "--trigger", "daily" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Parse_UnknownOption_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                CliParser.Parse(new[] { "add", "--name", "X", "--bogus", "1" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Require_MissingOption_Throws()
        {
            var p = CliParser.Parse(new[] { "get", "--name", "X" });
            var ex = Assert.ThrowsException<TaskServiceException>(() => p.Require("path"));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }
    }
}
```

`tests/ScheduleCenter.Core.Tests/SpecBuilderTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Cli;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class SpecBuilderTests
    {
        private static readonly string TestExe = @"C:\Windows\System32\cmd.exe";

        [TestMethod]
        public void BuildSpec_Weekly_ParsesTrigger()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "weekly", "--time", "09:00", "--days", "MON,WED,FRI"
            });
            TaskSpec spec = SpecBuilder.BuildSpec(p);

            Assert.AreEqual("Backup", spec.Name);
            Assert.AreEqual(TriggerKind.Weekly, spec.Trigger.Kind);
            Assert.AreEqual(new TimeSpan(9, 0, 0), spec.Trigger.Time);
            CollectionAssert.AreEqual(
                new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
                spec.Trigger.Days);
            Assert.IsTrue(spec.Enabled);
        }

        [TestMethod]
        public void BuildSpec_BadTimeFormat_Throws()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "daily", "--time", "9点"
            });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildSpec(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void BuildSpec_BadDay_Throws()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "weekly", "--time", "09:00", "--days", "周一"
            });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildSpec(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void BuildUpdate_OnlyProvidedOptions()
        {
            var p = CliParser.Parse(new[] { "update", "--name", "Backup", "--time", "10:00", "--trigger", "daily" });
            TaskUpdate u = SpecBuilder.BuildUpdate(p);

            Assert.AreEqual("Backup", u.Name);
            Assert.IsNull(u.Path);
            Assert.IsNotNull(u.Trigger);
            Assert.AreEqual(TriggerKind.Daily, u.Trigger.Kind);
        }

        [TestMethod]
        public void BuildUpdate_NothingToChange_Throws()
        {
            var p = CliParser.Parse(new[] { "update", "--name", "Backup" });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildUpdate(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter "CliParserTests|SpecBuilderTests"
```

Expected: 编译失败 `ScheduleCenter.Cli 命名空间不存在`。

- [ ] **Step 3: 实现 CliParser.cs**

`src/ScheduleCenter/Cli/CliParser.cs`:

```csharp
using System;
using System.Collections.Generic;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public sealed class ParsedArgs
    {
        public string Command { get; internal set; }

        private readonly Dictionary<string, string> _options =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool Has(string key)
        {
            return _options.ContainsKey(key);
        }

        public string Get(string key)
        {
            string v;
            return _options.TryGetValue(key, out v) ? v : null;
        }

        public string Require(string key)
        {
            string v = Get(key);
            if (v == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少必需参数 --" + key);
            return v;
        }

        internal void Set(string key, string value)
        {
            _options[key] = value;
        }
    }

    public static class CliParser
    {
        private static readonly HashSet<string> Commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "add", "update", "delete", "get", "list", "enable", "disable", "run", "history" };

        private static readonly HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "force", "run-as-system", "highest", "enabled", "start-when-available", "errors-only" };

        private static readonly HashSet<string> ValueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "name", "path", "args", "workdir", "trigger", "time", "date", "days", "day-of-month", "description", "filter", "last" };

        public static ParsedArgs Parse(string[] args)
        {
            if (args == null || args.Length == 0)
                throw new TaskServiceException(ErrorCode.InvalidArguments, Usage());

            string command = args[0].ToLowerInvariant();
            if (!Commands.Contains(command))
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "未知命令 '" + args[0] + "'\n" + Usage());

            var result = new ParsedArgs { Command = command };
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("--") || arg.Length == 2)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "无法识别的参数 '" + arg + "'");

                string key = arg.Substring(2);
                if (Flags.Contains(key))
                {
                    result.Set(key, "true");
                }
                else if (ValueOptions.Contains(key))
                {
                    if (i + 1 >= args.Length)
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "参数 --" + key + " 缺少值");
                    string value = args[i + 1];
                    if (value.StartsWith("--") && value.Length > 2 &&
                        (ValueOptions.Contains(value.Substring(2)) || Flags.Contains(value.Substring(2))))
                        throw new TaskServiceException(ErrorCode.InvalidArguments, "参数 --" + key + " 缺少值");
                    result.Set(key, value);
                    i++;
                }
                else
                {
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知选项 --" + key);
                }
            }
            return result;
        }

        public static string Usage()
        {
            return "用法: ScheduleCenter <add|update|delete|get|list|enable|disable|run|history> [选项]\n" +
                   "示例: ScheduleCenter add --name Backup --path C:\\app.exe --trigger daily --time 09:00";
        }
    }
}
```

- [ ] **Step 4: 实现 SpecBuilder.cs**

`src/ScheduleCenter/Cli/SpecBuilder.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class SpecBuilder
    {
        public static TaskSpec BuildSpec(ParsedArgs p)
        {
            var spec = new TaskSpec
            {
                Name = p.Require("name"),
                Path = p.Require("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Trigger = BuildTrigger(p, true),
                Enabled = true,
                RunAsSystem = p.Has("run-as-system"),
                Highest = p.Has("highest"),
                StartWhenAvailable = true
            };
            TaskValidator.ValidateSpec(spec);
            return spec;
        }

        public static TaskUpdate BuildUpdate(ParsedArgs p)
        {
            var update = new TaskUpdate
            {
                Name = p.Require("name"),
                Path = p.Get("path"),
                Arguments = p.Get("args"),
                WorkingDirectory = p.Get("workdir"),
                Description = p.Get("description"),
                Trigger = BuildTrigger(p, false),
                RunAsSystem = p.Has("run-as-system") ? true : (bool?)null,
                Highest = p.Has("highest") ? true : (bool?)null,
                Enabled = p.Has("enabled") ? true : (bool?)null
            };

            if (update.Path == null && update.Arguments == null && update.WorkingDirectory == null &&
                update.Description == null && update.Trigger == null && update.RunAsSystem == null &&
                update.Highest == null && update.Enabled == null)
                throw new TaskServiceException(ErrorCode.InvalidArguments, "update 命令至少需要一个要修改的选项");

            if (update.Path != null && !System.IO.File.Exists(update.Path))
                throw new TaskServiceException(ErrorCode.InvalidPath, "程序路径不存在: " + update.Path);
            return update;
        }

        public static TriggerSpec BuildTrigger(ParsedArgs p, bool required)
        {
            string type = p.Get("trigger");
            if (type == null)
            {
                if (required)
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "缺少必需参数 --trigger");
                return null;
            }

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
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "未知触发器类型 '" + type + "'");
            }

            TaskValidator.ValidateTrigger(spec);
            return spec;
        }

        public static DayOfWeek ParseDay(string s)
        {
            switch ((s ?? "").Trim().ToUpperInvariant())
            {
                case "MON": return DayOfWeek.Monday;
                case "TUE": return DayOfWeek.Tuesday;
                case "WED": return DayOfWeek.Wednesday;
                case "THU": return DayOfWeek.Thursday;
                case "FRI": return DayOfWeek.Friday;
                case "SAT": return DayOfWeek.Saturday;
                case "SUN": return DayOfWeek.Sunday;
                default:
                    throw new TaskServiceException(ErrorCode.InvalidArguments, "非法星期值 '" + s + "'，应为 MON-SUN");
            }
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

```bash
dotnet test tests/ScheduleCenter.Core.Tests --filter "CliParserTests|SpecBuilderTests"
```

Expected: 11 个测试全部 Passed。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(cli): argument parser and spec builder"
```

---

### Task 9: CLI 运行器与双模式入口

**Files:**
- Create: `src/ScheduleCenter/Cli/TaskDto.cs`、`src/ScheduleCenter/Cli/OutputWriter.cs`、`src/ScheduleCenter/Cli/CliRunner.cs`、`src/ScheduleCenter/Cli/ConsoleHelper.cs`
- Modify: `src/ScheduleCenter/App.xaml.cs`
- Create（桩，Task 10+ 完善）: `src/ScheduleCenter/MainWindow.xaml`、`src/ScheduleCenter/MainWindow.xaml.cs`、`src/ScheduleCenter/Gui/ErrorLogger.cs`

**Interfaces:**
- Consumes: Task 8 的 `CliParser`/`SpecBuilder`，Core 全部服务方法
- Produces: `CliRunner.Run(string[] args) -> int`；`ConsoleHelper.EnsureConsole()` —— App.OnStartup 调用。

- [ ] **Step 1: 实现 TaskDto.cs（JSON 形状契约）**

`src/ScheduleCenter/Cli/TaskDto.cs`:

```csharp
using System;
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
                trigger = TriggerDto(t.Trigger),
                nextRunTime = t.NextRunTime,
                lastRunTime = t.LastRunTime,
                lastResult = t.LastResult
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
                        type = "once",
                        date = spec.Date.HasValue ? spec.Date.Value.ToString("yyyy-MM-dd") : null,
                        time = FmtTime(spec.Time)
                    };
                case TriggerKind.Daily:
                    return new { type = "daily", time = FmtTime(spec.Time) };
                case TriggerKind.Weekly:
                    return new { type = "weekly", time = FmtTime(spec.Time), days = spec.Days == null ? null : spec.Days.Select(DayCode).ToArray() };
                case TriggerKind.Monthly:
                    return new { type = "monthly", time = FmtTime(spec.Time), dayOfMonth = spec.DayOfMonth };
                case TriggerKind.Boot:
                    return new { type = "boot" };
                default:
                    return new { type = "logon" };
            }
        }

        private static string FmtTime(TimeSpan? t)
        {
            return t.HasValue ? t.Value.ToString(@"hh\:mm") : null;
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

- [ ] **Step 2: 实现 OutputWriter.cs**

`src/ScheduleCenter/Cli/OutputWriter.cs`:

```csharp
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class OutputWriter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None
        };

        public static void Success(object payload)
        {
            Console.Out.WriteLine(JsonConvert.SerializeObject(payload, Settings));
        }

        public static void Error(string command, TaskServiceException ex)
        {
            Console.Error.WriteLine(JsonConvert.SerializeObject(new
            {
                success = false,
                command,
                error = new { code = ex.CodeName, message = ex.Message }
            }, Settings));
        }
    }
}
```

- [ ] **Step 3: 实现 ConsoleHelper.cs**

`src/ScheduleCenter/Cli/ConsoleHelper.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace ScheduleCenter.Cli
{
    internal static class ConsoleHelper
    {
        private const int AttachParentProcess = -1;
        private const int StdOutputHandle = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        public static void EnsureConsole()
        {
            // 已被重定向（管道）时 std handle 已存在，不附加父控制台
            if (GetStdHandle(StdOutputHandle) == IntPtr.Zero)
                AttachConsole(AttachParentProcess);
        }
    }
}
```

- [ ] **Step 4: 实现 CliRunner.cs**

`src/ScheduleCenter/Cli/CliRunner.cs`:

```csharp
using System;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class CliRunner
    {
        public static int Run(string[] args)
        {
            string command = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
            try
            {
                ParsedArgs parsed = CliParser.Parse(args);
                command = parsed.Command;
                var service = new ScheduledTaskService();

                switch (parsed.Command)
                {
                    case "add":
                    {
                        TaskInfo added = service.Add(SpecBuilder.BuildSpec(parsed));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(added) });
                        return 0;
                    }
                    case "update":
                    {
                        TaskInfo updated = service.Update(SpecBuilder.BuildUpdate(parsed));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(updated) });
                        return 0;
                    }
                    case "delete":
                    {
                        string name = parsed.Require("name");
                        service.Delete(name, parsed.Has("force"));
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "get":
                    {
                        TaskInfo info = service.Get(parsed.Require("name"));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(info) });
                        return 0;
                    }
                    case "list":
                    {
                        var tasks = service.List(parsed.Get("filter"));
                        OutputWriter.Success(new { success = true, command, tasks = tasks.Select(TaskDto.From).ToList() });
                        return 0;
                    }
                    case "enable":
                    case "disable":
                    {
                        string name = parsed.Require("name");
                        service.SetEnabled(name, parsed.Command == "enable");
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "run":
                    {
                        string name = parsed.Require("name");
                        service.Run(name);
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "history":
                    {
                        string name = parsed.Require("name");
                        int? last = null;
                        string lastStr = parsed.Get("last");
                        if (lastStr != null)
                        {
                            int n;
                            if (!int.TryParse(lastStr, out n) || n <= 0)
                                throw new TaskServiceException(ErrorCode.InvalidArguments, "last 格式错误 '" + lastStr + "'");
                            last = n;
                        }
                        var events = service.GetHistory(name, last, parsed.Has("errors-only"));
                        OutputWriter.Success(new
                        {
                            success = true,
                            command,
                            name,
                            events = events.Select(e => new
                            {
                                time = e.Time,
                                type = e.Type,
                                resultCode = e.ResultCode,
                                message = e.Message
                            }).ToList()
                        });
                        return 0;
                    }
                    default:
                        throw new TaskServiceException(ErrorCode.InvalidArguments, CliParser.Usage());
                }
            }
            catch (TaskServiceException ex)
            {
                OutputWriter.Error(command, ex);
                return ex.ExitCode;
            }
            catch (Exception ex)
            {
                OutputWriter.Error(command, new TaskServiceException(ErrorCode.InternalError, ex.Message, ex));
                return 1;
            }
        }
    }
}
```

- [ ] **Step 5: 改造 App.xaml.cs 为双模式入口**

`src/ScheduleCenter/App.xaml.cs` 完整替换为：

```csharp
using System;
using System.Linq;
using System.Windows;

namespace ScheduleCenter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            if (args.Length > 0)
            {
                Cli.ConsoleHelper.EnsureConsole();
                int code = Cli.CliRunner.Run(args);
                Console.Out.Flush();
                Console.Error.Flush();
                Environment.Exit(code);
                return;
            }

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);
            new MainWindow().Show();
        }

        private void App_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Gui.ErrorLogger.Log(e.Exception);
            MessageBox.Show("发生未预期的错误，详情已写入日志文件。\n" + e.Exception.Message,
                "ScheduleCenter", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
```

- [ ] **Step 6: 补齐编译桩（MainWindow / ErrorLogger，Task 10+ 完善）**

`src/ScheduleCenter/MainWindow.xaml`:

```xml
<Window x:Class="ScheduleCenter.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ScheduleCenter" Width="980" Height="640">
</Window>
```

`src/ScheduleCenter/MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace ScheduleCenter
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
```

`src/ScheduleCenter/Gui/ErrorLogger.cs`:

```csharp
using System;
using System.IO;

namespace ScheduleCenter.Gui
{
    public static class ErrorLogger
    {
        public static void Log(Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScheduleCenter");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
```

- [ ] **Step 7: 构建并以管理员身份做 CLI 冒烟验证**

```bash
dotnet build
```

然后在**管理员** cmd 中：

```bat
cd /d f:\Projects\scheduleCenter\src\ScheduleCenter\bin\Debug\net48
ScheduleCenter.exe add --name SmokeTest --path C:\Windows\System32\cmd.exe --args "/c exit 0" --trigger daily --time 09:00
echo %ERRORLEVEL%
ScheduleCenter.exe get --name SmokeTest
ScheduleCenter.exe list --filter Smoke*
ScheduleCenter.exe disable --name SmokeTest
ScheduleCenter.exe enable --name SmokeTest
ScheduleCenter.exe run --name SmokeTest
ScheduleCenter.exe history --name SmokeTest --last 5
ScheduleCenter.exe delete --name SmokeTest
echo %ERRORLEVEL%
ScheduleCenter.exe delete --name SmokeTest --force
echo %ERRORLEVEL%
ScheduleCenter.exe get --name SmokeTest
echo %ERRORLEVEL%
```

Expected:
- add 输出 `{"success":true,"command":"add","task":{...}}`，ERRORLEVEL=0
- 第一次 delete（无 --force）stderr 输出 `CONFIRM_REQUIRED`，ERRORLEVEL=2
- 第二次 delete ERRORLEVEL=0
- 最后 get ERRORLEVEL=4（TASK_NOT_FOUND）
- history 可能返回 HISTORY_DISABLED（系统未启用历史日志时），属预期

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(cli): dual-mode entry, command runner, json output"
```

---

### Task 10: GUI 基础设施与主窗口骨架

**Files:**
- Create: `src/ScheduleCenter/Gui/ViewModelBase.cs`、`src/ScheduleCenter/Gui/RelayCommand.cs`、`src/ScheduleCenter/Gui/FolderNode.cs`、`src/ScheduleCenter/Gui/TaskRowViewModel.cs`、`src/ScheduleCenter/Gui/HistoryRowViewModel.cs`、`src/ScheduleCenter/Gui/MainViewModel.cs`
- Modify: `src/ScheduleCenter/MainWindow.xaml`、`src/ScheduleCenter/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `ViewModelBase`、`RelayCommand`、`FolderNode{Name, FullPath, Children}`、`TaskRowViewModel(TaskInfo)`、`HistoryRowViewModel(HistoryEvent)`、`MainViewModel`（属性 `Tasks/Folders/SelectedTask/SelectedFolder/SearchText/StatusText/HistoryEvents/HistoryFilter/HistoryTitle`；命令 `NewCommand/RefreshCommand/EditCommand/DeleteCommand/ToggleEnabledCommand/RunCommand/CopyCliCommand/RefreshHistoryCommand`；internal 方法占位 `Refresh/ApplyFilter/LoadHistory/NewTask/EditTask/DeleteTask/ToggleEnabled/RunTask/CopyCli`，Task 11-14 填充）

- [ ] **Step 1: ViewModelBase.cs**

`src/ScheduleCenter/Gui/ViewModelBase.cs`:

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScheduleCenter.Gui
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

- [ ] **Step 2: RelayCommand.cs**

`src/ScheduleCenter/Gui/RelayCommand.cs`:

```csharp
using System;
using System.Windows.Input;

namespace ScheduleCenter.Gui
{
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
```

- [ ] **Step 3: FolderNode.cs / TaskRowViewModel.cs / HistoryRowViewModel.cs**

`src/ScheduleCenter/Gui/FolderNode.cs`:

```csharp
using System.Collections.ObjectModel;

namespace ScheduleCenter.Gui
{
    public sealed class FolderNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }   // "" 表示根，"MyApp" 表示子文件夹
        public ObservableCollection<FolderNode> Children { get; private set; }

        public FolderNode()
        {
            Children = new ObservableCollection<FolderNode>();
        }
    }
}
```

`src/ScheduleCenter/Gui/TaskRowViewModel.cs`:

```csharp
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
```

`src/ScheduleCenter/Gui/HistoryRowViewModel.cs`:

```csharp
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
```

- [ ] **Step 4: MainViewModel.cs 骨架**

`src/ScheduleCenter/Gui/MainViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class MainViewModel : ViewModelBase
    {
        internal readonly ScheduledTaskService Service = new ScheduledTaskService();
        private List<TaskInfo> _allTasks = new List<TaskInfo>();

        public ObservableCollection<FolderNode> Folders { get; private set; }
        public ObservableCollection<TaskRowViewModel> Tasks { get; private set; }
        public ObservableCollection<HistoryRowViewModel> HistoryEvents { get; private set; }

        private FolderNode _selectedFolder;
        public FolderNode SelectedFolder
        {
            get { return _selectedFolder; }
            set { if (Set(ref _selectedFolder, value)) ApplyFilter(); }
        }

        private TaskRowViewModel _selectedTask;
        public TaskRowViewModel SelectedTask
        {
            get { return _selectedTask; }
            set { if (Set(ref _selectedTask, value)) LoadHistory(); }
        }

        private string _searchText = "";
        public string SearchText
        {
            get { return _searchText; }
            set { if (Set(ref _searchText, value)) ApplyFilter(); }
        }

        private string _statusText = "就绪";
        public string StatusText
        {
            get { return _statusText; }
            set { Set(ref _statusText, value); }
        }

        private string _historyTitle = "运行历史";
        public string HistoryTitle
        {
            get { return _historyTitle; }
            set { Set(ref _historyTitle, value); }
        }

        private string _historyFilter = "全部";
        public string HistoryFilter
        {
            get { return _historyFilter; }
            set { if (Set(ref _historyFilter, value)) LoadHistory(); }
        }

        public ICommand NewCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ToggleEnabledCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand CopyCliCommand { get; private set; }
        public ICommand RefreshHistoryCommand { get; private set; }

        public MainViewModel()
        {
            Folders = new ObservableCollection<FolderNode>();
            Tasks = new ObservableCollection<TaskRowViewModel>();
            HistoryEvents = new ObservableCollection<HistoryRowViewModel>();

            NewCommand = new RelayCommand(NewTask);
            RefreshCommand = new RelayCommand(Refresh);
            EditCommand = new RelayCommand(EditTask, () => SelectedTask != null);
            DeleteCommand = new RelayCommand(DeleteTask, () => SelectedTask != null);
            ToggleEnabledCommand = new RelayCommand(ToggleEnabled, () => SelectedTask != null);
            RunCommand = new RelayCommand(RunTask, () => SelectedTask != null);
            CopyCliCommand = new RelayCommand(CopyCli, () => SelectedTask != null);
            RefreshHistoryCommand = new RelayCommand(LoadHistory, () => SelectedTask != null);
        }

        internal void Refresh() { /* Task 11 */ }
        internal void ApplyFilter() { /* Task 11 */ }
        internal void NewTask() { /* Task 12 */ }
        internal void EditTask() { /* Task 13 */ }
        internal void DeleteTask() { /* Task 13 */ }
        internal void ToggleEnabled() { /* Task 13 */ }
        internal void RunTask() { /* Task 13 */ }
        internal void CopyCli() { /* Task 13 */ }
        internal void LoadHistory() { /* Task 14 */ }

        internal static void ShowError(TaskServiceException ex)
        {
            System.Windows.MessageBox.Show(ex.CodeName + ": " + ex.Message, "操作失败",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
```

- [ ] **Step 5: MainWindow.xaml 完整布局**

`src/ScheduleCenter/MainWindow.xaml`:

```xml
<Window x:Class="ScheduleCenter.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ScheduleCenter 任务计划管理" Width="980" Height="640"
        WindowStartupLocation="CenterScreen">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <ToolBarTray Grid.Row="0">
            <ToolBar>
                <Button Content="新建任务" Command="{Binding NewCommand}" Padding="8,2"/>
                <Button Content="刷新" Command="{Binding RefreshCommand}" Padding="8,2"/>
                <Separator/>
                <TextBlock Text=" 搜索: " VerticalAlignment="Center"/>
                <TextBox Width="180" Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"/>
            </ToolBar>
        </ToolBarTray>

        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="200"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <TreeView Grid.Column="0" Margin="4" ItemsSource="{Binding Folders}"
                      SelectedItemChanged="FolderTree_SelectedItemChanged">
                <TreeView.ItemTemplate>
                    <HierarchicalDataTemplate ItemsSource="{Binding Children}">
                        <TextBlock Text="{Binding Name}"/>
                    </HierarchicalDataTemplate>
                </TreeView.ItemTemplate>
            </TreeView>

            <Grid Grid.Column="1" Margin="4">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="200"/>
                </Grid.RowDefinitions>

                <DataGrid Grid.Row="0" AutoGenerateColumns="False" IsReadOnly="True"
                          SelectionMode="Single" ItemsSource="{Binding Tasks}"
                          SelectedItem="{Binding SelectedTask}"
                          MouseDoubleClick="TaskGrid_MouseDoubleClick">
                    <DataGrid.Columns>
                        <DataGridTextColumn Header="名称" Binding="{Binding Name}" Width="2*"/>
                        <DataGridTextColumn Header="状态" Binding="{Binding State}" Width="0.8*"/>
                        <DataGridTextColumn Header="下次运行" Binding="{Binding NextRunTimeText}" Width="1.2*"/>
                        <DataGridTextColumn Header="上次运行" Binding="{Binding LastRunTimeText}" Width="1.2*"/>
                        <DataGridTextColumn Header="结果" Binding="{Binding LastResultText}" Width="0.8*"/>
                    </DataGrid.Columns>
                    <DataGrid.ContextMenu>
                        <ContextMenu>
                            <MenuItem Header="编辑" Command="{Binding DataContext.EditCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
                            <MenuItem Header="删除" Command="{Binding DataContext.DeleteCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
                            <MenuItem Header="启用/禁用" Command="{Binding DataContext.ToggleEnabledCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
                            <MenuItem Header="立即运行" Command="{Binding DataContext.RunCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
                            <MenuItem Header="复制 CLI 命令" Command="{Binding DataContext.CopyCliCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"/>
                        </ContextMenu>
                    </DataGrid.ContextMenu>
                </DataGrid>

                <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,4">
                    <Button Content="编辑" Command="{Binding EditCommand}" Padding="8,2" Margin="0,0,4,0"/>
                    <Button Content="删除" Command="{Binding DeleteCommand}" Padding="8,2" Margin="0,0,4,0"/>
                    <Button Content="启用/禁用" Command="{Binding ToggleEnabledCommand}" Padding="8,2" Margin="0,0,4,0"/>
                    <Button Content="立即运行" Command="{Binding RunCommand}" Padding="8,2" Margin="0,0,4,0"/>
                    <Button Content="复制 CLI 命令" Command="{Binding CopyCliCommand}" Padding="8,2"/>
                </StackPanel>

                <GroupBox Grid.Row="2" Header="{Binding HistoryTitle}" Margin="0,4,0,0">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="*"/>
                        </Grid.RowDefinitions>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                            <Button Content="刷新" Command="{Binding RefreshHistoryCommand}" Padding="8,2" Margin="0,0,8,0"/>
                            <TextBlock Text="筛选: " VerticalAlignment="Center"/>
                            <ComboBox Width="120" SelectedIndex="0" SelectionChanged="HistoryFilter_SelectionChanged">
                                <ComboBoxItem Content="全部"/>
                                <ComboBoxItem Content="仅错误"/>
                                <ComboBoxItem Content="仅完成"/>
                            </ComboBox>
                        </StackPanel>
                        <DataGrid Grid.Row="1" AutoGenerateColumns="False" IsReadOnly="True"
                                  ItemsSource="{Binding HistoryEvents}">
                            <DataGrid.Columns>
                                <DataGridTextColumn Header="时间" Binding="{Binding TimeText}" Width="160"/>
                                <DataGridTextColumn Header="事件类型" Binding="{Binding TypeText}" Width="120"/>
                                <DataGridTextColumn Header="结果码" Binding="{Binding ResultCodeText}" Width="80"/>
                                <DataGridTextColumn Header="消息" Binding="{Binding Message}" Width="*"/>
                            </DataGrid.Columns>
                            <DataGrid.RowStyle>
                                <Style TargetType="DataGridRow">
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding IsError}" Value="True">
                                            <Setter Property="Foreground" Value="Red"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </DataGrid.RowStyle>
                        </DataGrid>
                    </Grid>
                </GroupBox>
            </Grid>
        </Grid>

        <StatusBar Grid.Row="2">
            <StatusBarItem Content="{Binding StatusText}"/>
        </StatusBar>
    </Grid>
</Window>
```

- [ ] **Step 6: MainWindow.xaml.cs（完整替换）**

```csharp
using System.Windows;
using System.Windows.Controls;
using ScheduleCenter.Gui;

namespace ScheduleCenter
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += delegate { _vm.RefreshCommand.Execute(null); };
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _vm.SelectedFolder = e.NewValue as FolderNode;
        }

        private void TaskGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm.SelectedTask != null && _vm.EditCommand.CanExecute(null))
                _vm.EditCommand.Execute(null);
        }

        private void HistoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ComboBox).SelectedItem as ComboBoxItem;
            if (item != null)
                _vm.HistoryFilter = item.Content.ToString();
        }
    }
}
```

- [ ] **Step 7: 构建并手动验证**

```bash
dotnet build
```

Expected: 编译通过。运行 `src\ScheduleCenter\bin\Debug\net48\ScheduleCenter.exe`（会 UAC 提权），主窗口出现：工具栏、左侧空树、任务空表、操作按钮（除新建/刷新外灰）、历史区、状态栏"就绪"。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(gui): main window shell with mvvm infrastructure"
```

---

### Task 11: GUI 任务列表 / 搜索 / 文件夹树

**Files:**
- Modify: `src/ScheduleCenter/Gui/MainViewModel.cs`（填充 `Refresh`、`ApplyFilter`，新增私有 `BuildFolderTree`）

**Interfaces:**
- Consumes: Task 10 骨架；Core `List(null)`、`TaskInfo.Folder`（形如 `\ScheduleCenter\MyApp`）

- [ ] **Step 1: 填充 MainViewModel 数据逻辑**

替换 Task 10 的 `Refresh`/`ApplyFilter` 空实现，并新增 `BuildFolderTree` 私有方法：

```csharp
        internal void Refresh()
        {
            try
            {
                _allTasks = new List<TaskInfo>(Service.List(null));
                BuildFolderTree();
                ApplyFilter();
                StatusText = "共 " + _allTasks.Count + " 个任务";
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void ApplyFilter()
        {
            Tasks.Clear();
            string folderPath = SelectedFolder == null ? "" : SelectedFolder.FullPath;
            foreach (TaskInfo t in _allTasks)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    string expected = ScheduledTaskService.RootFolderPath + "\\" + folderPath;
                    if (!string.Equals(t.Folder, expected, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                if (!string.IsNullOrEmpty(SearchText) &&
                    t.RelativeName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Tasks.Add(new TaskRowViewModel(t));
            }
        }

        private void BuildFolderTree()
        {
            var root = new FolderNode { Name = "ScheduleCenter", FullPath = "" };
            var nodes = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            nodes[""] = root;

            foreach (TaskInfo t in _allTasks)
            {
                string folder = t.Folder;
                if (!folder.StartsWith(ScheduledTaskService.RootFolderPath)) continue;
                string rel = folder.Length > ScheduledTaskService.RootFolderPath.Length
                    ? folder.Substring(ScheduledTaskService.RootFolderPath.Length + 1)
                    : "";
                if (string.IsNullOrEmpty(rel) || nodes.ContainsKey(rel)) continue;

                string[] parts = rel.Split('\\');
                string path = "";
                FolderNode parent = root;
                foreach (string part in parts)
                {
                    path = path.Length == 0 ? part : path + "\\" + part;
                    FolderNode node;
                    if (!nodes.TryGetValue(path, out node))
                    {
                        node = new FolderNode { Name = part, FullPath = path };
                        nodes[path] = node;
                        parent.Children.Add(node);
                    }
                    parent = node;
                }
            }

            Folders.Clear();
            Folders.Add(root);
        }
```

- [ ] **Step 2: 手动验证**

1. 用 CLI 添加两个任务（管理员 cmd）：

```bat
ScheduleCenter.exe add --name GuiTestA --path C:\Windows\System32\cmd.exe --trigger daily --time 09:00
ScheduleCenter.exe add --name "Sub\GuiTestB" --path C:\Windows\System32\cmd.exe --trigger boot
```

2. 启动 GUI：左侧树显示 `ScheduleCenter` 及其下 `Sub`；根节点下列出 GuiTestA；搜索框输入 `GuiTest` 过滤生效；点击 `Sub` 节点只显示 GuiTestB。
3. 关闭 GUI。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(gui): task list, search filter, folder tree"
```

---

### Task 12: GUI 任务编辑器

**Files:**
- Create: `src/ScheduleCenter/EditorWindow.xaml`、`src/ScheduleCenter/EditorWindow.xaml.cs`、`src/ScheduleCenter/Gui/EditorViewModel.cs`
- Modify: `src/ScheduleCenter/Gui/MainViewModel.cs`（填充 `NewTask`）

**Interfaces:**
- Produces: `EditorWindow(ScheduledTaskService service, TaskInfo existing)`（existing 为 null 表示新建）；`EditorViewModel.RequestClose` 事件（`event Action<bool>`）—— Task 13 的 EditTask 复用。

- [ ] **Step 1: EditorViewModel.cs**

`src/ScheduleCenter/Gui/EditorViewModel.cs`:

```csharp
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
```

- [ ] **Step 2: EditorWindow.xaml**

`src/ScheduleCenter/EditorWindow.xaml`:

```xml
<Window x:Class="ScheduleCenter.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding Title}" Width="480" Height="520"
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
                    <CheckBox Content="启用" IsChecked="{Binding Enabled}" Margin="0,2"/>
                </StackPanel>
            </TabItem>
            <TabItem Header="触发器">
                <StackPanel Margin="8">
                    <TextBlock Text="触发器类型:"/>
                    <ComboBox ItemsSource="{Binding TriggerTypes}"
                              SelectedIndex="{Binding TriggerTypeIndex}" Margin="0,2,0,12"/>
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
                </StackPanel>
            </TabItem>
        </TabControl>

        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
            <Button Content="保存" Command="{Binding SaveCommand}" Width="80" Margin="0,0,8,0"/>
            <Button Content="取消" Command="{Binding CancelCommand}" Width="80"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: EditorWindow.xaml.cs**

`src/ScheduleCenter/EditorWindow.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Win32;
using ScheduleCenter.Core;
using ScheduleCenter.Gui;

namespace ScheduleCenter
{
    public partial class EditorWindow : Window
    {
        private readonly EditorViewModel _vm;

        public EditorWindow(ScheduledTaskService service, TaskInfo existing)
        {
            InitializeComponent();
            _vm = new EditorViewModel(service, existing);
            _vm.RequestClose += delegate(bool saved)
            {
                DialogResult = saved;
                Close();
            };
            DataContext = _vm;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) == true)
                _vm.Path = dlg.FileName;
        }
    }
}
```

- [ ] **Step 4: 填充 MainViewModel.NewTask**

替换 Task 10 的空实现：

```csharp
        internal void NewTask()
        {
            var editor = new EditorWindow(Service, null);
            editor.Owner = System.Windows.Application.Current.MainWindow;
            if (editor.ShowDialog() == true)
                Refresh();
        }
```

`MainViewModel.cs` 中 `EditTask` 等方法仍为 Task 13 的空实现，本任务不动。

- [ ] **Step 5: 构建并手动验证**

```bash
dotnet build
```

1. 启动 GUI → 点"新建任务"→ 常规页填名称 `GuiEditorTest`、程序路径 `C:\Windows\System32\cmd.exe`（或用浏览按钮）→ 触发器页选"每周"、时间 `09:30`、勾选 一/五 → 保存。
2. 主列表出现 `GuiEditorTest`。
3. 管理员 cmd 验证：`ScheduleCenter.exe get --name GuiEditorTest` 输出 trigger 为 weekly、`days:["MON","FRI"]`、`time:"09:30"`。
4. 再打开新建对话框：触发器切换到"每月"应显示"每月第几天"输入框；切换到"开机时"时间/日期控件隐藏；名称为空保存应弹出 `INVALID_ARGUMENTS` 错误对话框。
5. 清理：`ScheduleCenter.exe delete --name GuiEditorTest --force`。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(gui): task editor dialog (create mode)"
```

---

### Task 13: GUI 任务操作（编辑/删除/启停/运行/复制 CLI）

**Files:**
- Modify: `src/ScheduleCenter/Gui/MainViewModel.cs`（填充 `EditTask`、`DeleteTask`、`ToggleEnabled`、`RunTask`、`CopyCli`）

**Interfaces:**
- Consumes: Task 12 的 `EditorWindow(service, existing)`；Core `Delete(name, force)`、`SetEnabled`、`Run`、`Get`、`BuildAddCommand`

- [ ] **Step 1: 填充 MainViewModel 操作方法**

替换 Task 10 的五个空实现：

```csharp
        internal void EditTask()
        {
            if (SelectedTask == null) return;
            try
            {
                TaskInfo current = Service.Get(SelectedTask.Info.RelativeName);
                var editor = new EditorWindow(Service, current);
                editor.Owner = System.Windows.Application.Current.MainWindow;
                if (editor.ShowDialog() == true)
                    Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void DeleteTask()
        {
            if (SelectedTask == null) return;
            string name = SelectedTask.Info.RelativeName;
            System.Windows.MessageBoxResult confirm = System.Windows.MessageBox.Show(
                "确定要删除任务 '" + name + "' 吗？", "确认删除",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;
            try
            {
                Service.Delete(name, true);
                Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void ToggleEnabled()
        {
            if (SelectedTask == null) return;
            try
            {
                Service.SetEnabled(SelectedTask.Info.RelativeName, !SelectedTask.Info.Enabled);
                Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void RunTask()
        {
            if (SelectedTask == null) return;
            try
            {
                Service.Run(SelectedTask.Info.RelativeName);
                StatusText = "已触发运行: " + SelectedTask.Info.RelativeName;
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void CopyCli()
        {
            if (SelectedTask == null) return;
            try
            {
                TaskInfo current = Service.Get(SelectedTask.Info.RelativeName);
                System.Windows.Clipboard.SetText(Service.BuildAddCommand(current));
                StatusText = "CLI 命令已复制到剪贴板";
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }
```

- [ ] **Step 2: 构建并手动验证**

```bash
dotnet build
```

1. CLI 添加任务：`ScheduleCenter.exe add --name GuiOpsTest --path C:\Windows\System32\cmd.exe --trigger daily --time 09:00`
2. GUI 刷新 → 双击 `GuiOpsTest` → 编辑器打开且字段已填充（名称为灰色不可改）→ 改时间为 `10:00` 保存 → `ScheduleCenter.exe get --name GuiOpsTest` 确认 time 为 `10:00`。
3. 选中任务 → "启用/禁用" → 刷新后状态列显示"已禁用"；再点一次恢复。
4. 选中任务 → "立即运行" → 状态栏提示已触发。
5. 选中任务 → "复制 CLI 命令" → 粘贴到记事本，应为完整 `ScheduleCenter add --name "GuiOpsTest" ... --trigger daily --time 10:00`。
6. 选中任务 → "删除" → 弹出确认框 → 选"否"任务仍在；再删选"是" → 任务消失。右键菜单各项与按钮行为一致。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(gui): task actions (edit/delete/toggle/run/copy cli)"
```

---

### Task 14: GUI 历史页签与最终验证

**Files:**
- Modify: `src/ScheduleCenter/Gui/MainViewModel.cs`（填充 `LoadHistory`）

**Interfaces:**
- Consumes: Core `GetHistory(name, last, errorsOnly)`、`HistoryService.IsLogEnabled()`；Task 10 的 `HistoryRowViewModel`

- [ ] **Step 1: 填充 LoadHistory**

替换 Task 10 的空实现：

```csharp
        internal void LoadHistory()
        {
            HistoryEvents.Clear();
            if (SelectedTask == null)
            {
                HistoryTitle = "运行历史";
                return;
            }

            string name = SelectedTask.Info.RelativeName;
            HistoryTitle = "运行历史 - " + name;
            try
            {
                bool errorsOnly = HistoryFilter == "仅错误";
                var events = Service.GetHistory(name, 50, errorsOnly);
                foreach (HistoryEvent ev in events)
                {
                    var row = new HistoryRowViewModel(ev);
                    if (HistoryFilter == "仅完成" && !row.IsCompleted) continue;
                    HistoryEvents.Add(row);
                }
            }
            catch (TaskServiceException ex)
            {
                if (ex.Code == ErrorCode.HistoryDisabled)
                {
                    System.Windows.MessageBox.Show(
                        "系统未启用任务历史记录。\n\n请在 Windows\"任务计划程序\"右侧操作栏点击\"启用所有任务历史记录\"，然后回到本程序刷新。",
                        "历史记录不可用", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    ShowError(ex);
                }
            }
        }
```

- [ ] **Step 2: 构建并手动验证（完整验收清单）**

```bash
dotnet build
```

**历史功能：**
1. 确保系统已启用"所有任务历史记录"（任务计划程序右侧操作栏）。
2. CLI 添加并运行：`ScheduleCenter.exe add --name GuiHistTest --path C:\Windows\System32\cmd.exe --args "/c exit 0" --trigger daily --time 09:00`，再 `run` 两次。
3. GUI 选中 `GuiHistTest` → 历史区标题变为"运行历史 - GuiHistTest"，出现事件行；"仅完成"筛选只剩 completed/actionCompleted；"仅错误"为空（本次无失败）。
4. 构造失败：`ScheduleCenter.exe update --name GuiHistTest --args "/c exit 1"`，再 run，等几秒刷新历史 → 失败行红色显示。
5. 清理：删除 GuiHistTest（--force）。

**全部自动化测试（管理员终端）：**

```bash
dotnet test
```

Expected: 除因系统未启用历史日志导致 Inconclusive 的用例外全部 Passed。

**CLI 契约复核（管理员 cmd）：**

```bat
ScheduleCenter.exe add --name Final --path C:\Windows\System32\cmd.exe --trigger weekly --time 08:00 --days MON --highest
ScheduleCenter.exe list
ScheduleCenter.exe update --name Final --time 08:30 --trigger daily
ScheduleCenter.exe delete --name Final --force
```

**GUI 冒烟：** 启动 → 新建/编辑/删除/启停/运行/复制 CLI/搜索/文件夹切换/历史查看各一遍；`taskschd.msc` 中确认所有任务都在 `ScheduleCenter` 文件夹下，系统任务未出现在本工具中。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(gui): history view with filter and disabled-log guidance"
```

---

## 自审记录

- **规格覆盖：** §2 文件夹隔离 → Task 4/5（含隔离测试）；§3 双模式/AttachConsole → Task 9；§4 CLI 全部子命令与 JSON → Task 8/9；§5 GUI 列表/编辑器/历史/复制 CLI → Task 10-14；§6 错误模型/退出码/校验 → Task 2/3/9；§7 测试 → Task 2-8 + Task 14 清单。V1 不做项未包含，符合预期。
- **类型一致性：** `TriggerSpec/TaskSpec/TaskUpdate/TaskInfo/HistoryEvent` 字段名、`CliParser.Parse → ParsedArgs`、`SpecBuilder.BuildSpec/BuildUpdate/BuildTrigger/ParseDay`、`CliRunner.Run`、`MainViewModel` 命令名在前后任务间一致。
- **C# 7.3 合规：** 全部代码使用经典 using/switch/委托，无 C# 8+ 语法。











