# ScheduleCenter V2 设计文档

日期：2026-07-24
状态：草案
基线：`2026-07-23-schedule-center-design.md`（V1，已实现）

## 1. 概述与范围

V2 在 V1 基础上新增两块功能，对应 V1 spec §5 "V1 不做"的前两项：

1. **高级触发器与条件** —— 多触发器组合、空闲触发、事件触发、执行时间限制、电源条件
2. **任务 XML 导入导出** —— 单任务导出/导入标准 Windows 任务定义 XML

### V2 不做（延后到 V3）

- GUI 自动化测试（V1 spec "V1 不做" 第 3 项）
- 网络条件、失败重试、多实例策略
- 间隔/重复触发（RepetitionPattern）
- 批量 XML 导入导出（文件夹级 / 全树级）
- 系统托盘、批量操作、多语言、通知等其他增强

### V2 对 V1 的 Breaking Change

- 触发器字段由单数改为复数：`TaskSpec.Trigger` → `TaskSpec.Triggers`，`TaskUpdate` / `TaskInfo` 同步
- CLI JSON 输出：`task.trigger`（单对象）→ `task.triggers`（数组）
- **CLI 输入语法保留 V1 兼容**：单触发器场景的 `--trigger/--time/--days/--day-of-month` 用法不变；多触发器场景新增 `--triggers-json` / `--triggers-file`

详见 §2。

## 2. Breaking Change 与迁移指引

### 2.1 数据模型变更

| 类型 | V1 字段 | V2 字段 | 说明 |
|---|---|---|---|
| `TaskSpec` | `TriggerSpec Trigger` | `IList<TriggerSpec> Triggers` | 必须至少 1 个元素 |
| `TaskUpdate` | `TriggerSpec Trigger` | `IList<TriggerSpec> Triggers` | null = 不修改；非 null 则整体替换 |
| `TaskInfo` | `TriggerSpec Trigger` | `IList<TriggerSpec> Triggers` | 回读时按 `TaskDefinition.Triggers` 顺序填充 |

`TriggerSpec` 自身扩展新字段（见 §3.1），不删除已有字段。

### 2.2 CLI JSON 输出变更

V1（仅示意）：
```json
{"success":true,"command":"add","task":{"name":"Backup","trigger":{"type":"weekly","time":"09:00","days":["MON","FRI"]}, ...}}
```

V2：
```json
{"success":true,"command":"add","task":{"name":"Backup","triggers":[{"kind":"weekly","time":"09:00","days":["MON","FRI"]}], ...}}
```

- `task.trigger` 字段移除，调用方需改读 `task.triggers[0]` 或遍历数组
- 单触发器任务输出 1 元素数组

### 2.3 CLI 输入语法（保留兼容）

V1 的单触发器语法在 V2 中继续可用：

```
ScheduleCenter add --name Backup --path C:\app.exe --trigger weekly --time 09:00 --days MON,FRI
```

解析后转换为 1 元素 `Triggers` 数组传入 Core。V1 调用脚本无需修改即可工作。

新增多触发器语法（与单触发器语法**互斥**，混用报 `INVALID_ARGUMENTS`）：

```
ScheduleCenter add --name Backup --path C:\app.exe \
  --triggers-json '[{"kind":"daily","time":"09:00"},{"kind":"weekly","time":"10:00","days":["MON"]}]'

ScheduleCenter add --name Backup --path C:\app.exe --triggers-file C:\triggers.json
```

`--triggers-json` 与 `--triggers-file` 同时出现时以 `--triggers-file` 为准并报错？—— 不，二者也互斥，混用报 `INVALID_ARGUMENTS`。

### 2.4 V1 任务在 V2 中的读取

V1 创建的单触发器任务在 V2 中通过 `get` / `list` 读取时，`TaskInfo.Triggers` 自动为 1 元素数组。底层 `TaskDefinition.Triggers` 本就是集合，V1 只读了 `Triggers[0]`，V2 读取全部。

### 2.5 调用方迁移清单

- C# 调用方：`spec.Trigger` → `spec.Triggers[0]` 或 `spec.Triggers = new List<TriggerSpec> { ... }`
- JSON 解析方：`task.trigger` → `task.triggers[0]` 或遍历 `task.triggers`
- CLI 脚本：无需改动（单触发器语法保留）

## 3. 高级触发器与条件

### 3.1 TriggerSpec 扩展

```csharp
public enum TriggerKind { Once, Daily, Weekly, Monthly, Boot, Logon, Idle, Event }
```

