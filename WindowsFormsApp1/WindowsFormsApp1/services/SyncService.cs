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

            // Har 1 soniyada print queue tekshiradi (mobil chek so'rovlari)
            _printTimer = new System.Threading.Timer(PrintTick, null,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1));
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

        // ── Print: mobil print so'rovlarini 1 soniyada tekshiradi ────────────
        private static bool _printBusy = false;
        private static bool _tableChecked = false;

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
                    // Jadval mavjudligini bir marta tekshirish
                    if (!_tableChecked)
                    {
                        using (var chk = new SqlCommand(
                            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='print_queue'", central))
                        {
                            if ((int)chk.ExecuteScalar() == 0) return;
                            _tableChecked = true;
                        }
                    }

                    // Pending joblarni olish
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT id, order_id, ISNULL(print_type,'receipt') AS print_type " +
                        "FROM print_queue WHERE is_printed=0 AND tenant_id=@tid " +
                        "ORDER BY requested_at",
                        central))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@tid", Session.TenantId);
                        da.Fill(dt);
                    }

                    foreach (DataRow row in dt.Rows)
                    {
                        int    jobId     = Convert.ToInt32(row["id"]);
                        int    orderId   = Convert.ToInt32(row["order_id"]);
                        string printType = row["print_type"].ToString();

                        try
                        {
                            if (printType == "kitchen")
                                PrintKitchenFromCentral(central, orderId);
                            else
                                PrintService.PrintReceipt(orderId);
                        }
                        catch { }

                        // Printed deb belgilash
                        using (var cmd = new SqlCommand(
                            "UPDATE print_queue SET is_printed=1, printed_at=GETDATE() WHERE id=@id",
                            central))
                        {
                            cmd.Parameters.AddWithValue("@id", jobId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
            finally { _printBusy = false; }
        }

        // Markaziy serverdan kitchen tickets chop etadi
        private static void PrintKitchenFromCentral(SqlConnection central, int orderId)
        {
            var dt = new DataTable();
            using (var da = new SqlDataAdapter(@"
                SELECT f.id AS food_id, f.name AS food_name,
                       of2.quantity, ISNULL(f.unit,'ta') AS unit,
                       ISNULL(fc.printer_name,'') AS printer_name,
                       ISNULL(fc.name,'') AS cat_name,
                       ISNULL(of2.note,'') AS note,
                       ISNULL(u.name,'') AS waiter_name,
                       o.created_at
                FROM order_food of2
                JOIN food f ON f.id = of2.food_id
                JOIN food_category fc ON fc.id = f.food_category_id
                JOIN [order] o ON o.id = of2.order_id
                LEFT JOIN [user] u ON u.id = o.user_id
                WHERE of2.order_id = @oid", central))
            {
                da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0) return;

            var items       = new System.Collections.Generic.List<KitchenItem>();
            string waiter   = "";
            DateTime orderTime = DateTime.Now;

            foreach (DataRow r in dt.Rows)
            {
                string printer = r["printer_name"].ToString();
                if (string.IsNullOrEmpty(printer)) continue;

                items.Add(new KitchenItem
                {
                    FoodId       = Convert.ToInt32(r["food_id"]),
                    FoodName     = r["food_name"].ToString(),
                    Quantity     = Convert.ToInt32(r["quantity"]),
                    Unit         = r["unit"].ToString(),
                    PrinterName  = printer,
                    CategoryName = r["cat_name"].ToString(),
                    Note         = r["note"].ToString()
                });

                if (string.IsNullOrEmpty(waiter)) waiter = r["waiter_name"].ToString();
                if (r["created_at"] != DBNull.Value)
                    orderTime = Convert.ToDateTime(r["created_at"]);
            }

            if (items.Count > 0)
                PrintService.PrintKitchenTickets(orderId, items, waiter, orderTime);
        }
    }
}
