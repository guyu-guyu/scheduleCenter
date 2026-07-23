using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class ValidatorTests
    {
        private static void ExpectInvalid(Action action)
        {
            var ex = Assert.ThrowsException<TaskServiceException>(action);
            Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
        }

        [TestMethod]
        public void ValidateName_Valid_Passes()
        {
            TaskValidator.ValidateName("Backup");
            TaskValidator.ValidateName("MyApp\\Backup");
        }

        [TestMethod]
        public void ValidateName_Invalid_Throws()
        {
            ExpectInvalid(() => TaskValidator.ValidateName(null));
            ExpectInvalid(() => TaskValidator.ValidateName(""));
            ExpectInvalid(() => TaskValidator.ValidateName("  "));
            ExpectInvalid(() => TaskValidator.ValidateName("a\\\\b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a/b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a*b"));
            ExpectInvalid(() => TaskValidator.ValidateName("a'b"));
            ExpectInvalid(() => TaskValidator.ValidateName(" a "));
            ExpectInvalid(() => TaskValidator.ValidateName(".."));
        }

        [TestMethod]
        public void ValidateTrigger_Once_RequiresFutureDateTime()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Once }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Once,
                Date = DateTime.Today.AddDays(-1),
                Time = new TimeSpan(9, 0, 0)
            }));
            TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Once,
                Date = DateTime.Today.AddDays(1),
                Time = new TimeSpan(9, 0, 0)
            });
        }

        [TestMethod]
        public void ValidateTrigger_Weekly_RequiresTimeAndDays()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Weekly }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(9, 0, 0) }));
            TaskValidator.ValidateTrigger(new TriggerSpec
            {
                Kind = TriggerKind.Weekly,
                Time = new TimeSpan(9, 0, 0),
                Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday }
            });
        }

        [TestMethod]
        public void ValidateTrigger_Monthly_RequiresDayOfMonth1To31()
        {
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0) }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 0 }));
            ExpectInvalid(() => TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 32 }));
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 15 });
        }

        [TestMethod]
        public void ValidateTrigger_BootAndLogon_NoExtraParams()
        {
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Boot });
            TaskValidator.ValidateTrigger(new TriggerSpec { Kind = TriggerKind.Logon });
        }

        [TestMethod]
        public void ValidateSpec_InvalidPath_ThrowsInvalidPath()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => TaskValidator.ValidateSpec(new TaskSpec
            {
                Name = "X",
                Path = @"C:\definitely\not\exists_12345.exe",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            }));
            Assert.AreEqual(ErrorCode.InvalidPath, ex.Code);
        }
    }
}