`TriggerSpec` 新增字段（不删除已有字段）：

```csharp
public sealed class TriggerSpec
{
    public TriggerKind Kind { get; set; }
    public TimeSpan? Time { get; set; }          // once/daily/weekly/monthly
    public DateTime? Date { get; set; }          // once
    public DayOfWeek[] Days { get; set; }        // weekly
    public int? DayOfMonth { get; set; }         // monthly

    // V2 新增 —— Idle 触发器
    public IdleSettingsSpec IdleSettings { get; set; }

    // V2 新增 —— Event 触发器
    public string EventSubscription { get; set; }  // 完整 XPath，优先级高于简化参数
    public string EventLog { get; set; }           // 简化参数：日志名
    public string EventSource { get; set; }        // 简化参数：源（可选）
    public int? EventId { get; set; }              // 简化参数：事件 ID（可选）
}

public sealed class IdleSettingsSpec
{
    public TimeSpan? WaitTimeout { get; set; }     // 等待进入空闲的最长时间
    public TimeSpan? Duration { get; set; }        // 空闲持续时间
    public bool StopOnIdleEnd { get; set; }        // 空闲结束时停止任务
    public bool RestartOnIdle { get; set; }        // 空闲中断后重新计时
}
```

### 3.2 触发器参数规则（扩展 V1 spec §4 表格）

| trigger | 必需参数 | 可选参数 |
|---|---|---|
| `once` | `--date` `--time` | — |
| `daily` | `--time` | — |
| `weekly` | `--time` `--days` | — |
| `monthly` | `--time` `--day-of-month` | — |
| `boot` | — | — |
| `logon` | — | — |
| `idle` | — | `--idle-wait` `--idle-duration` `--idle-stop-on-end` `--idle-restart` |
| `event` | `--event-log` 或 `--event-subscription`（二选一） | `--event-source` `--event-id`（仅与 `--event-log` 搭配） |

**事件触发器两种语法：**

- 简化语法：`--event-log <日志名> [--event-source <源>] [--event-id <ID>]`，内部生成标准 XPath 订阅：
  ```xml
  <QueryList><Query Id="0" Path="{log}">
    <Select Path="{log}">*[System[Provider[@Name='{source}'] and EventID={id}]]</Select>
  </Query></QueryList>
  ```
  `--event-source` 与 `--event-id` 省略时对应条件不出现。
- 完整语法：`--event-subscription "<XPath 字符串>"`，直接传给 `EventTrigger.Subscription`，允许调用方写任意复杂查询。

两种语法互斥；`--event-subscription` 出现时忽略 `--event-log/source/id` 并报警告？—— 不，互斥，混用报 `INVALID_ARGUMENTS`。

**空闲触发器：**

`IdleTrigger` 自身无参数，空闲行为由 `TaskDefinition.Settings.IdleSettings` 控制。`--idle-*` 参数作用于 `IdleSettings`，仅在 `--trigger idle` 时有意义；用在其他触发器类型上报 `INVALID_ARGUMENTS`。

默认值（V2 不显式指定时）：
- `WaitTimeout` = 1 小时
- `Duration` = 10 分钟
- `StopOnIdleEnd` = true
- `RestartOnIdle` = false

### 3.3 多触发器组合

- `TaskSpec.Triggers` 至少 1 个元素，否则 `INVALID_ARGUMENTS`
- 每个元素独立校验（`TaskValidator.ValidateTrigger` 逐个调用）
- 多触发器为 **OR 关系**：任一触发器满足即启动任务
- `update` 命令提供 `Triggers` 时整体替换（与 V1 `Trigger` 语义一致），不支持增量增删单个触发器
- `CliCommandBuilder.BuildAddCommand` 输出策略：
  - 1 个触发器时输出 V1 风格单触发器语法（`--trigger daily --time 09:00`）
  - 多个触发器时输出 `--triggers-json '<JSON 数组>'` 内联

### 3.4 高级条件/设置

`TaskSpec` / `TaskUpdate` / `TaskInfo` 新增字段（平铺，与 V1 风格一致，不引入 `TaskSettings` 子类）：

| 字段 | 类型 | TaskSpec | TaskUpdate | TaskInfo | CLI 参数 | 默认值 |
|---|---|---|---|---|---|---|
| `ExecutionTimeLimit` | `TimeSpan?` | ✓ | `TimeSpan?`（null=不改） | ✓ | `--execution-time-limit <分钟>`（0 或省略 = 无限制） | 无限制 |
| `DisallowStartIfOnBatteries` | `bool` | ✓ | `bool?` | ✓ | `--no-start-on-battery` 标志 | false |
| `StopIfGoingOnBatteries` | `bool` | ✓ | `bool?` | ✓ | `--stop-on-battery` 标志 | true |

