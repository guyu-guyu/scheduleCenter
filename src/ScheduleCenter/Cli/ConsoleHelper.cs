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
