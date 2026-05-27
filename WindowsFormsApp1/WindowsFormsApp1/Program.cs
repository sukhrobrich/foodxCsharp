using System;
using System.Data.SqlClient;
using System.Threading;
using System.Windows.Forms;
using WindowsFormsApp1.forms.license;
using WindowsFormsApp1.forms.settings;
using WindowsFormsApp1.forms.user;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        private static Timer      _licTimer;
        private static string     _licLogin;
        private static string     _licPass;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. DB ulanishini tekshirish
            if (!CanConnect())
            {
                using (var dlg = new ConnectionSetupForm())
                    dlg.ShowDialog();

                if (!CanConnect())
                {
                    MessageBox.Show(
                        "Serverga ulanib bo'lmadi.\nApp.config yoki connection.cfg faylini tekshiring.",
                        "FoodX — Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // 2. Litsenziya tekshirish — har safar forma ko'rsatiladi
            string login, pass;
            using (var lic = new LicenseLoginForm())
            {
                if (lic.ShowDialog() != DialogResult.OK)
                    return; // Kirish bekor qilindi — dastur yopiladi

                login = lic.SavedLogin;
                pass  = lic.SavedPassword;
            }

            // 3. Background watchdog — har 30 daqiqada litsenziyani qayta tekshiradi
            _licLogin = login;
            _licPass  = pass;
            _licTimer = new Timer(WatchdogTick, null,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(30));

            // 4. Asosiy forma
            if (IsAdminExists())
                Application.Run(new Form1());
            else
                Application.Run(new Password("admin", false));

            // 5. Dastur yopilganda timerni to'xtatish
            _licTimer?.Dispose();
        }

        // ── Watchdog: muddat tugasa asosiy formani yopadi ───────────────────
        private static void WatchdogTick(object state)
        {
            var r = LicenseService.Verify(_licLogin, _licPass);

            // Oflayn bo'lsa tekshirmaymiz
            if (r.Offline) return;

            if (!r.Valid)
            {
                _licTimer?.Dispose();

                // UI threadga o'tib xabar ko'rsatish va dasturni yopish
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (form != null && !form.IsDisposed)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(
                            "Litsenziya muddati tugadi!\n\nDastur yopiladi. To'lov qiling va administrator bilan bog'laning.",
                            "FoodX — Litsenziya", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Application.Exit();
                    }));
                }
            }
            else if (r.DaysLeft <= 3)
            {
                // 3 kun qolganda ogohlantirish
                var form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
                if (form != null && !form.IsDisposed)
                {
                    form.BeginInvoke(new Action(() =>
                        MessageBox.Show(
                            "Diqqat: litsenziya " + r.DaysLeft + " kun ichida tugaydi!\nVaqtida to'lov qiling.",
                            "FoodX — Ogohlantirish", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                }
            }
        }

        // ── Yordamchi metodlar ───────────────────────────────────────────────
        static bool CanConnect()
        {
            try { var db = new dbconnect(); db.OpenCon(); db.CloseCon(); return true; }
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
