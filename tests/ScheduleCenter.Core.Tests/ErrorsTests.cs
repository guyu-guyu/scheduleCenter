using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScheduleCenter.Core;

namespace ScheduleCenter.Core.Tests
{
    [TestClass]
    public class ErrorsTests
    {
        [TestMethod]
        public void ExitCode_MapsCorrectly()
        {
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidArguments, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.ConfirmRequired, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidPath, "x").ExitCode);
            Assert.AreEqual(3, new TaskServiceException(ErrorCode.AccessDenied, "x").ExitCode);
            Assert.AreEqual(4, new TaskServiceException(ErrorCode.TaskNotFound, "x").ExitCode);
            Assert.AreEqual(5, new TaskServiceException(ErrorCode.TaskExists, "x").ExitCode);
            Assert.AreEqual(1, new TaskServiceException(ErrorCode.HistoryDisabled, "x").ExitCode);
            Assert.AreEqual(1, new TaskServiceException(ErrorCode.InternalError, "x").ExitCode);
        }

        [TestMethod]
        public void CodeName_MatchesCliContract()
        {
            Assert.AreEqual("INVALID_ARGUMENTS", new TaskServiceException(ErrorCode.InvalidArguments, "x").CodeName);
            Assert.AreEqual("CONFIRM_REQUIRED", new TaskServiceException(ErrorCode.ConfirmRequired, "x").CodeName);
            Assert.AreEqual("TASK_NOT_FOUND", new TaskServiceException(ErrorCode.TaskNotFound, "x").CodeName);
            Assert.AreEqual("TASK_EXISTS", new TaskServiceException(ErrorCode.TaskExists, "x").CodeName);
            Assert.AreEqual("ACCESS_DENIED", new TaskServiceException(ErrorCode.AccessDenied, "x").CodeName);
            Assert.AreEqual("HISTORY_DISABLED", new TaskServiceException(ErrorCode.HistoryDisabled, "x").CodeName);
            Assert.AreEqual("INVALID_PATH", new TaskServiceException(ErrorCode.InvalidPath, "x").CodeName);
            Assert.AreEqual("INTERNAL_ERROR", new TaskServiceException(ErrorCode.InternalError, "x").CodeName);
        }

        [TestMethod]
        public void V2_ExitCode_MapsCorrectly()
        {
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidTriggerFormat, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.InvalidEventSubscription, "x").ExitCode);
            Assert.AreEqual(2, new TaskServiceException(ErrorCode.XmlParseError, "x").ExitCode);
        }

        [TestMethod]
        public void V2_CodeName_MatchesCliContract()
        {
            Assert.AreEqual("INVALID_TRIGGER_FORMAT", new TaskServiceException(ErrorCode.InvalidTriggerFormat, "x").CodeName);
            Assert.AreEqual("INVALID_EVENT_SUBSCRIPTION", new TaskServiceException(ErrorCode.InvalidEventSubscription, "x").CodeName);
            Assert.AreEqual("XML_PARSE_ERROR", new TaskServiceException(ErrorCode.XmlParseError, "x").CodeName);
        }
    }
}
