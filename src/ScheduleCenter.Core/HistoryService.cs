using System;
using System.Collections.Generic;

namespace ScheduleCenter.Core
{
    public sealed class HistoryService
    {
        public IReadOnlyList<HistoryEvent> GetHistory(string taskFullPath, int? last, bool errorsOnly)
        {
            throw new NotImplementedException();
        }

        public bool IsLogEnabled()
        {
            throw new NotImplementedException();
        }
    }
}
