using System;
using System.IO;
using System.Text;

namespace WindowsFormsApp1.services
{
    public static class MultiMonoblokConfig
    {
        private static readonly string CfgFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FoodX", "multimonoblok.cfg");

        public static bool IsMultiMonoblokMode => File.Exists(CfgFile);

        public static (string apiUrl, int tenantId) Load()
        {
            try
            {
                if (!File.Exists(CfgFile)) return ("", 0);
                var lines = File.ReadAllLines(CfgFile, Encoding.UTF8);
                string apiUrl = lines.Length > 0 ? lines[0].Trim() : "";
                int tid = lines.Length > 1 ? (int.TryParse(lines[1].Trim(), out int t) ? t : 0) : 0;
                return (apiUrl, tid);
            }
            catch { return ("", 0); }
        }

        public static void Save(string apiUrl, int tenantId)
        {
            try
            {
                string dir = Path.GetDirectoryName(CfgFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(CfgFile, new[] { apiUrl, tenantId.ToString() }, Encoding.UTF8);
            }
            catch { }
        }

        public static void Clear()
        {
            try { if (File.Exists(CfgFile)) File.Delete(CfgFile); } catch { }
        }

        // Stol ko'rinish rejimi: "horizontal" yoki "vertical"
        private static readonly string LayoutFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FoodX", "table_layout.txt");

        public static string TableLayoutMode
        {
            get
            {
                try { return File.Exists(LayoutFile) ? File.ReadAllText(LayoutFile).Trim() : "horizontal"; }
                catch { return "horizontal"; }
            }
            set
            {
                try
                {
                    string dir = Path.GetDirectoryName(LayoutFile);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(LayoutFile, value, Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
