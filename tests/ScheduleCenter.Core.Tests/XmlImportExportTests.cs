using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class XmlImportExportTests : ServiceTestBase
    {
        [TestMethod]
        public void Export_ReturnsValidXml()
        {
            string name = UniqueName("Exp");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            Assert.IsFalse(string.IsNullOrEmpty(xml));
            XDocument doc = XDocument.Parse(xml); // 不抛异常即合法
            Assert.IsNotNull(doc.Root);
        }

        [TestMethod]
        public void ExportToFile_WritesFile()
        {
            string name = UniqueName("ExpF");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string tmpFile = Path.Combine(Path.GetTempPath(), "sc_test_" + name.Replace('\\', '_') + ".xml");
            try
            {
                Service.ExportToFile(name, tmpFile);
                Assert.IsTrue(File.Exists(tmpFile));
                string content = File.ReadAllText(tmpFile);
                XDocument.Parse(content);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        [TestMethod]
        public void Import_AfterExport_ProducesEquivalentTaskInfo()
        {
            string origName = UniqueName("Orig");
            string restName = UniqueName("Rest");
            Service.Add(new TaskSpec
            {
                Name = origName,
                Path = TestExe,
                Arguments = "/c exit 0",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                },
                Description = "test desc"
            });

            string xml = Service.Export(origName);
            TaskInfo restored = Service.Import(xml, restName, false);

            Assert.AreEqual(TestExe, restored.Path);
            Assert.AreEqual("/c exit 0", restored.Arguments);
            Assert.AreEqual("test desc", restored.Description);
            Assert.AreEqual(1, restored.Triggers.Count);
            Assert.AreEqual(TriggerKind.Daily, restored.Triggers[0].Kind);
        }

        [TestMethod]
        public void Import_DuplicateName_NoForce_ThrowsTaskExists()
        {
            string name = UniqueName("Dup");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            try
            {
                Service.Import(xml, name, false);
                Assert.Fail("应抛 TASK_EXISTS");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.TaskExists, ex.Code);
            }
        }

        [TestMethod]
        public void Import_DuplicateName_WithForce_Overwrites()
        {
            string name = UniqueName("Force");
            Service.Add(new TaskSpec
            {
                Name = name,
                Path = TestExe,
                Arguments = "orig",
                Triggers = new System.Collections.Generic.List<TriggerSpec>
                {
                    new TriggerSpec { Kind = TriggerKind.Daily, Time = System.TimeSpan.FromHours(9) }
                }
            });

            string xml = Service.Export(name);
            // 修改 XML 中的参数再导入
            xml = xml.Replace("orig", "replaced");

            TaskInfo restored = Service.Import(xml, name, true);
            Assert.AreEqual("replaced", restored.Arguments);
        }

        [TestMethod]
        public void Import_InvalidXml_ThrowsXmlParseError()
        {
            string name = UniqueName("Bad");
            try
            {
                Service.Import("<not valid xml<<<", name, false);
                Assert.Fail("应抛 XML_PARSE_ERROR");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.XmlParseError, ex.Code);
            }
        }

        [TestMethod]
        public void ImportFromFile_NonExistentFile_ThrowsInvalidPath()
        {
            string name = UniqueName("NoFile");
            try
            {
                Service.ImportFromFile(@"C:\nonexistent_path_sc_test.xml", name, false);
                Assert.Fail("应抛 INVALID_PATH");
            }
            catch (TaskServiceException ex)
            {
                Assert.AreEqual(ErrorCode.InvalidPath, ex.Code);
            }
        }
    }
}
