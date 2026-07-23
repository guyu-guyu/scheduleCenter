using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class HistoryTests : ServiceTestBase
    {
        [TestMethod]
        public void GetHistory_AfterRun_ReturnsEvents()
        {
            var history = new HistoryService();
            if (!history.IsLogEnabled())
                Assert.Inconclusive("系统未启用任务历史记录日志");

            string name = UniqueName("Hist");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "/c exit 0",
                Trigger = new TriggerSpec { Kind = TriggerKind.Daily, Time = new TimeSpan(9, 0, 0) }
            });
            Service.Run(name);

            // 事件日志写入有延迟，最多等 15 秒
            bool found = false;
            for (int i = 0; i < 15 && !found; i++)
            {
                Thread.Sleep(1000);
                var events = Service.GetHistory(name, 10, false);
                found = events.Count > 0;
            }
            Assert.IsTrue(found, "运行任务后 15 秒内应能读到历史事件");
        }

        [TestMethod]
        public void GetHistory_NotExists_ThrowsTaskNotFound()
        {
            var ex = Assert.ThrowsException<TaskServiceException>(() => Service.GetHistory("NoSuch_12345", null, false));
            Assert.AreEqual(ErrorCode.TaskNotFound, ex.Code);
        }
    }
}
