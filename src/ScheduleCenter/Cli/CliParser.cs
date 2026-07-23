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
