using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class AdvancedConditionsTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_WithExecutionTimeLimit_StoredAndReadBack()
        {
            string name = UniqueName("Etl");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                ExecutionTimeLimit = TimeSpan.FromMinutes(30)
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TimeSpan.FromMinutes(30), info.ExecutionTimeLimit);
        }

        [TestMethod]
        public void Add_NoExecutionTimeLimit_ReadsAsNull()
        {
            string name = UniqueName("EtlNull");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } }
                // ExecutionTimeLimit 未设置，默认 null → TimeSpan.Zero 写入
            });

            TaskInfo info = Service.Get(name);
            Assert.IsNull(info.ExecutionTimeLimit);
        }

        [TestMethod]
        public void Add_WithPowerConditions_StoredAndReadBack()
        {
            string name = UniqueName("Pwr");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                DisallowStartIfOnBatteries = true,
                StopIfGoingOnBatteries = true
            });

            TaskInfo info = Service.Get(name);
            Assert.IsTrue(info.DisallowStartIfOnBatteries);
            Assert.IsTrue(info.StopIfGoingOnBatteries);
        }

        [TestMethod]
        public void Update_ChangesExecutionTimeLimit()
        {
            string name = UniqueName("EtlUpd");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec> { new TriggerSpec { Kind = TriggerKind.Daily, Time = TimeSpan.FromHours(9) } },
                ExecutionTimeLimit = TimeSpan.FromMinutes(30)
            });

            Service.Update(new TaskUpdate { Name = name, ExecutionTimeLimit = TimeSpan.FromMinutes(60) });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TimeSpan.FromMinutes(60), info.ExecutionTimeLimit);
        }
    }
}
