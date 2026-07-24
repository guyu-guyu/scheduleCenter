using System;
using System.Linq;
using ScheduleCenter.Core;

namespace ScheduleCenter.Cli
{
    public static class CliRunner
    {
        public static int Run(string[] args)
        {
            string command = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
            try
            {
                ParsedArgs parsed = CliParser.Parse(args);
                command = parsed.Command;
                var service = new ScheduledTaskService();

                switch (parsed.Command)
                {
                    case "add":
                    {
                        TaskInfo added = service.Add(SpecBuilder.BuildSpec(parsed));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(added) });
                        return 0;
                    }
                    case "update":
                    {
                        TaskInfo updated = service.Update(SpecBuilder.BuildUpdate(parsed));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(updated) });
                        return 0;
                    }
                    case "delete":
                    {
                        string name = parsed.Require("name");
                        service.Delete(name, parsed.Has("force"));
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "get":
                    {
                        TaskInfo info = service.Get(parsed.Require("name"));
                        OutputWriter.Success(new { success = true, command, task = TaskDto.From(info) });
                        return 0;
                    }
                    case "list":
                    {
                        var tasks = service.List(parsed.Get("filter"));
                        OutputWriter.Success(new { success = true, command, tasks = tasks.Select(TaskDto.From).ToList() });
                        return 0;
                    }
                    case "enable":
                    case "disable":
                    {
                        string name = parsed.Require("name");
                        service.SetEnabled(name, parsed.Command == "enable");
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "run":
                    {
                        string name = parsed.Require("name");
                        service.Run(name);
                        OutputWriter.Success(new { success = true, command, name });
                        return 0;
                    }
                    case "history":
                    {
                        string name = parsed.Require("name");
                        int? last = null;
                        string lastStr = parsed.Get("last");
                        if (lastStr != null)
                        {
                            int n;
                            if (!int.TryParse(lastStr, out n) || n <= 0)
                                throw new TaskServiceException(ErrorCode.InvalidArguments, "last 格式错误 '" + lastStr + "'");
                            last = n;
                        }
                        var events = service.GetHistory(name, last, parsed.Has("errors-only"));
                        OutputWriter.Success(new
                        {
                            success = true,
                            command,
                            name,
                            events = events.Select(e => new
                            {
                                time = e.Time,
                                type = e.Type,
                                resultCode = e.ResultCode,
                                message = e.Message
                            }).ToList()
                        });
                        return 0;
                    }
                    case "export":
                    {
                        string name = parsed.Require("name");
                        string outputPath = parsed.Get("output");
                        if (outputPath == null)
                        {
                            string xml = service.Export(name);
                            Console.Out.Write(xml);
                            Console.Out.Flush();
                        }
                        else
                        {
                            service.ExportToFile(name, outputPath);
                            OutputWriter.Success(new { success = true, command, name, path = outputPath });
                        }
                        return 0;
                    }
                    case "import":
                    {
                        string file = parsed.Require("file");
                        string name = parsed.Require("name");
                        bool force = parsed.Has("force");
                        TaskInfo imported = service.ImportFromFile(file, name, force);
                        OutputWriter.Success(new { success = true, command, name, task = TaskDto.From(imported) });
                        return 0;
                    }
                    default:
                        throw new TaskServiceException(ErrorCode.InvalidArguments, CliParser.Usage());
                }
            }
            catch (TaskServiceException ex)
            {
                OutputWriter.Error(command, ex);
                return ex.ExitCode;
            }
            catch (Exception ex)
            {
                OutputWriter.Error(command, new TaskServiceException(ErrorCode.InternalError, ex.Message, ex));
                return 1;
            }
        }
    }
}