映射到 `TaskDefinition.Settings`：
- `Settings.ExecutionTimeLimit = TimeSpan.Zero` 表示无限制（TaskScheduler 库约定）
- `Settings.DisallowStartIfOnBatteries`
- `Settings.StopIfGoingOnBatteries`

`TaskInfo` 回读时按 `TaskDefinition.Settings` 实际值填充。

### 3.5 CLI 参数清单（V2 全量）

新增 / 变更的 CLI 选项（在 V1 基础上）：

**多触发器（互斥组）：**
- `--triggers-json <JSON 字符串>` —— 多触发器 JSON 数组
- `--triggers-file <文件路径>` —— 从文件读 JSON 数组

**新触发器类型参数：**
- `--idle-wait <分钟>` —— 空闲等待超时
- `--idle-duration <分钟>` —— 空闲持续时间
- `--idle-stop-on-end` —— 标志，空闲结束停止
- `--idle-restart` —— 标志，空闲中断重新计时
- `--event-log <日志名>` —— 事件日志名
- `--event-source <源>` —— 事件源
- `--event-id <ID>` —— 事件 ID（整数）
- `--event-subscription <XPath>` —— 完整事件订阅 XPath

**高级条件：**
- `--execution-time-limit <分钟>` —— 最长运行时间，0 = 无限制
- `--no-start-on-battery` —— 标志，电池供电时不启动
- `--stop-on-battery` —— 标志，切到电池时停止

**互斥规则（违反报 `INVALID_ARGUMENTS`）：**
- `--trigger` 与 `--triggers-json` / `--triggers-file` 互斥
- `--triggers-json` 与 `--triggers-file` 互斥
- `--event-log` 与 `--event-subscription` 互斥
- `--event-source` / `--event-id` 仅可与 `--event-log` 搭配，单独出现或与 `--event-subscription` 搭配报错
- `--idle-*` 仅可与 `--trigger idle` 搭配

### 3.6 JSON 契约扩展

`task.triggers` 数组元素结构：

```json
{
  "kind": "weekly|daily|once|monthly|boot|logon|idle|event",
  "time": "09:00",
  "date": "2026-08-01",
  "days": ["MON", "FRI"],
  "dayOfMonth": 15,
  "idleSettings": {
    "waitTimeout": "01:00:00",
    "duration": "00:10:00",
    "stopOnIdleEnd": true,
    "restartOnIdle": false
  },
  "eventSubscription": "<XPath XML 字符串>",
  "eventLog": "System",
  "eventSource": "Microsoft-Windows-...",
  "eventId": 1234
}
```

仅出现该 `kind` 用得到的字段；其余字段省略（不输出 null）。

`task` 对象新增字段：

```json
{
  "executionTimeLimit": "00:30:00",
  "disallowStartIfOnBatteries": false,
  "stopIfGoingOnBatteries": true
}
```

`executionTimeLimit` 为 `null` 或省略表示无限制。`TaskInfo` 回读时 `TimeSpan.Zero`（库中表示无限制）映射为输出 `null`。

### 3.7 GUI 编辑器扩展

#### 触发器页改造

V1 的"类型下拉 + 平铺字段"改为"触发器列表 + 编辑面板"：

```
┌─ 触发器页 ──────────────────────────────────────┐
│ [+ 新增触发器]   [删除选中]                       │
│ ┌──────────────┐ ┌──────────────────────────┐  │
│ │ 触发器列表    │ │ 编辑面板（随类型切换）     │  │
│ │ 1. 每日 09:00 │ │  类型: [每日 ▼]            │  │
│ │ 2. 每周一 10:0│ │  时间: [09:00]            │  │
│ │ 3. 空闲触发   │ │  ...                      │  │
│ │              │ │                            │  │
│ └──────────────┘ └──────────────────────────┘  │
└────────────────────────────────────────────────┘
```

- 列表项显示触发器摘要（如"每日 09:00"、"每周一 10:00"、"空闲"、"事件: System/100"）
- 选中后右侧面板显示该触发器编辑控件，类型切换时控件区动态切换（沿用 V1 类型控件 + 新增 idle/event 控件）
- 新增触发器默认类型 `daily`，时间 `09:00`
- 至少保留 1 个触发器，最后一个不允许删除（按钮禁用）；空列表保存时报 `INVALID_ARGUMENTS`

