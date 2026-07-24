using System;

namespace ScheduleCenter.Cli
{
    /// <summary>
    /// 未知命令时抛出。走独立的简短提示输出路径，不进入 JSON OutputWriter，
    /// 避免把完整 help 文本塞进 JSON message 字段造成 agent 解析负担。
    /// </summary>
    public sealed class UnknownCommandException : Exception
    {
        public string UnknownCommand { get; }

        public UnknownCommandException(string command)
            : base("未知命令: " + command)
        {
            UnknownCommand = command;
        }
    }
}
