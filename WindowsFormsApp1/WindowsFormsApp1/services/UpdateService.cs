using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    public static class UpdateService
    {
        private const string UPDATE_BASE = "http://195.158.24.155/update";
        public  const string APP_VERSION = "1.0.0";

        // Fon threadida versiyani tekshiradi, yangi versiya bo'lsa callback chaqiriladi
        public static void CheckInBackground(Action<string> onUpdateAvailable)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string latest = Fetch(UPDATE_BASE + "/version.txt")?.Trim();
                    if (!string.IsNullOrEmpty(latest) && IsNewer(latest, APP_VERSION))
                        onUpdateAvailable?.Invoke(latest);
                }
                catch { }
            });
        }

        // Dialog chiqarib yangilash (UI threadidan chaqiriladi)
        public static void PromptAndInstall(Form parent, string latestVersion)
        {
            var res = MessageBox.Show(
                $"Yangi versiya mavjud: {latestVersion}\nHozirgi versiya: {APP_VERSION}\n\nDasturni yangilash kerakmi?",
                "FoodX — Yangilash", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (res != DialogResult.Yes) return;
            DownloadAndInstall(parent, latestVersion);
        }

        // Faqat tekshirib xabar berish (button uchun)
        public static void ManualCheck(Form parent)
        {
            string latest;
            try { latest = Fetch(UPDATE_BASE + "/version.txt")?.Trim(); }
            catch { MessageBox.Show("Server bilan ulanib bo'lmadi.", "FoodX", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (string.IsNullOrEmpty(latest))
            {
                MessageBox.Show("Versiya ma'lumoti olinmadi.", "FoodX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsNewer(latest, APP_VERSION))
            {
                MessageBox.Show($"Dastur eng yangi versiyada.\nVersiya: {APP_VERSION}", "FoodX — Yangilash", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            PromptAndInstall(parent, latest);
        }

        private static void DownloadAndInstall(Form parent, string latestVersion)
        {
            string zipUrl    = UPDATE_BASE + "/FoodXCsharp.zip";
            string zipPath   = Path.Combine(Path.GetTempPath(), "FoodXUpdate.zip");
            string exeDir    = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string exeName   = AppDomain.CurrentDomain.FriendlyName;
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                exeName += ".exe";

            // Progress form
            Form dlg = new Form
            {
                Text            = "FoodX — Yuklanmoqda",
                Size            = new Size(420, 110),
                StartPosition   = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox      = false,
                BackColor       = Color.White
            };
            Label lblProg = new Label
            {
                Text      = "Yangi versiya yuklanmoqda...",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 10)
            };
            dlg.Controls.Add(lblProg);

            bool success = false;
            Exception downloadErr = null;

            dlg.Shown += (s, e) =>
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.DownloadProgressChanged += (ws, we) =>
                        {
                            if (!dlg.IsDisposed)
                                dlg.BeginInvoke(new Action(() =>
                                    lblProg.Text = $"Yuklanmoqda... {we.ProgressPercentage}%  ({we.BytesReceived / 1024} KB)"));
                        };

                        var task = wc.DownloadFileTaskAsync(new Uri(zipUrl), zipPath);
                        while (!task.IsCompleted)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50);
                        }
                        if (task.Exception != null) throw task.Exception.InnerException ?? task.Exception;
                    }

                    lblProg.Text = "O'rnatilmoqda...";
                    dlg.Refresh();

                    // Updater bat yaratish
                    string batPath    = Path.Combine(Path.GetTempPath(), "foodx_updater.bat");
                    string extractDir = Path.Combine(Path.GetTempPath(), "FoodXExtracted");

                    using (var sw = new StreamWriter(batPath, false, System.Text.Encoding.Default))
                    {
                        sw.WriteLine("@echo off");
                        sw.WriteLine("title FoodX Yangilanmoqda...");
                        sw.WriteLine("echo Yangilash jarayoni boshlandi, iltimos kuting...");
                        sw.WriteLine("timeout /t 3 /nobreak >nul");
                        sw.WriteLine($"if exist \"{extractDir}\" rmdir /s /q \"{extractDir}\"");
                        sw.WriteLine($"mkdir \"{extractDir}\"");
                        sw.WriteLine($"powershell -Command \"Expand-Archive -Path '{zipPath}' -DestinationPath '{extractDir}' -Force\"");
                        sw.WriteLine($"xcopy /s /y /q \"{extractDir}\\*\" \"{exeDir}\\\"");
                        sw.WriteLine("echo Yangilandi! Dastur qayta ishga tushmoqda...");
                        sw.WriteLine($"start \"\" \"{Path.Combine(exeDir, exeName)}\"");
                        sw.WriteLine($"rmdir /s /q \"{extractDir}\"");
                        sw.WriteLine($"del /f \"{zipPath}\"");
                        sw.WriteLine("timeout /t 2 /nobreak >nul");
                        sw.WriteLine("del /f \"%~f0\"");
                    }

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName        = batPath,
                        WindowStyle     = System.Diagnostics.ProcessWindowStyle.Normal,
                        UseShellExecute = true
                    });

                    success = true;
                }
                catch (Exception ex) { downloadErr = ex; }
                finally
                {
                    if (!dlg.IsDisposed) dlg.BeginInvoke(new Action(() => dlg.Close()));
                }
            };

            dlg.ShowDialog(parent);

            if (downloadErr != null)
            {
                MessageBox.Show("Yangilashda xatolik:\n" + downloadErr.Message,
                    "FoodX — Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (success)
                Application.Exit();
        }

        public static bool IsNewer(string latest, string current)
        {
            try
            {
                var v1 = new Version(latest.TrimStart('v', 'V'));
                var v2 = new Version(current.TrimStart('v', 'V'));
                return v1 > v2;
            }
            catch { return false; }
        }

        private static string Fetch(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 8000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream()))
                return rd.ReadToEnd();
        }
    }
}
