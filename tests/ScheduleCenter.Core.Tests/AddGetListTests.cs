using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class AddGetListTests : ServiceTestBase
    {
        private TaskSpec Spec(string name, TriggerSpec trigger)
        {
            return new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Description = "集成测试任务",
                Trigger = trigger,
                Highest = true
            };
        }

        [TestMethod]
        public void Add_AllTriggerKinds_CreatesTasks()
        {
            var triggers = new[]
            {
                new TriggerSpec { Kind = TriggerKind.Once, Date = DateTime.Today.AddDays(1), Time = new TimeSpan(9, 0, 0) },
                new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) },
                new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(9, 0, 0), Days = new[] { DayOfWeek.Monday, DayOfWeek.Friday } },
                new TriggerSpec { Kind = TriggerKind.Monthly, Time = new TimeSpan(9, 0, 0), DayOfMonth = 15 },
                new TriggerSpec { Kind = TriggerKind.Boot },
                new TriggerSpec { Kind = TriggerKind.Logon }
            };

            foreach (var trigger in triggers)
            {
                string name = UniqueName(trigger.Kind.ToString());
                TaskInfo info = Service.Add(Spec(name, trigger));
                Assert.AreEqual(name, info.RelativeName);
                Assert.AreEqual(trigger.Kind, info.Trigger.Kind);
                Assert.IsTrue(info.Highest);

                TaskInfo got = Service.Get(name);
                Assert.AreEqual(TestExe, got.Path);
                Assert.AreEqual("/c exit 0", got.Arguments);
                Assert.AreEqual("集成测试任务", got.Description);
            }
        }

        [TestMethod]
        public void Add_DuplicateName_ThrowsTaskExists()
        {
            string name = UniqueName("Dup");
            Service.Add(Spec(name, new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }));
            var ex = Assert.ThrowsException<TaskServiceException>(() =>
                Service.Add(Spec(name, new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(10, 0, 0) })));
            Assert.AreEqual(ErrorCode.TaskExists, ex.Code);
        }

        [TestMethod]
        public void Get_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get("NoSuchTask_12345"));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void List_WithWildcardFilter_ReturnsMatches()
        {
            string nameA = UniqueName("FilterAA");
            string nameB = UniqueName("FilterAB");
            Service.Add(Spec(nameA, new TriggerSpec { Kind = TriggerKind.Boot }));
            Service.Add(Spec(nameB, new TriggerSpec { Kind = TriggerKind.Boot }));

            var all = Service.List(null);
            Assert.IsTrue(all.Any(t => t.RelativeName == nameA));
            Assert.IsTrue(all.Any(t => t.RelativeName == nameB));

            string leafPrefix = nameA.Split('\\')[1];
            var filtered = Service.List(leafPrefix.Substring(0, 8) + "*");
            Assert.IsTrue(filtered.Any(t => t.RelativeName == nameA));
        }
    }
}
