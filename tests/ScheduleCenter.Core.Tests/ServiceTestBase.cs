using System;
using System.Collections.Generic;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public abstract class ServiceTestBase
    {
        protected const string TestFolder = "Tests";
        protected ScheduledTaskService Service;
        protected readonly List<string> Created = new List<string>();
        protected static readonly string TestExe = @"C:\Windows\System32\cmd.exe";

        [TestInitialize]
        public void BaseSetup()
        {
            using (var id = WindowsIdentity.GetCurrent())
            {
                if (!new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator))
                    Assert.Inconclusive("需要以管理员身份运行测试");
            }
            Service = new ScheduledTaskService();
        }

        protected string UniqueName(string prefix)
        {
            string name = TestFolder + "\\" + prefix + "_" + Guid.NewGuid().ToString("N");
            Created.Add(name);
            return name;
        }

        [TestCleanup]
        public void BaseCleanup()
        {
            foreach (string name in Created)
            {
                try { Service.Delete(name, true); } catch { }
            }
        }
    }
}
