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
