using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Cli;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class SpecBuilderTests
    {
        private static readonly string TestExe = @"C:\Windows\System32\cmd.exe";

        [TestMethod]
        public void BuildSpec_Weekly_ParsesTrigger()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "weekly", "--time", "09:00", "--days", "MON,WED,FRI"
            });
            TaskSpec spec = SpecBuilder.BuildSpec(p);

            Assert.AreEqual("Backup", spec.Name);
            Assert.AreEqual(TriggerKind.Weekly, spec.Triggers[0].Kind);
            Assert.AreEqual(new TimeSpan(9, 0, 0), spec.Triggers[0].Time);
            CollectionAssert.AreEqual(
                new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
                spec.Triggers[0].Days);
            Assert.IsTrue(spec.Enabled);
        }

        [TestMethod]
        public void BuildSpec_BadTimeFormat_Throws()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "daily", "--time", "9点"
            });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildSpec(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void BuildSpec_BadDay_Throws()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "Backup", "--path", TestExe,
                "--trigger", "weekly", "--time", "09:00", "--days", "周一"
            });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildSpec(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void BuildUpdate_OnlyProvidedOptions()
        {
            var p = CliParser.Parse(new[] { "update", "--name", "Backup", "--time", "10:00", "--trigger", "daily" });
            TaskUpdate u = SpecBuilder.BuildUpdate(p);

            Assert.AreEqual("Backup", u.Name);
            Assert.IsNull(u.Path);
            Assert.IsNotNull(u.Triggers);
            Assert.AreEqual(TriggerKind.Daily, u.Triggers[0].Kind);
        }

        [TestMethod]
        public void BuildUpdate_NothingToChange_Throws()
        {
            var p = CliParser.Parse(new[] { "update", "--name", "Backup" });
            var ex = Assert.ThrowsException<TaskServiceException>(() => SpecBuilder.BuildUpdate(p));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void BuildSpec_TriggersJson_ProducesMultipleTriggers()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"},{\"kind\":\"boot\"}]" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(2, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, spec.Triggers[0].Kind);
            Assert.AreEqual(TriggerKind.Boot, spec.Triggers[1].Kind);
        }

        [TestMethod]
        public void BuildSpec_SingleTrigger_V1Compat_ProducesOneElementList()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "daily", "--time", "09:00" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(1, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, spec.Triggers[0].Kind);
        }

        [TestMethod]
        public void BuildSpec_EventTrigger_SimplifiedArgs()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "event", "--event-log", "System", "--event-id", "42" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(1, spec.Triggers.Count);
            Assert.AreEqual(TriggerKind.Event, spec.Triggers[0].Kind);
            Assert.AreEqual("System", spec.Triggers[0].EventLog);
            Assert.AreEqual(42, spec.Triggers[0].EventId);
        }

        [TestMethod]
        public void BuildSpec_ExecutionTimeLimit_Propagated()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", @"C:\Windows\System32\cmd.exe",
                "--trigger", "daily", "--time", "09:00", "--execution-time-limit", "30" });
            TaskSpec spec = SpecBuilder.BuildSpec(parsed);
            Assert.AreEqual(System.TimeSpan.FromMinutes(30), spec.ExecutionTimeLimit);
        }
    }
}
