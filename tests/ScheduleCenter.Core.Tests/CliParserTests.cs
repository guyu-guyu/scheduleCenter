using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Cli;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class CliParserTests
    {
        [TestMethod]
        public void Parse_ValidAdd_AllOptionsCaptured()
        {
            var p = CliParser.Parse(new[]
            {
                "add", "--name", "MyApp\\Backup", "--path", @"C:\app.exe",
                "--trigger", "weekly", "--time", "09:00", "--days", "MON,FRI",
                "--highest", "--enabled"
            });
            Assert.AreEqual("add", p.Command);
            Assert.AreEqual("MyApp\\Backup", p.Get("name"));
            Assert.AreEqual(@"C:\app.exe", p.Get("path"));
            Assert.AreEqual("weekly", p.Get("trigger"));
            Assert.AreEqual("09:00", p.Get("time"));
            Assert.AreEqual("MON,FRI", p.Get("days"));
            Assert.IsTrue(p.Has("highest"));
            Assert.IsTrue(p.Has("enabled"));
            Assert.IsFalse(p.Has("force"));
        }

        [TestMethod]
        public void Parse_ValueStartingWithDoubleDash_AllowedWhenNotKnownOption()
        {
            var p = CliParser.Parse(new[] { "add", "--name", "X", "--args", "--full" });
            Assert.AreEqual("--full", p.Get("args"));
        }

        [TestMethod]
        public void Parse_UnknownCommand_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => CliParser.Parse(new[] { "explode" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Parse_MissingValue_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                CliParser.Parse(new[] { "add", "--name", "--trigger", "daily" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Parse_UnknownOption_Throws()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                CliParser.Parse(new[] { "add", "--name", "X", "--bogus", "1" }));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Require_MissingOption_Throws()
        {
            var p = CliParser.Parse(new[] { "get", "--name", "X" });
            var ex = Assert.ThrowsException<TaskServiceException>(() => p.Require("path"));
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void Parse_TriggersJson_OptionRecognized()
        {
            var parsed = CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"}]" });
            Assert.AreEqual("add", parsed.Command);
            Assert.IsTrue(parsed.Has("triggers-json"));
        }

        [TestMethod]
        public void Parse_TriggerAndTriggersJson_MutuallyExclusive_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "daily", "--time", "09:00",
                    "--triggers-json", "[{\"kind\":\"daily\",\"time\":\"09:00\"}]" });
                Assert.Fail("应抛互斥错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_EventLogAndEventSubscription_MutuallyExclusive_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "event", "--event-log", "System", "--event-subscription", "<q/>" });
                Assert.Fail("应抛互斥错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_IdleArgsWithNonIdleTrigger_Throws()
        {
            try
            {
                CliParser.Parse(new[] { "add", "--name", "X", "--path", "C:\\x.exe",
                    "--trigger", "daily", "--time", "09:00", "--idle-wait", "5" });
                Assert.Fail("应抛错误");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }

        [TestMethod]
        public void Parse_ExportImport_CommandsRecognized()
        {
            var p1 = CliParser.Parse(new[] { "export", "--name", "X", "--output", "C:\\x.xml" });
            Assert.AreEqual("export", p1.Command);
            var p2 = CliParser.Parse(new[] { "import", "--file", "C:\\x.xml", "--name", "Y" });
            Assert.AreEqual("import", p2.Command);
        }
    }
}
