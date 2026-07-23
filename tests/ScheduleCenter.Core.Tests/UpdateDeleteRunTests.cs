using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.TaskScheduler;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class UpdateDeleteRunTests : ServiceTestBase
    {
        private TaskSpec DailySpec(string name)
        {
            return new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            };
        }

        [TestMethod]
        public void Update_ChangesTimeAndTriggerKind()
        {
            string name = UniqueName("Upd");
            Service.Add(DailySpec(name));

            Service.Update(new TaskUpdate
            {
                Name = name,
                Trigger = new TriggerSpec { Kind = TriggerKind.Weekly, Time = new TimeSpan(10, 30, 0), Days = new[] { DayOfWeek.Wednesday } }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TriggerKind.Weekly, info.Trigger.Kind);
            Assert.AreEqual(new TimeSpan(10, 30, 0), info.Trigger.Time);
            CollectionAssert.AreEqual(new[] { DayOfWeek.Wednesday }, info.Trigger.Days);
        }

        [TestMethod]
        public void Update_ChangesPathAndSystemFlag()
        {
            string name = UniqueName("Upd2");
            Service.Add(DailySpec(name));

            Service.Update(new TaskUpdate
            {
                Name = name,
                Arguments = "/c exit 1",
                RunAsSystem = true,
                Highest = true
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual("/c exit 1", info.Arguments);
            Assert.IsTrue(info.RunAsSystem);
            Assert.IsTrue(info.Highest);
        }

        [TestMethod]
        public void Delete_WithoutForce_ThrowsConfirmRequired()
        {
            string name = UniqueName("Del");
            Service.Add(DailySpec(name));
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Delete(name, false));
            Assert.AreEqual(ErrorCode.ConfirmRequired, ex.Code);
            Service.Get(name); // 任务仍在
        }

        [TestMethod]
        public void Delete_WithForce_RemovesTask()
        {
            string name = UniqueName("Del2");
            Service.Add(DailySpec(name));
            Service.Delete(name, true);
            Created.Remove(name);
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get(name));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void SetEnabled_TogglesState()
        {
            string name = UniqueName("En");
            Service.Add(DailySpec(name));
            Service.SetEnabled(name, false);
            Assert.IsFalse(Service.Get(name).Enabled);
            Service.SetEnabled(name, true);
            Assert.IsTrue(Service.Get(name).Enabled);
        }

        [TestMethod]
        public void Run_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Run("NoSuch_12345"));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }

        [TestMethod]
        public void Isolation_TaskOutsideScope_NotVisible()
        {
            string outsideName = "ScOutside_" + Guid.NewGuid().ToString("N");
            using (var ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.Triggers.Add(new DailyTrigger(1) { StartBoundary = DateTime.Today.AddHours(9) });
                td.Actions.Add(new ExecAction(TestExe, "/c exit 0", null));
                try
                {
                    ts.RootFolder.RegisterTaskDefinition(outsideName, td, TaskCreation.CreateOrUpdate,
                        null, null, TaskLogonType.InteractiveToken, null);

                    var ex = Assert.ThrowsException<TaskServiceException>(() => Service.Get(outsideName));
                    Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
                    Assert.IsFalse(Service.List(null).Any(t => t.Name == outsideName));

                    var delEx = Assert.ThrowsException<TaskServiceException>(() => Service.Delete(outsideName, true));
                    Assert.AreEqual(ErrorCode.TaskNotFound, delEx.Code);
                }
                finally
                {
                    ts.RootFolder.DeleteTask(outsideName, false);
                }
            }
        }
    }
}
