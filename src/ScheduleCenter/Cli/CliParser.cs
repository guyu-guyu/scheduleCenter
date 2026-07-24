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
        { "add", "update", "delete", "get", "list", "enable", "disable", "run", "history", "export", "import",
          "help", "h", "manifest" };

        // 元命令：不带业务参数，跳过互斥校验
        private static readonly HashSet<string> MetaCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "help", "h", "manifest" };

        private static readonly HashSet<string> Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "force", "run-as-system", "highest", "enabled", "start-when-available", "errors-only",
          "no-start-on-battery", "stop-on-battery", "idle-stop-on-end", "idle-restart" };

        private static readonly HashSet<string> ValueOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "name", "path", "args", "workdir", "trigger", "time", "date", "days", "day-of-month", "description", "filter", "last",
          "triggers-json", "triggers-file", "event-log", "event-source", "event-id", "event-subscription",
          "idle-wait", "execution-time-limit", "output", "file" };

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
            // 元命令（help/h/manifest）跳过业务参数互斥校验
            if (!MetaCommands.Contains(command))
                ValidateMutualExclusion(result);
            return result;
        }

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
                if (!isIdle && (p.Has("idle-wait") || p.Has("idle-stop-on-end") || p.Has("idle-restart")))
                    throw new TaskServiceException(ErrorCode.InvalidArguments,
                        "--idle-* 参数仅在 --trigger idle 时可用");
            }
            else if (p.Has("idle-wait") || p.Has("idle-stop-on-end") || p.Has("idle-restart"))
            {
                throw new TaskServiceException(ErrorCode.InvalidArguments,
                    "--idle-* 参数仅在 --trigger idle 时可用");
            }
        }

        public static string Usage()
        {
            try
            {
                return ManifestProvider.RenderHelpText();
            }
            catch
            {
                // 嵌入资源加载失败时兜底，保证工具仍可用
                return "用法: ScheduleCenter <add|update|delete|get|list|enable|disable|run|history|export|import|help|manifest> [选项]\n" +
                       "运行 'ScheduleCenter help' 查看完整帮助；运行 'ScheduleCenter manifest' 获取机器可读的 CLI 清单。\n" +
                       "示例: ScheduleCenter add --name Backup --path C:\\app.exe --trigger daily --time 09:00";
            }
        }
    }
}
