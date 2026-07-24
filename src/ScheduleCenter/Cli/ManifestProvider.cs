using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ScheduleCenter.Cli
{
    /// <summary>
    /// 从嵌入资源加载 cli-manifest.json，提供 help 文本与 manifest JSON 的渲染入口。
    /// 配置内容与代码解耦：新增/修改命令只需更新 cli-manifest.json，无需改代码。
    /// </summary>
    internal static class ManifestProvider
    {
        private const string ResourceName = "ScheduleCenter.Cli.cli-manifest.json";

        private static readonly Lazy<JObject> _root = new Lazy<JObject>(LoadRoot);

        public static JObject Root => _root.Value;

        public static JObject HelpSection
        {
            get
            {
                JToken t = Root["help"];
                return t as JObject;
            }
        }

        public static JObject ManifestSection
        {
            get
            {
                JToken t = Root["manifest"];
                return t as JObject;
            }
        }

        private static JObject LoadRoot()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("嵌入资源未找到: " + ResourceName +
                        "。可用资源: " + string.Join(", ", asm.GetManifestResourceNames()));
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    return JObject.Parse(json);
                }
            }
        }

        /// <summary>
        /// 渲染人友好的帮助文本。命令列表从 manifest.commands 动态生成，避免维护两处。
        /// </summary>
        public static string RenderHelpText()
        {
            JObject help = HelpSection;
            JObject manifest = ManifestSection;
            var sb = new StringBuilder();

            if (help != null)
            {
                JToken summary = help["summary"];
                if (summary != null) sb.AppendLine(summary.ToString());
                sb.AppendLine();
                JToken usage = help["usage"];
                if (usage != null) { sb.AppendLine("用法: " + usage.ToString()); sb.AppendLine(); }
            }

            JObject commands = manifest != null ? manifest["commands"] as JObject : null;
            if (commands != null && commands.Count > 0)
            {
                sb.AppendLine("命令:");
                int maxLen = 0;
                foreach (JProperty prop in commands.Properties())
                    if (prop.Name.Length > maxLen) maxLen = prop.Name.Length;

                foreach (JProperty prop in commands.Properties())
                {
                    JObject cmd = prop.Value as JObject;
                    string desc = cmd != null && cmd["description"] != null
                        ? cmd["description"].ToString()
                        : "";
                    sb.AppendLine("  " + prop.Name.PadRight(maxLen) + "  " + desc);
                }
                sb.AppendLine();
            }

            if (help != null)
            {
                JToken hint = help["hintForAgents"];
                if (hint != null) sb.AppendLine(hint.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// 渲染完整 manifest 为缩进 JSON，供 agent 解析。
        /// </summary>
        public static string RenderManifestJson()
        {
            JObject manifest = ManifestSection;
            if (manifest == null)
                throw new InvalidOperationException("cli-manifest.json 缺少 manifest 节");
            return manifest.ToString(Formatting.Indented);
        }
    }
}
