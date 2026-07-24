using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class MultiTriggerTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_MultipleTriggers_CreatesTaskWithAllTriggers()
        {
            string name = UniqueName("Multi");
            var spec = new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) },
                    new TriggerSpec { Kind = TriggerKind.Weekly, Time = new System.TimeSpan(10, 0, 0), Days = new[] { System.DayOfWeek.Monday } }
                }
            };
            Service.Add(spec);

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(2, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, info.Triggers[0].Kind);
            Assert.AreEqual(TriggerKind.Weekly, info.Triggers[1].Kind);
        }

        [TestMethod]
        public void Update_ReplacesEntireTriggerList()
        {
            string name = UniqueName("MultiUpd");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) },
                    new TriggerSpec { Kind = TriggerKind.Weekly, Time = new System.TimeSpan(10, 0, 0), Days = new[] { System.DayOfWeek.Monday } }
                }
            });

            Service.Update(new TaskUpdate
            {
                Name = name,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Boot }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Boot, info.Triggers[0].Kind);
        }

        [TestMethod]
        public void Update_NullTriggers_KeepsExisting()
        {
            string name = UniqueName("MultiKeep");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = new System.TimeSpan(9, 0, 0) }
                }
            });

            Service.Update(new TaskUpdate { Name = name, Description = "changed" });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, info.Triggers[0].Kind);
        }

        [TestMethod]
        public void Add_EmptyTriggers_ThrowsInvalidArguments()
        {
            string name = UniqueName("Empty");
            try
            {
                Service.Add(new TaskSpec
                {
                    Name = name,
                    Path = TestExe,
                    Triggers = new List<TriggerSpec>()
                });
                Assert.Fail("应抛异常");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidArguments, ex.Code);
            }
        }
    }
}
