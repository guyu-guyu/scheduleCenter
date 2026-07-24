# ScheduleCenter

ScheduleCenter 是一个 Windows 任务计划管理中间工具，既提供 WPF GUI 管理界面，也能作为 CLI 被其他程序脚本调用，用于添加、修改、删除、查询 Windows 任务计划。

- **单 exe 双模式**：无参数启动 → WPF GUI；带参数启动 → CLI（JSON 输出 + 退出码）
- **专用文件夹隔离**：所有任务都创建在任务计划程序的 `\ScheduleCenter\` 文件夹下，系统自带任务对工具完全不可见、不可操作
- **管理员权限**：manifest 声明 `requireAdministrator`，支持创建 SYSTEM 账户 / 最高权限任务
- **目标框架**：.NET Framework 4.8（Windows 10/11 自带运行时，免安装）
- **界面语言**：中文

## 功能特性

### V1 基础功能

- 任务的增删改查、启用/禁用、立即运行
- 6 种基础触发器：一次性、每日、每周、每月、开机时、登录时
- 运行历史记录查看（来源：`Microsoft-Windows-TaskScheduler/Operational` 事件日志）
- GUI 文件夹树 + 任务列表 + 搜索 + 历史页签（含错误红色高亮、筛选）
- "复制等效 CLI 命令"功能（GUI 操作 → 剪贴板 CLI 命令）
- 统一错误模型（`TaskServiceException` + `ErrorCode` 枚举）

### V2 高级功能

- **多触发器组合**：一个任务可挂多个触发器（V1 仅支持单触发器）
- **空闲触发器**（idle）：系统空闲时触发
- **事件触发器**（event）：基于 Windows 事件日志，支持简化参数（日志名/源/事件 ID）或完整 XPath 订阅
- **高级条件**：
  - 执行时间限制（`ExecutionTimeLimit`）
  - 电池供电时不启动（`DisallowStartIfOnBatteries`）
  - 切换到电池时停止任务（`StopIfGoingOnBatteries`）
- **任务 XML 导入导出**：基于 TaskScheduler 库原生 `TaskDefinition.XmlText`，单任务粒度

## 构建与运行

### 依赖

- .NET Framework 4.8 SDK（Windows 10/11 SDK 自带）
- Visual Studio 2019/2022 或 `dotnet` CLI
- NuGet 包：`TaskScheduler` 2.12.2（dahall）、`Newtonsoft.Json` 13.0.3

### 构建

```bash
dotnet build ScheduleCenter.sln -c Release
```

产物：`src/ScheduleCenter/bin/Release/net48/ScheduleCenter.exe`

### 运行

- GUI：双击 `ScheduleCenter.exe`（会弹出 UAC 提权）
- CLI：在管理员命令行中执行 `ScheduleCenter.exe <子命令> [选项]`

## CLI 用法

### 子命令一览

```
ScheduleCenter add     --name <名称> --path <程序路径> [--args <参数>] [--workdir <目录>]
                       --trigger <once|daily|weekly|monthly|boot|logon|idle|event>
                       [--time <HH:mm>] [--date <yyyy-MM-dd>] [--days <MON,WED,...>]
                       [--day-of-month <1-31>] [--start-when-available]
                       [--run-as-system] [--highest] [--description <文本>] [--enabled]
                       [--idle-wait <分钟>] [--idle-stop-on-end] [--idle-restart]
                       [--event-log <日志名>] [--event-source <源>] [--event-id <ID>]
                       [--event-subscription <XPath>]
                       [--execution-time-limit <分钟>] [--no-start-on-battery] [--stop-on-battery]
                       [--triggers-json <JSON>] [--triggers-file <文件路径>]
