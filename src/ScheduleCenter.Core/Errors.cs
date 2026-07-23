using System;

namespace ScheduleCenter.Core
{
    public enum ErrorCode
    {
        InvalidArguments,
        ConfirmRequired,
        TaskNotFound,
        TaskExists,
        AccessDenied,
        HistoryDisabled,
        InvalidPath,
        InternalError
    }

    public sealed class TaskServiceException : Exception
    {
        public ErrorCode Code { get; private set; }

        public TaskServiceException(ErrorCode code, string message, Exception inner = null)
            : base(message, inner)
        {
            Code = code;
        }

        public int ExitCode
        {
            get
            {
                switch (Code)
                {
                    case ErrorCode.InvalidArguments:
                    case ErrorCode.ConfirmRequired:
                    case ErrorCode.InvalidPath:
                        return 2;
                    case ErrorCode.AccessDenied:
                        return 3;
                    case ErrorCode.TaskNotFound:
                        return 4;
                    case ErrorCode.TaskExists:
                        return 5;
                    default:
                        return 1;
                }
            }
        }

        public string CodeName
        {
            get
            {
                switch (Code)
                {
                    case ErrorCode.InvalidArguments: return "INVALID_ARGUMENTS";
                    case ErrorCode.ConfirmRequired: return "CONFIRM_REQUIRED";
                    case ErrorCode.TaskNotFound: return "TASK_NOT_FOUND";
                    case ErrorCode.TaskExists: return "TASK_EXISTS";
                    case ErrorCode.AccessDenied: return "ACCESS_DENIED";
                    case ErrorCode.HistoryDisabled: return "HISTORY_DISABLED";
                    case ErrorCode.InvalidPath: return "INVALID_PATH";
                    default: return "INTERNAL_ERROR";
                }
            }
        }
    }
}
