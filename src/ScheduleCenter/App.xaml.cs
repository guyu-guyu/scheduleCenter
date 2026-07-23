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
