using System;
using System.Data.SqlClient;
using System.Windows.Forms;
using WindowsFormsApp1.forms.settings;
using WindowsFormsApp1.forms.user;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Ulanishni tekshirish; muvaffaqiyatsiz bo'lsa sozlash formasi
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

            if (IsAdminExists())
                Application.Run(new Form1());
            else
                Application.Run(new Password("admin", false));
        }

        static bool CanConnect()
        {
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                db.CloseCon();
                return true;
            }
            catch { return false; }
        }

        static bool IsAdminExists()
        {
            dbconnect db = new dbconnect();
            string query = "SELECT COUNT(*) FROM [user] WHERE Name = 'admin'";
            SqlCommand cmd = new SqlCommand(query, db.GetCon());
            db.OpenCon();
            int count = (int)cmd.ExecuteScalar();
            db.CloseCon();
            return count > 0;
        }
    }
}
