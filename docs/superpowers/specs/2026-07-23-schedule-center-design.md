# ScheduleCenter 设计文档

日期：2026-07-23
状态：已确认

## 1. 概述

ScheduleCenter 是一个 Windows 中间工具，既能提供 GUI 管理界面，也能作为 CLI 被其他程序调用，用于添加、修改、删除、查询 Windows 任务计划。

- 单 exe 双模式：无参数启动进入 WPF GUI；带参数启动作为 CLI 运行
- 通过 `TaskScheduler` NuGet 包（dahall 维护）调用 Windows 原生 Task Scheduler 2.0 COM API（`ITaskService`）
- 目标框架：.NET Framework 4.8（Windows 10/11 自带，免安装运行时）
- 程序需要管理员权限（manifest 声明 `requireAdministrator`），支持创建 SYSTEM 账户 / 最高权限任务
- 界面语言：中文

## 2. 管理范围（重要声明）

**本工具不会显示并管理所有的 Windows 任务计划，只会管理通过该工具添加的任务计划。**

实现方式：**专用文件夹隔离**。所有通过本工具创建的任务都放在任务计划程序的 `\ScheduleCenter\` 文件夹下：

- 首次使用时自动创建 `\ScheduleCenter\` 文件夹
- add/update/delete/get/enable/disable/run/history 全部只作用于该文件夹（含其子文件夹）
- list 只列出该文件夹内的任务
- GUI 的文件夹树只显示 `\ScheduleCenter\` 及其子文件夹，不显示系统任务
- 系统自带任务、其他软件创建的任务对本工具完全不可见、不可操作
- 用户仍可在 Windows 自带"任务计划程序"中看到这些任务（位于 ScheduleCenter 文件夹下），这是正常且预期的

## 3. 整体架构

```
ScheduleCenter.exe          单 exe 双模式入口
├─ 无参数启动 → WPF GUI（完整管理界面）
└─ 带参数启动 → CLI 模式（子命令 + JSON 输出 + 退出码）
```

### 分层结构

| 层 | 职责 | 依赖 |
|---|---|---|
| `Core`（类库） | 任务计划服务：增/删/改/查/启用/禁用/运行/历史，全部通过 TaskScheduler 库调原生 COM API，操作范围限定 `\ScheduleCenter\` 文件夹 | TaskScheduler NuGet |
| `Cli`（集成在 exe 内） | 解析命令行参数 → 调 Core → 序列化 JSON 到 stdout / 错误到 stderr + 退出码 | Core |
| `Gui`（WPF） | MVVM 界面：任务列表、编辑器、历史页签、操作按钮，全部调 Core | Core |

### 关键决策

- 单一 VS 解决方案 `ScheduleCenter.sln`：1 个 WPF 项目 + 1 个 Core 类库项目（.NET Framework 4.8）
- exe 的 `app.manifest` 声明 `requireAdministrator`，启动即提权
- 模式判定：`args.Length == 0` → GUI；否则 → CLI
- WPF 程序默认无控制台，CLI 模式时用 `AttachConsole(-1)` 附加到调用方控制台写输出
- GUI 提供"复制等效 CLI 命令"功能

## 4. CLI 命令与 JSON 契约

### 子命令

```
ScheduleCenter add     --name <名称> --path <程序路径> [--args <参数>] [--workdir <目录>]
                       --trigger <once|daily|weekly|monthly|boot|logon>
                       [--time <HH:mm>] [--date <yyyy-MM-dd>] [--days <MON,WED,...>]
                       [--day-of-month <1-31>] [--start-when-available]
                       [--run-as-system] [--highest] [--description <文本>] [--enabled]