ScheduleCenter update  --name <名称> [同 add 的所有可选参数，只改提供的项]
ScheduleCenter delete  --name <名称> [--force]
ScheduleCenter get     --name <名称>
ScheduleCenter list    [--filter <通配符>]
ScheduleCenter enable  --name <名称>
ScheduleCenter disable --name <名称>
ScheduleCenter run     --name <名称>
ScheduleCenter history --name <名称> [--last <N>] [--errors-only]
ScheduleCenter export  --name <名称> [--output <文件路径>]
ScheduleCenter import  --file <XML 文件路径> --name <名称> [--force]
ScheduleCenter help      显示人友好的完整帮助文本（stdout，退出码 0）
ScheduleCenter h         help 的别名
ScheduleCenter manifest  输出机器可读的 CLI 清单 JSON（供 agent 解析命令、参数、类型、示例、错误码、退出码）
```

所有 `--name` 均相对于 `\ScheduleCenter\` 文件夹；支持子文件夹路径，如 `--name "MyApp\Backup"` 对应 `\ScheduleCenter\MyApp\Backup`。

### 自描述清单

工具内置 CLI 清单，便于人类用户和 agent 自动发现命令与参数（清单内容由 `src/ScheduleCenter/Cli/cli-manifest.json` 配置，编译为嵌入资源，不硬编码在代码中）：

- `ScheduleCenter help` 或 `ScheduleCenter h` — 输出人友好的完整帮助文本（命令列表 + 用法提示），并在结尾提示 agent 调用 `manifest` 获取机器可读清单
- `ScheduleCenter manifest` — 输出机器可读的 JSON 清单，包含所有命令、选项（含类型/必填/描述）、示例、错误码、退出码、互斥规则、输出形状 schema

调用未知命令时，工具会输出简短提示并建议运行 `help` 或 `manifest`；**agent 调用应优先使用 `manifest` 命令**以获取完整调用契约。

### 双轨触发器语法

V2 同时支持两种触发器输入方式（互斥）：

**单触发器（V1 兼容）**：用 `--trigger <类型>` + 对应参数，与 V1 完全一致。

**多触发器（V2 新增）**：用 `--triggers-json` 或 `--triggers-file` 传入 JSON 数组：

```bash
ScheduleCenter.exe add --name Backup --path C:\app.exe ^
  --triggers-json "[{\"kind\":\"daily\",\"time\":\"09:00\"},{\"kind\":\"weekly\",\"time\":\"10:00\",\"days\":[\"MON\"]}]"
```

JSON 字段：`kind`（必填）、`time`、`date`、`days`、`dayOfMonth`、`idleSettings`、`eventSubscription`、`eventLog`、`eventSource`、`eventId`。

### 成功输出（stdout，单行 JSON，退出码 0）

```json
{"success":true,"command":"add","task":{"name":"Backup","path":"C:\\app.exe","triggers":[{"kind":"daily","time":"09:00"}],"enabled":true,"executionTimeLimit":null,"disallowStartIfOnBatteries":false,"stopIfGoingOnBatteries":false,"nextRunTime":"2026-07-24T09:00:00"}}
```

- `list` 返回 `tasks` 数组
- `delete`/`enable`/`disable`/`run` 返回 `{"success":true,"command":"delete","name":"Backup"}`
- `export` 不带 `--output` 时直接将 XML 写到 stdout；带 `--output` 写文件并返回 `{"success":true,"command":"export","name":"Backup","path":"C:\\x.xml"}`
- `import` 返回 `{"success":true,"command":"import","name":"Restored","task":{...}}`

### 失败输出（stderr，退出码非 0）

```json
{"success":false,"command":"add","error":{"code":"TASK_EXISTS","message":"任务 'Backup' 已存在"}}
```

### 退出码约定

| 退出码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 一般错误（`INTERNAL_ERROR` / `HISTORY_DISABLED`） |
| 2 | 参数错误（`INVALID_ARGUMENTS` / `CONFIRM_REQUIRED` / `INVALID_PATH` / `INVALID_TRIGGER_FORMAT` / `INVALID_EVENT_SUBSCRIPTION` / `XML_PARSE_ERROR`） |
| 3 | 权限不足（`ACCESS_DENIED`） |
| 4 | 任务不存在（`TASK_NOT_FOUND`） |
| 5 | 任务已存在（`TASK_EXISTS`） |

### 输出约定

- 所有时间用本地时间 ISO 8601
- JSON 字段名 camelCase
- CLI 模式通过 `AttachConsole(-1)` 附加到调用方控制台写输出

## GUI 用法

启动后主窗口三栏布局：

- **左侧**：文件夹树（仅显示 `\ScheduleCenter\` 及其子文件夹）
- **右侧上部**：任务列表 DataGrid（名称 / 状态 / 下次运行 / 上次运行 / 结果）
- **右侧下部**：运行历史页签（时间 / 事件类型 / 结果码 / 消息；失败事件红色高亮；支持"全部/仅错误/仅完成"筛选）

工具栏：新建任务 / 导入 / 导出 / 刷新 / 搜索

任务操作：编辑、删除、启用/禁用、立即运行、导出...、复制 CLI 命令（双击任务或右键菜单也可）

### 任务编辑器

- **常规页**：名称、描述、程序路径（带浏览按钮）、参数、工作目录、安全选项（SYSTEM 账户/最高权限/启用）、高级条件（执行时间限制、电池供电时不启动、切换到电池时停止任务）
- **触发器页**：触发器列表（ListBox，支持新增/删除）+ 编辑面板，类型下拉支持 8 种触发器，下方区域随类型动态切换对应参数控件（时间/日期/星期复选框/每月几号/空闲设置/事件设置）

## 项目结构

```
ScheduleCenter.sln
├─ src/ScheduleCenter.Core/              核心类库（net48）
│  ├─ Models.cs                          数据模型（TriggerKind / TriggerSpec / TaskSpec / TaskUpdate / TaskInfo / IdleSettingsSpec / HistoryEvent）
│  ├─ Errors.cs                          ErrorCode 枚举 + TaskServiceException
│  ├─ TaskValidator.cs                   参数校验
│  ├─ ScheduledTaskService.cs            任务计划服务（增删改查/启用禁用/运行/历史/XML 导入导出）
│  ├─ HistoryService.cs                  历史事件读取
│  └─ CliCommandBuilder.cs               TaskInfo → 等效 add CLI 命令
├─ src/ScheduleCenter/                   WPF 主程序（net48 WinExe）
│  ├─ Cli/                               CLI 解析、调度、输出、自描述清单
│  │  ├─ CliParser.cs                    参数解析 + 互斥校验
│  │  ├─ SpecBuilder.cs                  ParsedArgs → TaskSpec/TaskUpdate（含多触发器 JSON 解析）
│  │  ├─ TaskDto.cs                      TaskInfo → JSON DTO
│  │  ├─ CliRunner.cs                    子命令分发（含 help/h/manifest）
│  │  ├─ OutputWriter.cs                 stdout/stderr JSON 输出
│  │  ├─ ManifestProvider.cs             加载嵌入清单 JSON，渲染 help 文本与 manifest JSON
│  │  ├─ cli-manifest.json               CLI 清单配置（命令/选项/类型/示例/错误码/退出码，嵌入资源）
│  │  └─ ConsoleHelper.cs                控制台附加 + UTF-8 输出编码
│  ├─ Gui/                               MVVM 视图模型
│  │  ├─ MainViewModel.cs                主窗口 VM
│  │  ├─ EditorViewModel.cs              编辑器 VM（多触发器列表 + Idle/Event + 高级条件）
│  │  ├─ TriggerSummaryConverter.cs      触发器列表摘要显示
│  │  ├─ InputDialog.xaml(.cs)           导入名称输入对话框
│  │  └─ ...
│  ├─ MainWindow.xaml(.cs)               主窗口
│  ├─ EditorWindow.xaml(.cs)             任务编辑器
│  └─ Properties/app.manifest            requireAdministrator
└─ tests/ScheduleCenter.Core.Tests/       MSTest 集成测试（net48）
   ├─ ServiceTestBase.cs                 测试基类（管理员检查 + \ScheduleCenter\Tests\ 隔离 + 自动清理）
   ├─ AddGetListTests.cs / UpdateDeleteRunTests.cs / HistoryTests.cs   V1 集成测试
   ├─ MultiTriggerTests.cs / IdleAndEventTriggerTests.cs / AdvancedConditionsTests.cs / XmlImportExportTests.cs   V2 集成测试
   └─ CliParserTests.cs / SpecBuilderTests.cs / CliCommandBuilderTests.cs / ValidatorTests.cs / ErrorsTests.cs   单元测试
