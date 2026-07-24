using System;
using System.Runtime.InteropServices;
using System.Text;

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

            // 统一 UTF-8 输出：保证 agent 调用解析中文稳定，避免系统默认 GBK 与管道交互乱码
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch
            {
                // 某些环境下无法修改编码（如无控制台句柄），忽略以保持原有行为
            }
        }
    }
}