新增触发器类型的编辑面板：
- **idle**：空闲等待（分钟数字框）、空闲持续时间（分钟数字框）、空闲结束停止（复选框）、空闲中断重启（复选框）
- **event**：单选"简化参数 / 完整 XPath"
  - 简化参数：日志名（文本框）、源（文本框，可选）、事件 ID（数字框，可选）
  - 完整 XPath：多行文本框

#### 常规页新增"高级条件"区

在 V1 安全选项下方新增：

```
┌─ 高级条件 ───────────────────────────────────┐
│ 执行时间限制: [____] [分钟 ▼]  (0 = 无限制)   │
│ □ 电池供电时不启动                              │
│ □ 切换到电池时停止任务                          │
└────────────────────────────────────────────┘
```

- 执行时间限制：数字框 + 单位下拉（分钟/小时），存储为 `TimeSpan`；值为 0 时表示无限制
- 两个电源复选框（默认不勾选，与 TaskScheduler 库默认一致；但 `StopIfGoingOnBatteries` 库默认 true，V2 GUI 默认勾选以匹配库默认）

#### 复制 CLI 命令行为

- 单触发器任务：输出 V1 风格单行命令（含 `--trigger ...`）
- 多触发器任务：输出 `--triggers-json '<JSON>'` 内联命令，JSON 紧凑格式（无多余空格）

## 4. 任务 XML 导入导出

### 4.1 XML 格式

使用 TaskScheduler 库的 `TaskDefinition.XmlText`（标准 Windows 任务计划 XML 格式，与 `taskschd.msc` 导出格式一致）。

不封装额外元数据层，保证：
- 本工具导出的 XML 可在 `taskschd.msc` 中直接导入
- `taskschd.msc` 导出的 XML 可在本工具中导入（前提是内容符合 §4.4 限制）

### 4.2 CLI 子命令

```
ScheduleCenter export --name <任务名> [--output <文件路径>]
ScheduleCenter import --file <XML 路径> [--name <新任务名>] [--force]
```

**export 行为：**
- 不指定 `--output`：XML 字符串输出到 stdout（不封装 JSON，原始 XML 文本，便于管道与重定向）
- 指定 `--output`：写入文件，stdout 输出标准 JSON `{"success":true,"command":"export","name":"...","path":"C:\\...\\file.xml"}`
- 导出内容：完整 `TaskDefinition.XmlText`（含触发器、操作、设置、Principal）

**import 行为：**
- `--file` 必需
- `--name` 可选；不指定时用 XML 内 `RegistrationInfo.URI` 的叶子名，若 URI 也缺失则用文件名（去扩展名）
- `--name` 可含子路径（与 `add` 一致），路径仍在 `\ScheduleCenter\` 下
- 同名任务已存在：
  - 未带 `--force`：报 `TASK_EXISTS`（退出码 5），不导入
  - 带 `--force`：走 `CreateOrUpdate` 语义覆盖
- 成功输出：`{"success":true,"command":"import","name":"...","task":{...TaskInfo JSON...}}`
- XML 解析失败：报 `XML_PARSE_ERROR`（退出码 2），message 含原始异常信息
- 文件不存在：报 `INVALID_PATH`（退出码 2）

### 4.3 GUI 入口

主窗口工具栏新增两个按钮：

```
工具栏: [新建任务] [导入] [导出] [刷新]  搜索框: [________]
```

- **导出**：选中任务 → 点击 → `SaveFileDialog`（默认文件名 `<任务叶子名>.xml`，过滤器 `任务计划 XML (*.xml)|*.xml`）→ 调 `service.Export(name, filePath)` → 成功提示
- **导入**：点击 → `OpenFileDialog`（过滤器同上）→ 弹出"导入到名称"对话框（文本框预填 XML 内 URI 或文件名，可改）→ 勾选"覆盖现有"复选框（等效 `--force`）→ 调 `service.Import(filePath, newName, force)` → 成功后刷新列表
- 未选中任务时"导出"按钮禁用

右键菜单同步新增"导出..."项；"导入"为全局操作仅在工具栏。

### 4.4 校验与限制

导入的 XML 必须可被 `TaskDefinition.XmlText` 反序列化。反序列化后做以下校验：

- **操作类型**：仅允许 `ExecAction`。含 `EmailAction` / `ComHandlerAction` / `ShowMessageAction` 等其他类型时报 `INVALID_ARGUMENTS`（退出码 2），message 指出第几个 Action 类型不支持
- **触发器类型**：仅允许映射到 `TriggerKind` 枚举的 8 种类型（once/daily/weekly/monthly/boot/logon/idle/event）。含 `RegistrationTrigger` / `SessionStateChangeTrigger` 等未支持类型时：
  - 该触发器被忽略，不影响其他触发器导入
  - 在 JSON 输出中增加 `warnings: ["已忽略 N 个不支持的触发器类型: ..."]` 字段提示
- **Principal**：
  - `UserId = SYSTEM` → `RunAsSystem = true`
  - 其他用户 → `RunAsSystem = false`，保留 `UserId`（但 V2 不暴露自定义用户名到 TaskSpec，按当前用户重新注册）
  - `RunLevel = Highest` → `Highest = true`
- **路径限制**：导入后的任务必须注册在 `\ScheduleCenter\` 下（由 `--name` 决定，与 V1 一致）。XML 内的 `Task.Path` 信息忽略

### 4.5 Core API

```csharp
public sealed class ScheduledTaskService
{
    // V2 新增
    public string Export(string name);                          // 返回 XML 字符串
    public void ExportToFile(string name, string filePath);     // 写文件
    public TaskInfo Import(string xml, string name, bool force);
    public TaskInfo ImportFromFile(string filePath, string name, bool force);
}
```

`Export` 内部：
```csharp
using (Task task = FindTaskOrThrow(ts, name))
    return task.Definition.XmlText;