ScheduleCenter update  --name <名称> [同 add 的所有可选参数，只改提供的项]
ScheduleCenter delete  --name <名称> [--force]
ScheduleCenter get     --name <名称>
ScheduleCenter list    [--filter <通配符>]
ScheduleCenter enable  --name <名称>
ScheduleCenter disable --name <名称>
ScheduleCenter run     --name <名称>
ScheduleCenter history --name <名称> [--last <N>] [--errors-only]
```

所有命令的 `--name` 均相对于 `\ScheduleCenter\` 文件夹；支持子文件夹路径，如 `--name "MyApp\Backup"` 对应 `\ScheduleCenter\MyApp\Backup`。

**delete 的确认语义：** CLI 为非交互场景设计，不提示输入。`delete` 不带 `--force` 时不执行删除，返回错误 `CONFIRM_REQUIRED`（退出码 2）；带 `--force` 才真正删除。GUI 则通过确认对话框完成同样的确认动作。

### 触发器参数规则

| trigger | 必需参数 | 可选参数 |
|---|---|---|
| `once` | `--date` `--time` | — |
| `daily` | `--time` | — |
| `weekly` | `--time` `--days`（如 `MON,WED,FRI`） | — |
| `monthly` | `--time` `--day-of-month` | — |
| `boot` | — | — |
| `logon` | — | — |

### 成功输出（stdout，单行 JSON，退出码 0）

```json
{"success":true,"command":"add","task":{"name":"Backup","path":"C:\\app.exe","args":"--full","trigger":{"type":"weekly","time":"09:00","days":["MON","FRI"]},"enabled":true,"runAsSystem":false,"highest":true,"nextRunTime":"2026-07-24T09:00:00","folder":"\\ScheduleCenter\\"}}
```

- `list` 返回 `tasks` 数组
- `delete`/`enable`/`disable`/`run` 返回 `{"success":true,"command":"delete","name":"Backup"}`
- `history` 返回：

```json
{"success":true,"command":"history","name":"Backup","events":[{"time":"2026-07-23T09:00:01","type":"completed","resultCode":0,"message":"任务已完成"}]}
```

### 失败输出（stderr，退出码非 0）

```json
{"success":false,"command":"add","error":{"code":"TASK_EXISTS","message":"任务 'Backup' 已存在"}}
```

### 退出码约定

| 退出码 | 含义 |
|---|---|
| 0 | 成功 |
| 1 | 一般错误 |
| 2 | 参数错误（缺参数/格式错/组合非法） |
| 3 | 权限不足 |
| 4 | 任务不存在 |
| 5 | 任务已存在 |

### 输出约定

- 所有时间用本地时间 ISO 8601
- JSON 字段名 camelCase

## 5. GUI 设计

### 主窗口（三栏布局，MVVM）

```
┌──────────────────────────────────────────────────────────┐
│ 工具栏: [新建任务] [刷新]  搜索框: [________]             │
├────────────┬─────────────────────────────────────────────┤
│ 文件夹树   │ 任务列表 (DataGrid)                          │
│ ScheduleCt │ 名称 | 状态 | 下次运行 | 上次运行 | 结果      │
│  └ MyApp   │ ─────────────────────────────────────────── │
│            │ 选中任务后底部操作栏:                        │
│            │ [编辑] [删除] [启用/禁用] [立即运行]          │
│            │ [复制 CLI 命令]                              │
├────────────┴─────────────────────────────────────────────┤
│ 状态栏: 共 N 个任务 | 已选中 1 个                         │
└──────────────────────────────────────────────────────────┘
```

文件夹树只显示 `\ScheduleCenter\` 及其子文件夹。

### 任务编辑器对话框（新建/编辑共用）

- 常规页：名称、描述、程序路径（带浏览按钮）、参数、工作目录、安全选项（当前用户/SYSTEM、是否最高权限、启用复选框）
- 触发器页：类型下拉（once/daily/weekly/monthly/boot/logon），下方区域随类型动态切换对应参数控件（日期时间选择器、星期复选框组、每月几号输入框）
- 保存前做与 CLI 相同的参数校验，错误就地高亮提示

### 历史记录页签

- 主窗口选中任务后显示"历史"页签，展示该任务运行历史
- 表格列：时间、事件类型（已触发/已启动/已完成/失败等）、结果码、消息
- 顶部 [刷新] 按钮 + 事件类型筛选下拉（全部/错误/完成）
- 失败事件行红色高亮
- 数据来源：事件日志 `Microsoft-Windows-TaskScheduler/Operational`（通过 TaskScheduler 库的 `Task.History` 读取）
- 若系统未启用任务历史记录，界面显示引导文案提示用户在"任务计划程序"中启用"所有任务历史记录"；程序不自动修改系统设置

### 行为细节

- 列表行右键菜单与底部操作栏功能相同；双击进入编辑
- 删除前弹确认框（等效 CLI 的 `--force` 语义）
- "复制 CLI 命令"把当前任务生成等效 `add` 命令放入剪贴板
- 所有操作调 Core 层，成功后刷新列表；失败弹错误对话框显示 error.code + message

### V1 不做

- 条件/设置页高级选项（空闲触发、事件触发、多触发器组合等，留待后续版本）
- 任务导入导出 XML
- GUI 自动化测试

## 6. 错误处理

### 统一错误模型

- Core 定义 `TaskServiceException`：携带 `ErrorCode` 枚举 + 中文消息
- 所有 COM 异常、参数校验异常在 Core 边界处捕获并翻译成该类型，CLI 与 GUI 只面对统一错误模型

### ErrorCode 枚举（与 CLI JSON 的 `error.code` 一一对应）

| code | 退出码 | 触发场景 |
|---|---|---|
| `INVALID_ARGUMENTS` | 2 | 缺必需参数、时间格式错误、trigger 与参数组合非法 |
| `CONFIRM_REQUIRED` | 2 | delete 未带 `--force` |
| `TASK_NOT_FOUND` | 4 | get/update/delete/enable/disable/run/history 找不到任务（在 `\ScheduleCenter\` 内） |
| `TASK_EXISTS` | 5 | add 同名任务已存在 |
| `ACCESS_DENIED` | 3 | 非管理员运行或权限不足 |
| `HISTORY_DISABLED` | 1 | 任务历史事件日志未启用 |
| `INVALID_PATH` | 2 | `--path` 指向的可执行文件不存在 |
| `INTERNAL_ERROR` | 1 | 其他未分类异常（message 带原始异常信息） |

### 各模式行为

- CLI：任何未预期异常兜底捕获 → `INTERNAL_ERROR` JSON 到 stderr，退出码 1，绝不输出堆栈到 stdout（保证调用方解析稳定）
- GUI：操作失败弹对话框显示 code + message；未预期异常全局捕获（`DispatcherUnhandledException`）→ 写本地日志文件 `%LOCALAPPDATA%\ScheduleCenter\error.log` + 友好提示

### 参数校验（Core 层，CLI/GUI 复用）

- 名称非空且不含非法字符（`/` 及 Windows 任务计划保留字符）
- 时间格式正确（HH:mm）
- once 触发器的日期时间不为过去
- days 值合法（MON-SUN）
- day-of-month ∈ 1-31
- 程序路径存在

## 7. 测试

- 测试项目 `Core.Tests`（MSTest + .NET 4.8），用真实 Task Scheduler 做集成测试
- 测试自身要求管理员运行；测试任务全部创建在 `\ScheduleCenter\Tests\` 子文件夹内，命名 `Test_{guid}`，用完即删，保证幂等可重复且不污染正常使用数据
- 覆盖：
  - add（六种触发器各一）
  - update（改时间/改触发器类型）
  - delete、get、list、enable/disable、run、history
  - 错误路径：TASK_NOT_FOUND / TASK_EXISTS / INVALID_ARGUMENTS
  - 隔离性： `\ScheduleCenter\` 之外存在同名任务时不被识别和操作
- CLI 层做参数解析的单元测试（不碰 COM）：合法/非法输入 → 解析结果或对应 ErrorCode
- GUI 不做自动化测试（V1），手动验证清单随实现计划提供
