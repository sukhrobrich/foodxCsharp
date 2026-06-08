using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace WindowsFormsApp1.services
{
    /// <summary>
    /// Har bir write operatsiyasidan keyin SyncQueue ga yozib, central ga push qiladi.
    /// </summary>
    public static class SyncQueueHelper
    {
        // EntityName konstantlari — imlo xatolarini oldini olish uchun
        public const string Orders               = "Orders";
        public const string OrderFood            = "OrderFood";
        public const string OrderPayments        = "OrderPayments";
        public const string OrderDebts           = "OrderDebts";
        public const string CancellationLogs     = "CancellationLogs";
        public const string CashTransactions     = "CashTransactions";
        public const string Customers            = "Customers";
        public const string IngredientPurchases  = "IngredientPurchases";
        public const string FoodPurchases        = "FoodPurchases";
        public const string Ingredients          = "Ingredients";
        public const string Foods                = "Foods";
        public const string FoodDelete          = "FoodDelete";
        public const string Users                = "Users";

        /// <summary>
        /// SyncQueue ga yozadi va online bo'lsa background da sync ishga tushiradi.
        /// </summary>
        public static void Add(string entityName, int entityId,
                               string actionType = "Insert")
        {
            try
            {
                using (var conn = dbconnect.OpenLocalForSync())
                using (var cmd = new SqlCommand(
                    "INSERT INTO SyncQueue(EntityName,EntityId,ActionType,IsSynced,CreatedAt)" +
                    " VALUES(@en,@eid,@at,0,GETDATE())", conn))
                {
                    cmd.Parameters.AddWithValue("@en",  entityName);
                    cmd.Parameters.AddWithValue("@eid", entityId);
                    cmd.Parameters.AddWithValue("@at",  actionType);
                    cmd.ExecuteNonQuery();
                }

                // Online bo'lsa — darhol background sync
                if (Session.IsOnline && Session.TenantId > 0)
                    Task.Run(() => SyncEngine.ProcessSyncQueue());
            }
            catch
            {
                // SyncQueue yozilmasa ham asosiy operatsiya bajarilgan —
                // mavjud is_synced mexanizmi zahira sifatida ishlaydi
            }
        }

        /// <summary>
        /// Bir nechta entity ni bir vaqtda SyncQueue ga qo'shish.
        /// Masalan: buyurtma saqlaganda Orders + OrderFood birgalikda.
        /// </summary>
        public static void AddBatch(params (string entity, int id, string action)[] items)
        {
            bool wrote = false;
            try
            {
                using (var conn = dbconnect.OpenLocalForSync())
                {
                    foreach (var (entity, id, action) in items)
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(
                                "INSERT INTO SyncQueue(EntityName,EntityId,ActionType,IsSynced,CreatedAt)" +
                                " VALUES(@en,@eid,@at,0,GETDATE())", conn))
                            {
                                cmd.Parameters.AddWithValue("@en",  entity);
                                cmd.Parameters.AddWithValue("@eid", id);
                                cmd.Parameters.AddWithValue("@at",  action);
                                cmd.ExecuteNonQuery();
                                wrote = true;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            if (wrote && Session.IsOnline && Session.TenantId > 0)
                Task.Run(() => SyncEngine.ProcessSyncQueue());
        }
    }
}
