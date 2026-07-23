using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class OutputWriter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None
        };

        public static void Success(object payload)
        {
            Console.Out.WriteLine(JsonConvert.SerializeObject(payload, Settings));
        }

        public static void Error(string command, TaskServiceException ex)
        {
            Console.Error.WriteLine(JsonConvert.SerializeObject(new
            {
                success = false,
                command,
                error = new { code = ex.CodeName, message = ex.Message }
            }, Settings));
        }
    }
}
