using System;
using System.Data.SqlClient;
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
            string login, pass;
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
            }

            // 3. Session o'rnatish
            Session.TenantId = tenantId;
            Session.IsOnline = !isOffline && dbconnect.CheckCentral();

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

        static bool IsAdminExists()
        {
            var db  = new dbconnect();
            var cmd = new SqlCommand("SELECT COUNT(*) FROM [user] WHERE Name = 'admin'", db.GetCon());
            db.OpenCon();
            int n = (int)cmd.ExecuteScalar();
            db.CloseCon();
            return n > 0;
        }
    }
}