```

`Import` 内部：
```csharp
TaskDefinition td = TaskService.Instance.NewTask();
td.XmlText = xml;  // 反序列化
// 校验 Actions / Triggers（按 §4.4）
// 应用 Principal 策略（按 §4.4 强制 ScheduleCenter 隔离规则）
// 注册到 \ScheduleCenter\<name> 文件夹下
```

## 5. 错误码扩展

在 V1 `ErrorCode` 枚举基础上新增：

| code | 退出码 | 触发场景 |
|---|---|---|
| `INVALID_TRIGGER_FORMAT` | 2 | `--triggers-json` JSON 解析失败、`--triggers-file` 文件读取失败或 JSON 解析失败 |
| `INVALID_EVENT_SUBSCRIPTION` | 2 | 事件触发器参数不足（既无 `--event-log` 也无 `--event-subscription`）或 XPath 格式非法 |
| `XML_PARSE_ERROR` | 2 | `import` 的 XML 无法被 `TaskDefinition.XmlText` 反序列化 |

其余错误码沿用 V1：`INVALID_ARGUMENTS`（互斥参数冲突、未知触发器类型等）、`TASK_EXISTS`、`TASK_NOT_FOUND`、`INVALID_PATH`（import 文件不存在）、`ACCESS_DENIED`、`INTERNAL_ERROR`。

## 6. 测试策略

### 6.1 Core 层（MSTest 集成测试，沿用 V1 `ServiceTestBase`）

**多触发器：**
- `Add_MultipleTriggers_CreatesTaskWithAllTriggers` —— 2-3 个触发器，`get` 回读 `Triggers` 数组长度与类型匹配
- `Update_ReplacesEntireTriggerList` —— 原 2 个触发器，update 传 1 个新触发器，回读为 1 元素数组
- `Update_NullTriggers_KeepsExisting` —— `TaskUpdate.Triggers = null`，触发器列表不变
- `Add_EmptyTriggers_ThrowsInvalidArguments`

**Idle 触发器：**
- `Add_IdleTrigger_AppliesIdleSettings` —— 指定 `--idle-wait 5 --idle-duration 10 --idle-stop-on-end`，回读 `IdleSettings` 字段匹配
- `Add_IdleTrigger_DefaultSettings` —— 不指定 idle 参数，回读默认值（WaitTimeout 1h、Duration 10min、StopOnIdleEnd true、RestartOnIdle false）

**Event 触发器：**
- `Add_EventTrigger_SimplifiedArgs_GeneratesXPath` —— `--event-log System --event-source X --event-id 100`，回读 `EventSubscription` 包含对应 XPath
- `Add_EventTrigger_FullSubscription` —— `--event-subscription "<XPath>"`，回读 `EventSubscription` 与传入一致
- `Add_EventTrigger_NoArgs_ThrowsInvalidEventSubscription`

**高级条件：**
- `Add_WithExecutionTimeLimit_StoredAndReadBack` —— 30 分钟，回读匹配
- `Add_WithPowerConditions_StoredAndReadBack`
- `Update_ChangesExecutionTimeLimit`

**XML 导入导出：**
- `Export_ReturnsValidXml` —— 导出后 `XDocument.Parse` 成功，XML 含触发器/操作节点
- `ExportToFile_WritesFile`
- `Import_V1ExportedXml_ProducesEquivalentTaskInfo` —— 先 `add` 创建任务，`export` 取 XML，删除任务，`import` 还原，`get` 比较 TaskInfo 关键字段
- `Import_UnknownTriggerType_WarningAndIgnored` —— 构造含 `RegistrationTrigger` 的 XML，导入成功但 warnings 非空，回读 `Triggers` 不含该类型
- `Import_UnsupportedActionType_ThrowsInvalidArguments` —— 含 `EmailAction` 报错
- `Import_DuplicateName_NoForce_ThrowsTaskExists`
- `Import_DuplicateName_WithForce_Overwrites`
- `Import_InvalidXml_ThrowsXmlParseError`
- `Import_NonExistentFile_ThrowsInvalidPath`

### 6.2 CLI 层（单元测试，不碰 COM）

- `Parse_SingleTriggerSyntax_ProducesOneElementTriggers` —— V1 语法回归
- `Parse_TriggersJson_ProducesMultipleTriggers`
- `Parse_TriggersFile_ReadsAndParses`
- `Parse_TriggerAndTriggersJson_MutuallyExclusive_Throws`
- `Parse_TriggersJsonAndTriggersFile_MutuallyExclusive_Throws`
- `Parse_EventLogAndEventSubscription_MutuallyExclusive_Throws`
- `Parse_EventSourceWithoutEventLog_Throws`
- `Parse_IdleArgsWithNonIdleTrigger_Throws`
- `BuildSpec_PropagatesExecutionTimeLimit`
- `BuildSpec_PropagatesPowerConditions`
- `CliCommandBuilder.SingleTrigger_OutputsV1Style`
- `CliCommandBuilder.MultiTrigger_OutputsTriggersJson`

### 6.3 GUI

不写自动化测试（延后 V3）。手动验证清单随实现计划提供。

## 7. 实现顺序提示

写实现计划时建议按以下顺序拆分任务，每步可独立验证：

1. **数据模型扩展** —— `TriggerSpec` 新字段、`TaskSpec/TaskUpdate/TaskInfo` 改 `Triggers` 数组、`IdleSettingsSpec` 类、`ErrorCode` 新增项
2. **校验扩展** —— `TaskValidator` 支持新触发器类型与高级条件
3. **服务层多触发器** —— `ScheduledTaskService.Add/Update/Get/List` 改用 `Triggers` 集合；`BuildTrigger` → `BuildTriggers`；`ReadTrigger` → `ReadTriggers`；`ToTaskInfo` 输出数组
4. **服务层新触发器** —— `IdleTrigger` + `IdleSettings` 映射；`EventTrigger` + Subscription 生成；高级条件 `Settings` 字段映射
5. **CLI 解析扩展** —— `CliParser` 支持 `--triggers-json` / `--triggers-file` / 新触发器参数 / 高级条件参数；互斥规则
6. **CLI 规格构建** —— `SpecBuilder.BuildSpec/BuildUpdate` 处理多触发器与新字段
7. **CLI 命令生成** —— `CliCommandBuilder` 多触发器场景输出 `--triggers-json`
8. **CLI runner** —— `export` / `import` 子命令分发
9. **Core XML 导入导出** —— `ScheduledTaskService.Export/Import` 实现，含 §4.4 校验
10. **GUI 编辑器改造** —— 触发器列表 + 编辑面板；高级条件区
11. **GUI 工具栏导入导出** —— 主窗口工具栏按钮 + 对话框
12. **测试补全与最终验证**

## 8. 自审待办

写实现计划前需确认：
- `--triggers-json` 与 `--triggers-file` 互斥策略是否合理（vs 二者并存以 file 为准）
- import 时未支持触发器类型的"忽略 + warning"策略是否合理（vs 直接报错）
- GUI 默认电源条件勾选状态是否匹配 TaskScheduler 库默认
- 高级条件字段平铺到 `TaskSpec` vs 引入 `TaskSettings` 子类 —— 本 spec 选平铺，与 V1 风格一致；若字段后续继续增多，V3 再重构
