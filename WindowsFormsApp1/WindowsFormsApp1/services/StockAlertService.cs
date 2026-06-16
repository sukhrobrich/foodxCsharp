using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;

namespace WindowsFormsApp1.services
{
    // Ombordagi ingredient miqdori min_quantity dan past/teng bo'lganda
    // bir martalik bildirishnoma (toast + ovoz) chiqarish uchun.
    internal static class StockAlertService
    {
        public class LowStockInfo
        {
            public string  Name        { get; set; }
            public string  Unit        { get; set; }
            public decimal Quantity    { get; set; }
            public decimal MinQuantity { get; set; }
        }

        public static event Action<LowStockInfo> LowStockDetected;

        static Timer _timer;
        static readonly HashSet<int> _alreadyLow = new HashSet<int>();
        static readonly object _tickLock = new object();

        public static void Start()
        {
            _timer = new Timer(Tick, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        }

        public static void Stop()
        {
            _timer?.Dispose();
        }

        static void Tick(object state)
        {
            if (!Monitor.TryEnter(_tickLock)) return;
            try
            {
                using (var con = dbconnect.OpenLocalForSync())
                using (var cmd = new SqlCommand(
                    "SELECT id,name,unit,quantity,min_quantity FROM ingredient WHERE ISNULL(min_quantity,0) > 0", con))
                using (var dr = cmd.ExecuteReader())
                {
                    var stillLow = new HashSet<int>();
                    while (dr.Read())
                    {
                        int     id  = Convert.ToInt32(dr["id"]);
                        decimal qty = Convert.ToDecimal(dr["quantity"]);
                        decimal min = Convert.ToDecimal(dr["min_quantity"]);
                        if (qty > min) continue;

                        stillLow.Add(id);
                        if (!_alreadyLow.Contains(id))
                        {
                            LowStockDetected?.Invoke(new LowStockInfo
                            {
                                Name        = dr["name"].ToString(),
                                Unit        = dr["unit"].ToString(),
                                Quantity    = qty,
                                MinQuantity = min
                            });
                        }
                    }
                    _alreadyLow.Clear();
                    foreach (int id in stillLow) _alreadyLow.Add(id);
                }
            }
            catch { } // Lokal DB hali tayyor bo'lmasligi mumkin — kritik emas
            finally { Monitor.Exit(_tickLock); }
        }
    }
}
