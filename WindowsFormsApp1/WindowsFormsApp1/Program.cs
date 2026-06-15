using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.forms.license;
using WindowsFormsApp1.forms.settings;
using WindowsFormsApp1.forms.user;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        private static System.Threading.Timer _licTimer;
        private static string                _licLogin;
        private static string                _licPass;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Ulanish holatini aniqlash (central → local fallback)
            Session.IsOnline = dbconnect.CheckCentral();

            if (!Session.IsOnline)
            {
                // Central yo'q — lokal DB bormi?
                bool hasLocal = CanConnectLocal();
                if (!hasLocal)
                {
                    // Ikkalasida yo'q — connection setup ko'rsatamiz
                    using (var dlg = new ConnectionSetupForm())
                        dlg.ShowDialog();

                    Session.IsOnline = dbconnect.CheckCentral();
                    if (!Session.IsOnline && !CanConnectLocal())
                    {
                        MessageBox.Show(
                            "Na server, na mahalliy baza bilan ulanib bo'lmadi.\nApp.config faylini tekshiring.",
                            "FoodX — Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            // 2. Litsenziya tekshirish
            string login, pass, cafeName, expiresAt;
            int    tenantId;
            bool   isOffline;

            using (var lic = new LicenseLoginForm())
            {
                if (lic.ShowDialog() != DialogResult.OK)
                    return;

                login     = lic.SavedLogin;
                pass      = lic.SavedPassword;
                tenantId  = lic.SavedTenantId;
                isOffline = lic.SavedIsOffline;
                cafeName  = lic.SavedCafeName;
                expiresAt = lic.SavedExpiresAt;
            }

            // 3. Session o'rnatish
            Session.TenantId = tenantId;
            Session.IsOnline = !isOffline && dbconnect.CheckCentral();

            // 3a. Faqat oflayn holatda saqlangan rejimni tiklaymiz
            if (!Session.IsOnline)
                ApplySavedConnectionMode();

            // 3b. Mahalliy bazani tekshirib, zarur bo'lsa yaratamiz
            if (!dbconnect.EnsureLocalDatabase(out string dbError))
            {
                MessageBox.Show(
                    "Mahalliy baza yaratilmadi.\n\n" + dbError,
                    "FoodX — Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _licTimer?.Dispose();
                return;
            }

            // 3b+. Lokal license jadvalini haqiqiy tenant_id bilan yangilash
            // Oflayn kirish (staff-list) to'g'ri tenant topishi uchun zarur
            dbconnect.UpdateLocalLicense(tenantId, cafeName, expiresAt, login);

            // 3c. Baza bo'sh bo'lsa (yangi yoki o'chirilgan) — centraldan yuklaymiz
            if (Session.IsOnline && Session.TenantId > 0 && IsLocalDbEmpty())
            {
                RestoreFromCentral();
            }

            // 4. Watchdog — har 30 daqiqada litsenziyani qayta tekshiradi
            _licLogin = login;
            _licPass  = pass;
            _licTimer = new System.Threading.Timer(WatchdogTick, null,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(30));

            // 5. Online↔offline avtomatik monitor
            SyncService.Start();

            // 6. Asosiy forma
            if (IsAdminExists())
                Application.Run(new Form1());
            else
                Application.Run(new Password("admin", false));

            // 7. Dastur yopilganda tozalash
            _licTimer?.Dispose();
            SyncService.Stop();
        }

        // ── Watchdog ────────────────────────────────────────────────────────
        private static void WatchdogTick(object state)
        {
            var r = LicenseService.Verify(_licLogin, _licPass);
            if (r.Offline) return;

            if (!r.Valid)
            {
                _licTimer?.Dispose();
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (form != null && !form.IsDisposed)
                    form.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            "Litsenziya muddati tugadi!\n\nDastur yopiladi. To'lov qiling va administrator bilan bog'laning.",
                            "FoodX — Litsenziya", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Application.Exit();
                    }));
            }
            else if (r.DaysLeft <= 3)
            {
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (form != null && !form.IsDisposed)
                    form.BeginInvoke(new Action(() =>
                        MessageBox.Show(
                            "Diqqat: litsenziya " + r.DaysLeft + " kun ichida tugaydi!\nVaqtida to'lov qiling.",
                            "FoodX — Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }

        // ── Yordamchi metodlar ───────────────────────────────────────────────

        // Markaziy serverdan barcha ma'lumotlarni local bazaga yuklab oladi
        static void RestoreFromCentral()
        {
            Form dlg = new Form
            {
                Text            = "FoodX — Tiklash",
                Size            = new Size(400, 140),
                StartPosition   = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox      = false,
                BackColor       = Color.White
            };

            Label lbl = new Label
            {
                Text      = "⏳  Markaziy serverdan ma'lumotlar yuklanmoqda...",
                AutoSize  = false,
                Dock      = DockStyle.Top,
                Height    = 56,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 10)
            };
            Label lbl2 = new Label
            {
                Text      = "Iltimos kuting. Bu bir necha soniya oladi.",
                AutoSize  = false,
                Dock      = DockStyle.Top,
                Height    = 28,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            dlg.Controls.Add(lbl);
            dlg.Controls.Add(lbl2);
            dlg.Show();
            Application.DoEvents();

            try
            {
                var result = SyncEngine.DownloadAll();

                if (result.Errors > 0 && result.Synced == 0)
                {
                    // Hech narsa yuklanmadi — xatolikni ko'rsatamiz
                    lbl.Text = "⚠  Yuklanishda xatolik: " + result.LastError;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(2000);
                }
                else
                {
                    lbl.Text  = "✓  Tiklash yakunlandi!";
                    lbl2.Text = result.Synced + " ta yozuv yuklandi.";
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(600);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ma'lumotlar yuklanishida xatolik:\n" + ex.Message +
                    "\n\nDastur bo'sh baza bilan ishga tushadi.",
                    "FoodX — Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                dlg.Close();
                dlg.Dispose();
            }
        }

        // Lokal bazadan saqlangan connection_mode ni o'qib Session ga qo'llaydi.
        // Faqat local DB mavjud bo'lsa ishlaydi; yo'q bo'lsa hech narsa qilmaydi.
        static void ApplySavedConnectionMode()
        {
            try
            {
                if (!dbconnect.CheckLocal()) return;

                bool prevOnline = Session.IsOnline;
                Session.IsOnline = false;            // lokal bazani o'qish uchun
                string mode = "";
                try { mode = PrintService.GetSetting("connection_mode", ""); }
                finally { Session.IsOnline = prevOnline; }

                if (mode == "offline")
                {
                    Session.IsOnline     = false;
                    Session.ForceOffline = true;
                }
                // "online" yoki bo'sh bo'lsa — CheckCentral natijasi saqlanib qoladi
            }
            catch { }
        }

        static bool CanConnectLocal()
        {
            try
            {
                bool prev = Session.IsOnline;
                Session.IsOnline = false;
                var db = new dbconnect();
                db.OpenCon();
                db.CloseCon();
                Session.IsOnline = prev;
                return true;
            }
            catch { return false; }
        }

        // Lokal [user] jadvali bo'shligini tekshiradi (baza yangi yoki o'chirilgan)
        static bool IsLocalDbEmpty()
        {
            bool prev = Session.IsOnline;
            Session.IsOnline = false;
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                int n;
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM [user]", db.GetCon()))
                    n = (int)cmd.ExecuteScalar();
                db.CloseCon();
                return n == 0;
            }
            catch { return true; }
            finally { Session.IsOnline = prev; }
        }

        // Lokal bazada kamida bitta foydalanuvchi borligini tekshiradi.
        // Session.IsOnline vaqtincha false — local DB dan to'g'ri o'qish uchun.
        static bool IsAdminExists()
        {
            bool prevOnline = Session.IsOnline;
            Session.IsOnline = false;
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                int n = 0;
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM [user]", db.GetCon()))
                    n = (int)cmd.ExecuteScalar();
                db.CloseCon();
                return n > 0;
            }
            catch { return false; }
            finally { Session.IsOnline = prevOnline; }
        }
    }
}
