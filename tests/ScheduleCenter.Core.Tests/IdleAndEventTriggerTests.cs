using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class IdleAndEventTriggerTests : ServiceTestBase
    {
        [TestMethod]
        public void Add_IdleTrigger_WithSettings_StoredAndReadBack()
        {
            string name = UniqueName("Idle");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec
                    {
                        Kind = TriggerKind.Idle,
                        IdleSettings = new IdleSettingsSpec
                        {
                            WaitTimeout = System.TimeSpan.FromMinutes(5),
                            StopOnIdleEnd = true,
                            RestartOnIdle = false
                        }
                    }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Idle, info.Triggers[0].Kind);
            Assert.IsNotNull(info.Triggers[0].IdleSettings);
            Assert.AreEqual(System.TimeSpan.FromMinutes(5), info.Triggers[0].IdleSettings.WaitTimeout);
            Assert.IsTrue(info.Triggers[0].IdleSettings.StopOnIdleEnd);
        }

        [TestMethod]
        public void Add_EventTrigger_SimplifiedArgs_GeneratesSubscription()
        {
            string name = UniqueName("Evt");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec
                    {
                        Kind = TriggerKind.Event,
                        EventLog = "System",
                        EventSource = "Microsoft-Windows-Kernel-Power",
                        EventId = 42
                    }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(1, info.Triggers.Count);
            Assert.AreEqual(TriggerKind.Event, info.Triggers[0].Kind);
            Assert.IsFalse(string.IsNullOrEmpty(info.Triggers[0].EventSubscription));
            StringAssert.Contains(info.Triggers[0].EventSubscription, "System");
            StringAssert.Contains(info.Triggers[0].EventSubscription, "Kernel-Power");
            StringAssert.Contains(info.Triggers[0].EventSubscription, "42");
        }

        [TestMethod]
        public void Add_EventTrigger_FullSubscription_StoredAsIs()
        {
            string name = UniqueName("EvtFull");
            string xpath = "<QueryList><Query Id=\"0\" Path=\"Application\"><Select Path=\"Application\">*[System[EventID=1000]]</Select></Query></QueryList>";
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Event, EventSubscription = xpath }
                }
            });

            TaskInfo info = Service.Get(name);
            Assert.AreEqual(TriggerKind.Event, info.Triggers[0].Kind);
            Assert.AreEqual(xpath, info.Triggers[0].EventSubscription);
        }
    }
}
