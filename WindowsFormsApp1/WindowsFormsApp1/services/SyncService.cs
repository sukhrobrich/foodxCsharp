using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1.services
{
    internal static class SyncService
    {
        private static System.Threading.Timer _timer;
        private static System.Threading.Timer _downloadTimer;
        private static System.Threading.Timer _printTimer;

        public static void Start()
        {
            // Har 60 soniyada online/offline holatini tekshiradi va upload qiladi
            _timer = new System.Threading.Timer(Tick, null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60));

            // Har 1 soatda centraldan localga ma'lumot yuklab oladi (zahira)
            _downloadTimer = new System.Threading.Timer(DownloadTick, null,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromHours(1));

            // Har 8 soniyada print queue tekshiradi (mobil chek so'rovlari)
            _printTimer = new System.Threading.Timer(PrintTick, null,
                TimeSpan.FromSeconds(8),
                TimeSpan.FromSeconds(8));
        }

        public static void Stop()
        {
            if (_timer        != null) _timer.Dispose();
            if (_downloadTimer != null) _downloadTimer.Dispose();
            if (_printTimer   != null) _printTimer.Dispose();
        }

        // ── Upload: local → central (online bo'lganda) ────────────────────────
        private static void Tick(object state)
        {
            if (Session.ForceOffline) return;

            bool wasOnline = Session.IsOnline;
            bool nowOnline = dbconnect.CheckCentral();

            if (wasOnline == nowOnline) return;

            Session.IsOnline = nowOnline;

            Form form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (form == null || form.IsDisposed) return;

            if (nowOnline)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    SyncEngine.SyncResult upload = SyncEngine.SyncAll();
                    SyncEngine.SyncResult download = SyncEngine.DownloadAll();

                    form.BeginInvoke(new Action(delegate
                    {
                        if (upload.Errors > 0)
                            MessageBox.Show(
                                "Server bilan ulanish tiklandi, lekin sinxronizatsiyada xatolik:\n" + upload.LastError,
                                "FoodX — Sinxronizatsiya xatosi",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        else if (upload.Synced > 0)
                            MessageBox.Show(
                                "Ulanish tiklandi!\n" + upload.Synced + " ta oflayn yozuv markaziy serverga yuklandi.",
                                "FoodX — Sinxronizatsiya tugadi",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show(
                                "Server bilan ulanish tiklandi.\nDastur onlayn rejimda ishlaydi.",
                                "FoodX — Onlayn",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                });
            }
            else
            {
                form.BeginInvoke(new Action(delegate
                {
                    MessageBox.Show(
                        "Server bilan ulanish uzildi.\nDastur oflayn rejimda davom etadi.\nMa'lumotlar mahalliy bazaga yoziladi.",
                        "FoodX — Oflayn",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            }
        }

        // ── Download: central → local (har 1 soatda, zahira sifatida) ─────────
        private static void DownloadTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline) return;
            SyncEngine.DownloadAll();
        }

        // ── Print: mobil chek so'rovlarini tekshiradi va chop etadi ──────────
        private static bool _printBusy = false;
        private static void PrintTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline) return;
            if (Session.TenantId == 0) return;
            if (_printBusy) return;
            _printBusy = true;
            try
            {
                using (SqlConnection central = dbconnect.OpenCentralForSync(Session.TenantId))
                {
                    // Jadval mavjudligini tekshirish
                    using (var chk = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='print_queue'", central))
                    { if ((int)chk.ExecuteScalar() == 0) return; }

                    // Pending joblarni olish
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT id, order_id FROM print_queue WHERE is_printed=0 AND tenant_id=@tid ORDER BY requested_at",
                        central))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@tid", Session.TenantId);
                        da.Fill(dt);
                    }

                    foreach (DataRow row in dt.Rows)
                    {
                        int jobId   = Convert.ToInt32(row["id"]);
                        int orderId = Convert.ToInt32(row["order_id"]);
                        try { PrintService.PrintReceipt(orderId); } catch { }
                        using (var cmd = new SqlCommand(
                            "UPDATE print_queue SET is_printed=1, printed_at=GETDATE() WHERE id=@id", central))
                        { cmd.Parameters.AddWithValue("@id", jobId); cmd.ExecuteNonQuery(); }
                    }
                }
            }
            catch { }
            finally { _printBusy = false; }
        }
    }
}
