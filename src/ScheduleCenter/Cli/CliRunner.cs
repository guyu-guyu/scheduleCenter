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
                    case "help":
                    case "h":
                    {
                        // 人友好帮助文本，输出到 stdout，退出码 0
                        Console.Out.Write(ManifestProvider.RenderHelpText());
                        Console.Out.Flush();
                        return 0;
                    }
                    case "manifest":
                    {
                        // 机器可读的完整 CLI 清单（JSON），供 agent 解析
                        Console.Out.Write(ManifestProvider.RenderManifestJson());
                        Console.Out.WriteLine();
                        Console.Out.Flush();
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
            catch (UnknownCommandException ex)
            {
                // 未知命令走简短提示路径（非 JSON），避免把完整 help 塞进 JSON message 字段
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine("可用命令: add, update, delete, get, list, enable, disable, run, history, export, import, help, manifest");
                Console.Error.WriteLine();
                Console.Error.WriteLine("提示:");
                Console.Error.WriteLine("  - 运行 'ScheduleCenter help' 查看完整帮助");
                Console.Error.WriteLine("  - 运行 'ScheduleCenter manifest' 获取机器可读的 CLI 清单 JSON");
                Console.Error.WriteLine("  - Agent 调用应优先使用 'ScheduleCenter manifest' 命令以获取完整的命令、参数及类型信息");
                Console.Error.Flush();
                return 2;
            }
            catch (Exception ex)
            {
                OutputWriter.Error(command, new TaskServiceException(ErrorCode.InternalError, ex.Message, ex));
                return 1;
            }
        }
    }
}