```

## 测试

测试项目使用 MSTest + 真实 Task Scheduler 集成测试，**必须在管理员终端运行**：

```bash
dotnet test tests/ScheduleCenter.Core.Tests/ScheduleCenter.Core.Tests.csproj
```

- 测试任务全部创建在 `\ScheduleCenter\Tests\` 子文件夹内，命名 `Test_{guid}`，用完即删，保证幂等可重复
- 当前共 66 个测试通过，1 个跳过（`HistoryTests` — 系统未启用任务历史日志时 Inconclusive）
- GUI 不做自动化测试（延后到 V3），手动验证清单见实现计划文档

## 版本历史

### V2（2026-07-24）

新增高级触发器与条件 + 任务 XML 导入导出。**Breaking change**：数据模型 `Trigger`（单字段）→ `Triggers`（数组）；CLI JSON 输出 `trigger` 对象 → `triggers` 数组；CLI 输入语法保留 V1 单触发器兼容（双轨语法）。

### V1（2026-07-23）

基础功能：6 种触发器、增删改查、启用禁用、运行、历史、GUI 管理、CLI JSON 契约。

## 设计文档

完整设计文档与实现计划位于 `docs/superpowers/`：

- `specs/2026-07-23-schedule-center-design.md` — V1 设计文档
- `plans/2026-07-23-schedule-center.md` — V1 实现计划
- `specs/2026-07-24-schedule-center-v2-design.md` — V2 设计文档
- `plans/2026-07-24-schedule-center-v2.md` — V2 实现计划

## 范围声明

本工具不会管理所有 Windows 任务计划，只管理通过本工具创建的任务（位于 `\ScheduleCenter\` 文件夹下）。用户仍可在 Windows 自带"任务计划程序"中看到这些任务，这是正常且预期的。
