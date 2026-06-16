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
        private static System.Threading.Timer _orderSyncTimer;
        private static System.Threading.Timer _orderDownloadTimer; // mobil zakaslami olish
        private static System.Threading.Timer _fullSyncTimer;
        private static System.Threading.Timer _downloadTimer;
        private static System.Threading.Timer _printTimer;
        private static System.Threading.Timer _localPrintTimer;

        public static void Start()
        {
            // Online/offline holat o'zgarishini kuzatish — har 30 soniyada
            _timer = new System.Threading.Timer(Tick, null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            // Buyurtma o'zgarishlari (SyncQueue) — har 5 soniyada
            _orderSyncTimer = new System.Threading.Timer(OrderSyncTick, null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            // Mobil ilovadan kelgan zakaslami yuklab olish — har 1 soniyada
            _orderDownloadTimer = new System.Threading.Timer(OrderDownloadTick, null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));

            // To'liq sinxronizatsiya (taomlar, sozlamalar va boshqalar) — har 2 daqiqada
            _fullSyncTimer = new System.Threading.Timer(FullSyncTick, null,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(2));

            // Markaziy serverdan localga yuklash — har 5 daqiqada
            _downloadTimer = new System.Threading.Timer(DownloadTick, null,
                TimeSpan.FromMinutes(2),
                TimeSpan.FromMinutes(5));

            // Mobil print so'rovlari — har 1 soniyada (cloud)
            _printTimer = new System.Threading.Timer(PrintTick, null,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1));

            // Lokal baza print so'rovlari — har 1 soniyada (offline rejim uchun)
            _localPrintTimer = new System.Threading.Timer(LocalPrintTick, null,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(1));

            // Kam qolgan ingredientlarni kuzatish — har 20 soniyada
            StockAlertService.Start();
        }

        public static void Stop()
        {
            if (_timer               != null) _timer.Dispose();
            if (_orderSyncTimer      != null) _orderSyncTimer.Dispose();
            if (_orderDownloadTimer  != null) _orderDownloadTimer.Dispose();
            if (_fullSyncTimer       != null) _fullSyncTimer.Dispose();
            if (_downloadTimer       != null) _downloadTimer.Dispose();
            if (_printTimer          != null) _printTimer.Dispose();
            if (_localPrintTimer     != null) _localPrintTimer.Dispose();
            StockAlertService.Stop();
        }

        // ── Upload: local → central ───────────────────────────────────────────
        private static bool _syncBusy = false;

        private static void Tick(object state)
        {
            if (Session.ForceOffline) return;

            bool wasOnline = Session.IsOnline;
            bool nowOnline = dbconnect.CheckCentral();
            Session.IsOnline = nowOnline;

            Form form = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

            if (wasOnline != nowOnline)
            {
                // ── Holat o'zgardi ────────────────────────────────────────
                if (nowOnline)
                {
                    // OFFLINE → ONLINE: to'liq sync + SyncQueue
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        if (_syncBusy) return;
                        _syncBusy = true;
                        try
                        {
                            SyncEngine.SyncResult upload   = SyncEngine.SyncAll();
                            SyncEngine.SyncResult queue    = SyncEngine.ProcessSyncQueue();
                            SyncEngine.SyncResult download = SyncEngine.DownloadAll();

                            if (form == null || form.IsDisposed) return;
                            form.BeginInvoke(new Action(delegate
                            {
                                var msgs = new System.Text.StringBuilder();
                                bool hasError = false;

                                if (upload.Errors > 0 || queue.Errors > 0)
                                {
                                    string err = upload.LastError ?? queue.LastError;
                                    msgs.AppendLine("⬆ Yuklashda xatolik: " + err);
                                    hasError = true;
                                }
                                else
                                {
                                    int synced = upload.Synced + queue.Synced;
                                    if (synced > 0)
                                        msgs.AppendLine("⬆ " + synced + " ta oflayn yozuv yuklandi.");
                                }

                                if (download.Errors > 0)
                                {
                                    msgs.AppendLine("⬇ Yuklab olishda xatolik: " + download.LastError);
                                    hasError = true;
                                }
                                else if (download.Synced > 0)
                                    msgs.AppendLine("⬇ " + download.Synced + " ta yozuv local bazaga tushdi.");

                                if (hasError)
                                    MessageBox.Show(msgs.ToString().Trim(),
                                        "FoodX — Sinxronizatsiya xatosi",
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                else if (msgs.Length > 0)
                                    MessageBox.Show("Ulanish tiklandi!\n" + msgs.ToString().Trim(),
                                        "FoodX — Sinxronizatsiya tugadi",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                                else
                                    MessageBox.Show(
                                        "Server bilan ulanish tiklandi.\nDastur onlayn rejimda ishlaydi.",
                                        "FoodX — Onlayn",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        }
                        finally { _syncBusy = false; }
                    });
                }
                else
                {
                    // ONLINE → OFFLINE
                    if (form != null && !form.IsDisposed)
                        form.BeginInvoke(new Action(delegate
                        {
                            MessageBox.Show(
                                "Server bilan ulanish uzildi.\nDastur oflayn rejimda davom etadi.\nMa'lumotlar mahalliy bazaga yoziladi.",
                                "FoodX — Oflayn",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }));
                }
            }
        }

        // ── Buyurtmalar: har 5 soniyada SyncQueue ni tekshiradi ─────────────────
        private static void OrderSyncTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline || Session.TenantId == 0) return;
            if (_syncBusy) return;
            // ProcessSyncQueue ichida _syncLock bor — concurrent call o'zi skip qiladi
            ThreadPool.QueueUserWorkItem(delegate { SyncEngine.ProcessSyncQueue(); });
        }

        // ── To'liq sync: har 2 daqiqada taomlar, sozlamalar va boshqalar ────────
        private static void FullSyncTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline || Session.TenantId == 0) return;
            if (_syncBusy) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (_syncBusy) return;
                _syncBusy = true;
                try { SyncEngine.SyncAll(); }
                finally { _syncBusy = false; }
            });
        }

        // ── Tezkor download: mobil zakaslami 3 soniyada bir oladi ────────────
        private static bool _orderDlBusy = false;

        private static void OrderDownloadTick(object state)
        {
            if (!Session.IsOnline || Session.ForceOffline || Session.TenantId == 0) return;
            if (_orderDlBusy) return; // _syncBusy ni tekshirmaymiz — buyurtma download alohida, tezkor
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (_orderDlBusy) return;
                _orderDlBusy = true;
                try { SyncEngine.DownloadOrdersFast(); }
                finally { _orderDlBusy = false; }
            });
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

        // Heartbeat: oxirgi marta central serverga ulanganligi (har 30 soniyada bir marta)
        private static DateTime _lastHeartbeat = DateTime.MinValue;

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
                    // Heartbeat — har 10 soniyada settings ga yozamiz
                    if ((DateTime.Now - _lastHeartbeat).TotalSeconds >= 10)
                    {
                        try
                        {
                            using (var hCmd = new SqlCommand(@"
                                IF EXISTS (SELECT 1 FROM settings WHERE [key]='last_heartbeat' AND tenant_id=@tid)
                                    UPDATE settings SET value=@v WHERE [key]='last_heartbeat' AND tenant_id=@tid
                                ELSE
                                    INSERT INTO settings([key],[value],tenant_id) VALUES('last_heartbeat',@v,@tid)",
                                central))
                            {
                                hCmd.Parameters.AddWithValue("@tid", Session.TenantId);
                                hCmd.Parameters.AddWithValue("@v",
                                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
                                hCmd.ExecuteNonQuery();
                            }
                            _lastHeartbeat = DateTime.Now;
                        }
                        catch { }
                    }

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
                                PrintKitchenFromCentral(central, orderId, jobId);
                            else if (printType == "kitchen_cancel")
                                PrintKitchenCancelFromCentral(central, jobId, orderId);
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

        // Markaziy serverdan bekor qilish chiptasini chop etadi
        private static void PrintKitchenCancelFromCentral(SqlConnection central, int queueId, int orderId)
        {
            var dt = new DataTable();
            using (var da = new SqlDataAdapter(
                "SELECT food_id, food_name, quantity, unit, printer_name, cat_name " +
                "FROM print_queue_items WHERE queue_id=@qid", central))
            {
                da.SelectCommand.Parameters.AddWithValue("@qid", queueId);
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0) return;

            var items = new System.Collections.Generic.List<KitchenItem>();
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
                    Note         = ""
                });
            }

            if (items.Count > 0)
                PrintService.PrintKitchenCancellation(orderId, items, "", DateTime.Now);
        }

        // ── Lokal baza print so'rovlarini 1 soniyada tekshiradi (offline rejim) ──
        private static bool _localPrintBusy = false;

        private static void LocalPrintTick(object state)
        {
            if (_localPrintBusy) return;
            _localPrintBusy = true;
            try
            {
                using (SqlConnection local = dbconnect.OpenLocalForSync())
                {
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT id, order_id, ISNULL(print_type,'kitchen') AS print_type " +
                        "FROM print_queue WHERE is_printed=0 ORDER BY requested_at",
                        local))
                    {
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
                                PrintKitchenFromLocal(local, orderId, jobId);
                            else if (printType == "kitchen_cancel")
                                PrintKitchenCancelFromLocal(local, jobId, orderId);
                        }
                        catch { }

                        using (var cmd = new SqlCommand(
                            "UPDATE print_queue SET is_printed=1, printed_at=GETDATE() WHERE id=@id",
                            local))
                        {
                            cmd.Parameters.AddWithValue("@id", jobId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
            finally { _localPrintBusy = false; }
        }

        private static void PrintKitchenFromLocal(SqlConnection local, int orderId, int queueId)
        {
            bool hasSpecific = false;
            using (var chk = new SqlCommand(
                "SELECT COUNT(*) FROM print_queue_items WHERE queue_id=@qid", local))
            {
                chk.Parameters.AddWithValue("@qid", queueId);
                hasSpecific = Convert.ToInt32(chk.ExecuteScalar()) > 0;
            }

            var dt = new DataTable();
            if (hasSpecific)
            {
                using (var da = new SqlDataAdapter(@"
                    SELECT pqi.food_id, pqi.food_name,
                           pqi.quantity, ISNULL(pqi.unit,'ta') AS unit,
                           ISNULL(pqi.printer_name,'') AS printer_name,
                           ISNULL(pqi.cat_name,'') AS cat_name,
                           '' AS note,
                           ISNULL(u.name,'') AS waiter_name,
                           o.created_at
                    FROM print_queue_items pqi
                    JOIN [order] o ON o.id = @oid
                    LEFT JOIN [user] u ON u.id = o.user_id
                    WHERE pqi.queue_id = @qid", local))
                {
                    da.SelectCommand.Parameters.AddWithValue("@qid", queueId);
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(dt);
                }
            }
            else
            {
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
                    WHERE of2.order_id = @oid", local))
                {
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(dt);
                }
            }

            if (dt.Rows.Count == 0) return;

            var items      = new System.Collections.Generic.List<KitchenItem>();
            string waiter  = "";
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

        private static void PrintKitchenCancelFromLocal(SqlConnection local, int queueId, int orderId)
        {
            var dt = new DataTable();
            using (var da = new SqlDataAdapter(
                "SELECT food_id, food_name, quantity, unit, printer_name, cat_name " +
                "FROM print_queue_items WHERE queue_id=@qid", local))
            {
                da.SelectCommand.Parameters.AddWithValue("@qid", queueId);
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0) return;

            var items = new System.Collections.Generic.List<KitchenItem>();
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
                    Note         = ""
                });
            }

            if (items.Count > 0)
                PrintService.PrintKitchenCancellation(orderId, items, "", DateTime.Now);
        }

        // Markaziy serverdan kitchen tickets chop etadi
        private static void PrintKitchenFromCentral(SqlConnection central, int orderId, int queueId)
        {
            // Agar print_queue_items da bu job uchun itemlar bo'lsa — faqat o'shalarni chiqarish
            // (buyurtma yangilanganida faqat yangi qo'shilgan taomlar)
            bool hasSpecific = false;
            using (var chk = new SqlCommand(
                "SELECT COUNT(*) FROM print_queue_items WHERE queue_id=@qid", central))
            {
                chk.Parameters.AddWithValue("@qid", queueId);
                hasSpecific = Convert.ToInt32(chk.ExecuteScalar()) > 0;
            }

            var dt = new DataTable();
            if (hasSpecific)
            {
                // Faqat yangi qo'shilgan itemlar
                using (var da = new SqlDataAdapter(@"
                    SELECT pqi.food_id, pqi.food_name,
                           pqi.quantity, ISNULL(pqi.unit,'ta') AS unit,
                           ISNULL(pqi.printer_name,'') AS printer_name,
                           ISNULL(pqi.cat_name,'') AS cat_name,
                           '' AS note,
                           ISNULL(u.name,'') AS waiter_name,
                           o.created_at
                    FROM print_queue_items pqi
                    JOIN [order] o ON o.id = @oid
                    LEFT JOIN [user] u ON u.id = o.user_id
                    WHERE pqi.queue_id = @qid", central))
                {
                    da.SelectCommand.Parameters.AddWithValue("@qid", queueId);
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(dt);
                }
            }
            else
            {
                // Yangi buyurtma — barcha itemlarni chiqarish
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
