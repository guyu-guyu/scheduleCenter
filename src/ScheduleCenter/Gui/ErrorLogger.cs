using System;
using System.IO;

namespace ScheduleCenter.Gui
{
    public static class ErrorLogger
    {
        public static void Log(Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScheduleCenter");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
