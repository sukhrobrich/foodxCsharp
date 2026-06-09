using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace WindowsFormsApp1.services
{
    internal static class SyncEngine
    {
        private static readonly object _syncLock = new object();

        public class SyncResult
        {
            public int    Synced    { get; set; }
            public int    Errors    { get; set; }
            public string LastError { get; set; }
        }

        // Lokal is_synced=1 bo'lgan yozuvlarni 0 ga qaytarib, keyin yuklaydi
        public static SyncResult ForceUploadAll()
        {
            var result = new SyncResult();
            if (!Session.IsOnline) { result.Errors++; result.LastError = "Oflayn rejimda bo'lmaydi."; return result; }
            if (Session.TenantId == 0) { result.Errors++; result.LastError = "TenantId=0."; return result; }

            SqlConnection local = null;
            try
            {
                local = dbconnect.OpenLocalForSync();
                string[] resets = {
                    "UPDATE customer               SET is_synced=0, central_id=NULL",
                    "UPDATE [order]                SET is_synced=0, central_id=NULL",
                    "UPDATE order_food              SET is_synced=0",
                    "UPDATE order_payments          SET is_synced=0",
                    "UPDATE order_debt              SET is_synced=0",
                    "UPDATE order_cancellation_log  SET is_synced=0",
                    "UPDATE cash_transaction        SET is_synced=0",
                    "UPDATE ingredient_purchase     SET is_synced=0",
                    "UPDATE food_purchase           SET is_synced=0",
                    // FIX: synced_qty ni 0 ga reset — to'liq qayta yuklash uchun
                    "UPDATE ingredient SET synced_qty=0 WHERE synced_qty IS NOT NULL",
                };
                foreach (string sql in resets)
                    using (var cmd = new SqlCommand(sql, local)) cmd.ExecuteNonQuery();
            }
            catch (Exception ex) { result.Errors++; result.LastError = "Reset xatosi: " + ex.Message; return result; }
            finally { if (local != null) { local.Close(); local.Dispose(); } }

            return SyncAll();
        }

        public static SyncResult SyncAll()
        {
            var result = new SyncResult();
            if (!Session.IsOnline)
            {
                result.Errors++;
                result.LastError = "Oflayn rejimda sinxronlash mumkin emas.";
                return result;
            }
            if (Session.TenantId == 0)
            {
                result.Errors++;
                result.LastError = "TenantId = 0. Dasturni qayta ishga tushirib litsenziya bilan kiring.";
                return result;
            }

            // Har sync/download oldidan lokal sxemani yangilash
            dbconnect.FixLocalDefaults();

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                // ── 1. Referens jadvallar (FK tartibida) ──────────────────────
                TryUl(() => SyncRefUserCategories(local, central),    result);
                TryUl(() => SyncRefUsers(local, central),             result);
                TryUl(() => SyncRefFoodCategories(local, central),    result);
                TryUl(() => SyncRefFoods(local, central),             result);
                TryUl(() => SyncRefPlaceCategories(local, central),   result);
                TryUl(() => SyncRefPlaceOuts(local, central),         result);
                TryUl(() => SyncRefPlaceIns(local, central),          result);
                TryUl(() => SyncRefPayments(local, central),          result);
                TryUl(() => SyncRefIngredients(local, central),       result);
                TryUl(() => SyncRefRecipeIngredients(local, central), result);

                // ── 2. Tranzaksiya jadvallari ─────────────────────────────────
                TryUl(() => SyncIngredientPurchases(local, central),  result);
                TryUl(() => SyncFoodPurchases(local, central),        result);
                TryUl(() => SyncCustomers(local, central),            result);
                TryUl(() => SyncOrders(local, central),               result);
                TryUl(() => SyncOrderFoods(local, central),           result);
                TryUl(() => SyncOrderPayments(local, central),        result);
                TryUl(() => SyncOrderDebts(local, central),           result);
                TryUl(() => SyncCancellationLogs(local, central),     result);
                TryUl(() => SyncCashTransactions(local, central),     result);
                TryUl(() => SyncIngredientQuantities(local, central), result);
                // FIX: Oflayn o'zgartirilgan sozlamalarni central ga yuklash
                TryUl(() => SyncSettings(local, central),             result);
                // Web/mobil dan qo'shilgan kassa yozuvlarini yuklab olish
                TryDl(() => DlCashTransactions(local, central),       result);
                // Web dan taom/kategoriya o'zgarishlarini yuklab olish (create/update/delete)
                TryDl(() => DlFoodCategories(local, central),         result);
                TryDl(() => DlFoodsWithDelete(local, central),        result);
            }
            catch (Exception ex)
            {
                result.LastError = ex.Message;
                result.Errors++;
            }
            finally
            {
                if (local   != null) { local.Close();   local.Dispose(); }
                if (central != null) { central.Close(); central.Dispose(); }
            }
            return result;
        }

        private static void TryUl(Func<int> action, SyncResult result)
        {
            try { result.Synced += action(); }
            catch (Exception ex) { result.Errors++; if (result.LastError == null) result.LastError = ex.Message; }
        }

        // Sync token borligini ta'minlaydi, yo'q bo'lsa yaratadi va lokal DB ga saqlaydi
        private static Guid EnsureToken(SqlConnection local, string table, DataRow r)
        {
            if (r["sync_token"] != DBNull.Value && r["sync_token"] is Guid g && g != Guid.Empty)
                return g;
            var tok = Guid.NewGuid();
            try { Exec(local, $"UPDATE {table} SET sync_token=@t WHERE id=@id", P("@t", tok), P("@id", r["id"])); }
            catch { }
            return tok;
        }

        // ── REFERENS JADVALLAR: local → central (token-based, ID konfliktsiz) ──

        // ── TOKEN-BASED SYNC: IDENTITY_INSERT o'rniga sync_token + central_id ──────
        // Sabab: har bir kafe local DB da id=1 dan boshlaydi, central da ID konflikt chiqardi.
        // Endi har bir row ning sync_token (UUID) bilan topamiz, mavjud bo'lsa UPDATE,
        // yo'q bo'lsa INSERT qilamiz (DB yangi central ID beradi), va uni lokal central_id ga saqlaymiz.

        private static int SyncRefUserCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,name,role_type,color,sync_token,central_id FROM user_category").Rows)
            {
                Guid tok = EnsureToken(local, "user_category", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM user_category WHERE sync_token=@t", "@t", tok);
                // Unique(name,tenant_id) — nom bo'yicha fallback
                if (!cid.HasValue)
                {
                    cid = ScalarOrNull(central,
                        "SELECT TOP 1 id FROM user_category WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                        "@n", r["name"]);
                    if (cid.HasValue)
                        Exec(central, "UPDATE user_category SET sync_token=@t WHERE id=@cid",
                            P("@t", tok), P("@cid", cid.Value));
                }
                if (cid.HasValue)
                    Exec(central, "UPDATE user_category SET name=@n,role_type=@rt,color=@c WHERE id=@cid",
                        P("@n",r["name"]),P("@rt",r["role_type"]),P("@c",r["color"]),P("@cid",cid.Value));
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO user_category(name,role_type,color,sync_token) OUTPUT INSERTED.id VALUES(@n,@rt,@c,@t)",
                        P("@n",r["name"]),P("@rt",r["role_type"]),P("@c",r["color"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                }
                Exec(local,"UPDATE user_category SET central_id=@c WHERE id=@id",
                    P("@c",cid.Value),P("@id",r["id"]));
                count++;
            }
            return count;
        }

        private static int SyncRefUsers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT u.id,u.name,u.user_category_id,u.login,u.password,u.app_password," +
                "u.phone_number,u.created_at,u.updated_at,u.sort_order,u.sync_token,u.central_id," +
                "uc.central_id AS cat_central_id " +
                "FROM [user] u LEFT JOIN user_category uc ON uc.id=u.user_category_id").Rows)
            {
                Guid tok = EnsureToken(local, "[user]", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM [user] WHERE sync_token=@t", "@t", tok);
                // Unique(login,tenant_id) — login bo'yicha fallback
                if (!cid.HasValue)
                {
                    cid = ScalarOrNull(central,
                        "SELECT TOP 1 id FROM [user] WHERE login=@l AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                        "@l", r["login"]);
                    if (cid.HasValue)
                        Exec(central, "UPDATE [user] SET sync_token=@t WHERE id=@cid",
                            P("@t", tok), P("@cid", cid.Value));
                }
                // Kategoriya central ID sini ishlatamiz
                object catCid = (r["cat_central_id"] == DBNull.Value)
                    ? (object)r["user_category_id"] : r["cat_central_id"];
                if (cid.HasValue)
                    Exec(central,
                        "UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw," +
                        "app_password=@ap,phone_number=@ph,updated_at=@ua,sort_order=@so WHERE id=@cid",
                        P("@n",r["name"]),P("@uc",catCid),P("@l",r["login"]),P("@pw",r["password"]),
                        P("@ap",r["app_password"]),P("@ph",r["phone_number"]),P("@ua",r["updated_at"]),
                        P("@so",r["sort_order"]),P("@cid",cid.Value));
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO [user](name,user_category_id,login,password,app_password," +
                        "phone_number,created_at,updated_at,sort_order,sync_token) " +
                        "OUTPUT INSERTED.id VALUES(@n,@uc,@l,@pw,@ap,@ph,@ca,@ua,@so,@t)",
                        P("@n",r["name"]),P("@uc",catCid),P("@l",r["login"]),P("@pw",r["password"]),
                        P("@ap",r["app_password"]),P("@ph",r["phone_number"]),P("@ca",r["created_at"]),
                        P("@ua",r["updated_at"]),P("@so",r["sort_order"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                }
                Exec(local,"UPDATE [user] SET central_id=@c WHERE id=@id",
                    P("@c",cid.Value),P("@id",r["id"]));
                count++;
            }
            return count;
        }

        private static int SyncRefFoodCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,name,printer_name,sort_order,sync_token,central_id FROM food_category").Rows)
            {
                Guid tok = EnsureToken(local, "food_category", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM food_category WHERE sync_token=@t", "@t", tok);
                // Unique(name,tenant_id) — nom bo'yicha fallback
                if (!cid.HasValue)
                {
                    cid = ScalarOrNull(central,
                        "SELECT TOP 1 id FROM food_category WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                        "@n", r["name"]);
                    if (cid.HasValue)
                        Exec(central, "UPDATE food_category SET sync_token=@t WHERE id=@cid",
                            P("@t", tok), P("@cid", cid.Value));
                }
                if (cid.HasValue)
                    Exec(central,
                        "UPDATE food_category SET name=@n,printer_name=@pn,sort_order=@so WHERE id=@cid",
                        P("@n",r["name"]),P("@pn",r["printer_name"]),P("@so",r["sort_order"]),P("@cid",cid.Value));
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO food_category(name,printer_name,sort_order,sync_token) OUTPUT INSERTED.id VALUES(@n,@pn,@so,@t)",
                        P("@n",r["name"]),P("@pn",r["printer_name"]),P("@so",r["sort_order"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                }
                Exec(local,"UPDATE food_category SET central_id=@c WHERE id=@id",
                    P("@c",cid.Value),P("@id",r["id"]));
                count++;
            }
            return count;
        }

        private static int SyncRefFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT f.id,f.food_category_id,f.name,f.count,f.original_price,f.selling_price," +
                "f.photo,f.created_at,f.updated_at,f.unit,f.description,f.is_unlimited,f.sort_order," +
                "f.sync_token,f.central_id," +
                "fc.central_id AS cat_central_id " +
                "FROM food f LEFT JOIN food_category fc ON fc.id=f.food_category_id").Rows)
            {
                Guid tok = EnsureToken(local, "food", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM food WHERE sync_token=@t", "@t", tok);

                // BUG FIX: nom bo'yicha fallback — mavjud taomni topib UPDATE qiladi, duplikat yaratmaydi
                if (!cid.HasValue)
                {
                    try
                    {
                        using (var cmd = new System.Data.SqlClient.SqlCommand(
                            "SELECT TOP 1 id FROM food WHERE name=@n " +
                            "AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)", central))
                        {
                            cmd.Parameters.AddWithValue("@n", r["name"]);
                            object val = cmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value)
                            {
                                cid = Convert.ToInt32(val);
                                Exec(central, "UPDATE food SET sync_token=@t WHERE id=@cid",
                                    P("@t", tok), P("@cid", cid.Value));
                            }
                        }
                    }
                    catch { }
                }

                object catCid = (r["cat_central_id"] == DBNull.Value)
                    ? (object)r["food_category_id"] : r["cat_central_id"];
                if (cid.HasValue)
                {
                    Exec(central,
                        "UPDATE food SET food_category_id=@fc,name=@n,count=@cnt,original_price=@op," +
                        "selling_price=@sp,photo=@ph,updated_at=@ua,unit=@u,description=@d," +
                        "is_unlimited=@iu,sort_order=@so WHERE id=@cid",
                        P("@fc",catCid),P("@n",r["name"]),P("@cnt",r["count"]),
                        P("@op",r["original_price"]),P("@sp",r["selling_price"]),P("@ph",r["photo"]),
                        P("@ua",r["updated_at"]),P("@u",r["unit"]),P("@d",r["description"]),
                        P("@iu",r["is_unlimited"]),P("@so",r["sort_order"]),P("@cid",cid.Value));
                    Exec(local,"UPDATE food SET central_id=@c WHERE id=@id",
                        P("@c",cid.Value),P("@id",r["id"]));
                }
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO food(food_category_id,name,count,original_price,selling_price," +
                        "photo,created_at,updated_at,unit,description,is_unlimited,sort_order,sync_token) " +
                        "OUTPUT INSERTED.id VALUES(@fc,@n,@cnt,@op,@sp,@ph,@ca,@ua,@u,@d,@iu,@so,@t)",
                        P("@fc",catCid),P("@n",r["name"]),P("@cnt",r["count"]),
                        P("@op",r["original_price"]),P("@sp",r["selling_price"]),P("@ph",r["photo"]),
                        P("@ca",r["created_at"]),P("@ua",r["updated_at"]),P("@u",r["unit"]),
                        P("@d",r["description"]),P("@iu",r["is_unlimited"]),P("@so",r["sort_order"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                    Exec(local,"UPDATE food SET central_id=@c WHERE id=@id",
                        P("@c",cid.Value),P("@id",r["id"]));
                }
                count++;
            }
            return count;
        }

        private static int SyncRefPlaceCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name FROM place_category").Rows)
            {
                // Unique(tenant_id,name) — nom bo'yicha upsert
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM place_category WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT))" +
                    " UPDATE place_category SET name=@n WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)" +
                    " ELSE INSERT INTO place_category(name) VALUES(@n)",
                    P("@n",r["name"]));
                count++;
            }
            return count;
        }

        // place_category: markaziy DB da oladi yoki yaratadi
        private static int EnsureCentralPlaceCategory(SqlConnection central, string catName)
        {
            if (!string.IsNullOrEmpty(catName))
            {
                int? id = ScalarOrNull(central,
                    "SELECT TOP 1 id FROM place_category WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                    "@n", catName);
                if (id.HasValue) return id.Value;
            }
            // Mavjud birinchi kategoriyani olamiz
            try
            {
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1 id FROM place_category WHERE tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                    central))
                {
                    object v = cmd.ExecuteScalar();
                    if (v != null && v != DBNull.Value) return Convert.ToInt32(v);
                }
            }
            catch { }
            // Hali ham yo'q — yangi yaratamiz
            object newId = Exec(central,
                "INSERT INTO place_category(name) OUTPUT INSERTED.id VALUES(@n)",
                P("@n", string.IsNullOrEmpty(catName) ? "Umumiy" : catName));
            return Convert.ToInt32(newId);
        }

        private static int SyncRefPlaceOuts(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // central_id ustuni mavjudligini tekshirish (lokal DB da bo'lmasligi mumkin)
            bool hasCentralId = false;
            try { using (var t = new System.Data.SqlClient.SqlCommand("SELECT TOP 0 central_id FROM place_out", local)) { t.ExecuteNonQuery(); hasCentralId = true; } } catch { }

            // place_count: saqlangan qiymat o'rniga haqiqiy sonni hisoblaymiz
            string sql =
                "SELECT po.id, po.name, " +
                "(SELECT COUNT(*) FROM place_in WHERE place_out_id=po.id) AS place_count, " +
                "po.created_at, po.updated_at, " +
                "po.serviceFee, po.price, po.sort_order, pc.name AS cat_name" +
                (hasCentralId ? ", po.central_id" : ", CAST(NULL AS INT) AS central_id") +
                " FROM place_out po LEFT JOIN place_category pc ON pc.id=po.place_category_id";

            foreach (DataRow r in ReadAll(local, sql).Rows)
            {
                int centralCatId = EnsureCentralPlaceCategory(central, r["cat_name"] as string);

                // central_id bo'yicha topishga harakat (mavjud bo'lsa)
                int? cid = r["central_id"] as int?;
                // Yo'q bo'lsa nom bo'yicha topamiz
                if (!cid.HasValue)
                    cid = ScalarOrNull(central,
                        "SELECT TOP 1 id FROM place_out WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                        "@n", r["name"]);

                if (cid.HasValue)
                {
                    Exec(central,
                        "UPDATE place_out SET name=@n,place_count=@cnt,serviceFee=@sf,price=@pr," +
                        "sort_order=@so,updated_at=@ua WHERE id=@cid",
                        P("@n",r["name"]),P("@cnt",r["place_count"]),P("@sf",r["serviceFee"]),
                        P("@pr",r["price"]),P("@so",r["sort_order"]),
                        P("@ua",r["updated_at"]),P("@cid",cid.Value));
                }
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO place_out(place_category_id,name,place_count,created_at,updated_at,serviceFee,price,sort_order)" +
                        " OUTPUT INSERTED.id VALUES(@catid,@n,@cnt,@ca,@ua,@sf,@pr,@so)",
                        P("@catid",centralCatId),P("@n",r["name"]),P("@cnt",r["place_count"]),
                        P("@ca",r["created_at"]),P("@ua",r["updated_at"]),P("@sf",r["serviceFee"]),
                        P("@pr",r["price"]),P("@so",r["sort_order"]));
                    cid = Convert.ToInt32(newId);
                }
                // central_id ni lokal DB ga saqlash (ustun mavjud bo'lsa)
                if (hasCentralId)
                    try { Exec(local, "UPDATE place_out SET central_id=@c WHERE id=@id", P("@c",cid.Value), P("@id",r["id"])); } catch { }
                count++;
            }
            return count;
        }

        private static int SyncRefPlaceIns(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // central_id ustuni mavjudligini tekshirish
            bool hasCentralId = false;
            try { using (var t = new System.Data.SqlClient.SqlCommand("SELECT TOP 0 central_id FROM place_in", local)) { t.ExecuteNonQuery(); hasCentralId = true; } } catch { }

            string sql =
                "SELECT pi.id, pi.room_name, pi.empty, pi.created_at, pi.price, po.name AS zone_name" +
                (hasCentralId ? ", pi.central_id" : ", CAST(NULL AS INT) AS central_id") +
                " FROM place_in pi JOIN place_out po ON po.id=pi.place_out_id";

            foreach (DataRow r in ReadAll(local, sql).Rows)
            {
                // Zona central ID sini nom bo'yicha topamiz
                int? centralZoneId = ScalarOrNull(central,
                    "SELECT TOP 1 id FROM place_out WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                    "@n", r["zone_name"]);
                if (!centralZoneId.HasValue) continue;

                // central_id bo'yicha yoki nom+zona bo'yicha topamiz
                int? cid = r["central_id"] as int?;
                if (!cid.HasValue)
                {
                    try
                    {
                        using (var cmd = new System.Data.SqlClient.SqlCommand(
                            "SELECT TOP 1 id FROM place_in WHERE room_name=@rn AND place_out_id=@po " +
                            "AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)", central))
                        {
                            cmd.Parameters.AddWithValue("@rn", r["room_name"]);
                            cmd.Parameters.AddWithValue("@po", centralZoneId.Value);
                            object v = cmd.ExecuteScalar();
                            if (v != null && v != DBNull.Value) cid = Convert.ToInt32(v);
                        }
                    }
                    catch { }
                }

                if (cid.HasValue)
                {
                    Exec(central,
                        "UPDATE place_in SET room_name=@rn,place_out_id=@po,price=@pr,empty=@e WHERE id=@cid",
                        P("@rn",r["room_name"]),P("@po",centralZoneId.Value),
                        P("@pr",r["price"]),P("@e",r["empty"]),P("@cid",cid.Value));
                }
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO place_in(place_out_id,room_name,empty,created_at,price)" +
                        " OUTPUT INSERTED.id VALUES(@po,@rn,'YES',@ca,@pr)",
                        P("@po",centralZoneId.Value),P("@rn",r["room_name"]),
                        P("@ca",r["created_at"]),P("@pr",r["price"]));
                    cid = Convert.ToInt32(newId);
                }
                if (hasCentralId)
                    try { Exec(local, "UPDATE place_in SET central_id=@c WHERE id=@id", P("@c",cid.Value), P("@id",r["id"])); } catch { }
                count++;
            }
            return count;
        }

        private static int SyncRefPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name,sort_order,sync_token,central_id FROM payment").Rows)
            {
                Guid tok = EnsureToken(local, "payment", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM payment WHERE sync_token=@t", "@t", tok);
                // Unique(name,tenant_id) — nom bo'yicha fallback
                if (!cid.HasValue)
                {
                    cid = ScalarOrNull(central,
                        "SELECT TOP 1 id FROM payment WHERE name=@n AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)",
                        "@n", r["name"]);
                    if (cid.HasValue)
                        Exec(central, "UPDATE payment SET sync_token=@t WHERE id=@cid",
                            P("@t", tok), P("@cid", cid.Value));
                }
                if (cid.HasValue)
                    Exec(central, "UPDATE payment SET name=@n,sort_order=@so WHERE id=@cid",
                        P("@n",r["name"]),P("@so",r["sort_order"]),P("@cid",cid.Value));
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO payment(name,sort_order,sync_token) OUTPUT INSERTED.id VALUES(@n,@so,@t)",
                        P("@n",r["name"]),P("@so",r["sort_order"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                }
                Exec(local,"UPDATE payment SET central_id=@c WHERE id=@id",
                    P("@c",cid.Value),P("@id",r["id"]));
                count++;
            }
            return count;
        }

        private static int SyncRefIngredients(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,name,unit,quantity,price_per_unit,min_quantity,sync_token,central_id FROM ingredient").Rows)
            {
                Guid tok = EnsureToken(local, "ingredient", r);
                int? cid = r["central_id"] as int? ?? ScalarOrNull(central,
                    "SELECT id FROM ingredient WHERE sync_token=@t", "@t", tok);

                // Nom bo'yicha fallback — central da xuddi shu nomli ingredient bo'lsa
                if (!cid.HasValue)
                {
                    try
                    {
                        using (var cmd = new System.Data.SqlClient.SqlCommand(
                            "SELECT TOP 1 id FROM ingredient WHERE name=@n " +
                            "AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)", central))
                        {
                            cmd.Parameters.AddWithValue("@n", r["name"]);
                            object val = cmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value)
                            {
                                cid = Convert.ToInt32(val);
                                Exec(central, "UPDATE ingredient SET sync_token=@t WHERE id=@cid",
                                    P("@t", tok), P("@cid", cid.Value));
                            }
                        }
                    }
                    catch { }
                }

                if (cid.HasValue)
                {
                    // quantity ni overwrite qilmaymiz — SyncIngredientQuantities delta orqali hal qiladi
                    Exec(central,
                        "UPDATE ingredient SET name=@n,unit=@u,price_per_unit=@pp,min_quantity=@mq WHERE id=@cid",
                        P("@n",r["name"]),P("@u",r["unit"]),
                        P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),P("@cid",cid.Value));
                    Exec(local,"UPDATE ingredient SET central_id=@c WHERE id=@id",
                        P("@c",cid.Value),P("@id",r["id"]));
                }
                else
                {
                    object newId = Exec(central,
                        "INSERT INTO ingredient(name,unit,quantity,price_per_unit,min_quantity,sync_token) " +
                        "OUTPUT INSERTED.id VALUES(@n,@u,@q,@pp,@mq,@t)",
                        P("@n",r["name"]),P("@u",r["unit"]),P("@q",r["quantity"]),
                        P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),P("@t",tok));
                    cid = Convert.ToInt32(newId);
                    // synced_qty bazasini o'rnatamiz — delta tracking to'g'ri ishlashi uchun
                    Exec(local,
                        "UPDATE ingredient SET central_id=@c, synced_qty=quantity WHERE id=@id",
                        P("@c",cid.Value),P("@id",r["id"]));
                }
                count++;
            }
            return count;
        }

        private static int SyncRefRecipeIngredients(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,food_id,ingredient_id,quantity_per_portion FROM recipe_ingredient").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM recipe_ingredient WHERE id=@id)" +
                    " UPDATE recipe_ingredient SET food_id=@fid,ingredient_id=@iid,quantity_per_portion=@qpp WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT recipe_ingredient ON;" +
                    " INSERT INTO recipe_ingredient(id,food_id,ingredient_id,quantity_per_portion) VALUES(@id,@fid,@iid,@qpp);" +
                    " SET IDENTITY_INSERT recipe_ingredient OFF END",
                    P("@id",r["id"]),P("@fid",r["food_id"]),
                    P("@iid",r["ingredient_id"]),P("@qpp",r["quantity_per_portion"]));
                count++;
            }
            return count;
        }

        private static int SyncIngredientPurchases(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT ip.id, ip.ingredient_id, ip.quantity, ip.price_per_unit, ip.total_price," +
                "  ip.purchased_at, ip.notes, ip.sync_token, i.central_id AS ing_central_id " +
                "FROM ingredient_purchase ip " +
                "LEFT JOIN ingredient i ON i.id=ip.ingredient_id " +
                "WHERE ip.is_synced=0").Rows)
            {
                // sync_token NULL bo'lsa (eski DB) — NEWID yaratib saqlaymiz
                Guid tok;
                if (r["sync_token"] == DBNull.Value || !(r["sync_token"] is Guid))
                {
                    tok = Guid.NewGuid();
                    Exec(local, "UPDATE ingredient_purchase SET sync_token=@t WHERE id=@id",
                        P("@t", tok), P("@id", r["id"]));
                }
                else tok = (Guid)r["sync_token"];

                // central_id yo'q bo'lsa ingredient hali sync bo'lmagan — keyingi siklda qayta sinab ko'riladi
                if (r["ing_central_id"] == DBNull.Value) continue;
                int centralIngId = Convert.ToInt32(r["ing_central_id"]);
                if (ScalarOrNull(central,"SELECT id FROM ingredient_purchase WHERE sync_token=@t","@t",tok) == null)
                    Exec(central,
                        "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token)" +
                        " VALUES(@iid,@q,@pp,@tp,@pa,@n,@tok)",
                        P("@iid",centralIngId),P("@q",r["quantity"]),P("@pp",r["price_per_unit"]),
                        P("@tp",r["total_price"]),P("@pa",r["purchased_at"]),P("@n",r["notes"]),P("@tok",tok));
                Exec(local,"UPDATE ingredient_purchase SET is_synced=1 WHERE id=@id",P("@id",r["id"]));
                count++;
            }
            return count;
        }

        private static int SyncFoodPurchases(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,food_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token " +
                "FROM food_purchase WHERE is_synced=0").Rows)
            {
                Guid tok;
                if (r["sync_token"] == DBNull.Value || !(r["sync_token"] is Guid))
                {
                    tok = Guid.NewGuid();
                    Exec(local, "UPDATE food_purchase SET sync_token=@t WHERE id=@id",
                        P("@t", tok), P("@id", r["id"]));
                }
                else tok = (Guid)r["sync_token"];
                if (ScalarOrNull(central,"SELECT id FROM food_purchase WHERE sync_token=@t","@t",tok) == null)
                    Exec(central,
                        "INSERT INTO food_purchase(food_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token)" +
                        " VALUES(@fid,@q,@pp,@tp,@pa,@n,@tok)",
                        P("@fid",r["food_id"]),P("@q",r["quantity"]),P("@pp",r["price_per_unit"]),
                        P("@tp",r["total_price"]),P("@pa",r["purchased_at"]),P("@n",r["notes"]),P("@tok",tok));
                Exec(local,"UPDATE food_purchase SET is_synced=1 WHERE id=@id",P("@id",r["id"]));
                count++;
            }
            return count;
        }

        // ── 1. Mijozlar ──────────────────────────────────────────────────────
        private static int SyncCustomers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT id, name, phone, notes, created_at, sync_token FROM customer WHERE is_synced = 0");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];

                int? centralId = ScalarOrNull(central,
                    "SELECT id FROM customer WHERE sync_token = @t", "@t", tok);

                if (centralId == null)
                {
                    string phone = r["phone"] as string;
                    if (!string.IsNullOrEmpty(phone))
                        centralId = ScalarOrNull(central,
                            "SELECT TOP 1 id FROM customer WHERE phone = @p", "@p", phone);
                }

                if (centralId == null)
                {
                    object inserted = Exec(central,
                        "INSERT INTO customer (name, phone, notes, created_at, sync_token) " +
                        "OUTPUT INSERTED.id " +
                        "VALUES (@n, @p, @no, @c, @t)",
                        P("@n",  r["name"]),       P("@p",  r["phone"]),
                        P("@no", r["notes"]),       P("@c",  r["created_at"]),
                        P("@t",  tok));
                    centralId = Convert.ToInt32(inserted);
                }

                Exec(local,
                    "UPDATE customer SET is_synced = 1, central_id = @cid WHERE id = @id",
                    P("@cid", centralId), P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 2. Buyurtmalar ───────────────────────────────────────────────────
        private static int SyncOrders(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT o.*, " +
                "  c.central_id  AS c_central_id, " +
                "  u.central_id  AS u_central_id, " +
                "  p.central_id  AS p_central_id, " +
                "  p2.central_id AS p2_central_id, " +
                "  pi.central_id AS pi_central_id " +
                "FROM [order] o " +
                "LEFT JOIN customer  c  ON c.id  = o.customer_id " +
                "LEFT JOIN [user]    u  ON u.id  = o.user_id " +
                "LEFT JOIN payment   p  ON p.id  = o.payment_id " +
                "LEFT JOIN payment   p2 ON p2.id = o.payment2_id " +
                "LEFT JOIN place_in  pi ON pi.id = o.place_id " +
                "WHERE o.is_synced = 0");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];

                int? centralId = ScalarOrNull(central,
                    "SELECT id FROM [order] WHERE sync_token = @t", "@t", tok);

                if (centralId == null)
                {
                    // Central ID lardan foydalanamiz (local ID o'rniga)
                    object centralCustId = r["c_central_id"]  == DBNull.Value ? DBNull.Value : r["c_central_id"];
                    object centralUserId = r["u_central_id"]  == DBNull.Value ? r["user_id"]  : r["u_central_id"];
                    object centralPayId  = r["p_central_id"]  == DBNull.Value ? r["payment_id"] : r["p_central_id"];
                    object centralPay2Id = r["p2_central_id"] == DBNull.Value ? r["payment2_id"] : r["p2_central_id"];
                    object centralPlaceId= r["pi_central_id"] == DBNull.Value ? r["place_id"]  : r["pi_central_id"];

                    object inserted = Exec(central,
                        "INSERT INTO [order] " +
                        "  (user_id, place_id, payment_id, created_at, paid, total, " +
                        "   discount_amount, discount_pct, customer_id, customer_name, " +
                        "   delivery_phone, delivery_address, is_delivery, order_note, " +
                        "   custom_svc_fee, custom_svc_type, payment2_id, payment2_amount, sync_token) " +
                        "OUTPUT INSERTED.id " +
                        "VALUES (@uid,@pid,@pay,@cat,@paid,@tot,@disc,@discp,@cust,@custn," +
                        "        @dph,@dadr,@isdel,@note,@svf,@svt,@p2,@p2a,@tok)",
                        P("@uid",   centralUserId),          P("@pid",   centralPlaceId),
                        P("@pay",   centralPayId),            P("@cat",   r["created_at"]),
                        P("@paid",  r["paid"]),              P("@tot",   r["total"]),
                        P("@disc",  r["discount_amount"]),   P("@discp", r["discount_pct"]),
                        P("@cust",  centralCustId),          P("@custn", r["customer_name"]),
                        P("@dph",   r["delivery_phone"]),    P("@dadr",  r["delivery_address"]),
                        P("@isdel", r["is_delivery"]),       P("@note",  r["order_note"]),
                        P("@svf",   r["custom_svc_fee"]),    P("@svt",   r["custom_svc_type"]),
                        P("@p2",    centralPay2Id),           P("@p2a",   r["payment2_amount"]),
                        P("@tok",   tok));
                    centralId = Convert.ToInt32(inserted);
                }
                else
                {
                    object centralCustId2 = r["c_central_id"]  == DBNull.Value ? DBNull.Value : r["c_central_id"];
                    object centralPayId2  = r["p_central_id"]  == DBNull.Value ? r["payment_id"]  : r["p_central_id"];
                    object centralPay2Id2 = r["p2_central_id"] == DBNull.Value ? r["payment2_id"] : r["p2_central_id"];
                    // FIX: Mavjud buyurtmani yangilash — delivery fieldlari ham qo'shildi
                    Exec(central,
                        "UPDATE [order] SET " +
                        "  paid=@paid, total=@tot, discount_amount=@disc, discount_pct=@discp, " +
                        "  payment_id=@pay, custom_svc_fee=@svf, custom_svc_type=@svt, " +
                        "  order_note=@note, payment2_id=@p2, payment2_amount=@p2a," +
                        "  customer_id=@cust, customer_name=@custn," +
                        "  delivery_phone=@dph, delivery_address=@dadr, is_delivery=@isdel " +
                        "WHERE id=@cid",
                        P("@paid",  r["paid"]),              P("@tot",   r["total"]),
                        P("@disc",  r["discount_amount"]),   P("@discp", r["discount_pct"]),
                        P("@pay",   centralPayId2),           P("@svf",   r["custom_svc_fee"]),
                        P("@svt",   r["custom_svc_type"]),   P("@note",  r["order_note"]),
                        P("@p2",    centralPay2Id2),          P("@p2a",   r["payment2_amount"]),
                        P("@cust",  centralCustId2),          P("@custn", r["customer_name"]),
                        P("@dph",   r["delivery_phone"]),    P("@dadr",  r["delivery_address"]),
                        P("@isdel", r["is_delivery"]),
                        P("@cid",   centralId.Value));
                }

                Exec(local,
                    "UPDATE [order] SET is_synced = 1, central_id = @cid WHERE id = @id",
                    P("@cid", centralId), P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 3. Buyurtma tarkibi ──────────────────────────────────────────────
        // FIX: Delete+reinsert strategiyasi — o'chirilgan taomlar ham sinxronlanadi
        private static int SyncOrderFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;

            // is_synced=0 bo'lgan order_food larni o'z ichiga olgan buyurtmalar
            DataTable orders = ReadAll(local,
                @"SELECT DISTINCT o.id AS local_id, o.central_id
                  FROM order_food f
                  JOIN [order] o ON o.id = f.order_id
                  WHERE f.is_synced = 0 AND o.central_id IS NOT NULL");

            foreach (DataRow ord in orders.Rows)
            {
                int localOrderId   = Convert.ToInt32(ord["local_id"]);
                int centralOrderId = Convert.ToInt32(ord["central_id"]);

                // food central_id ni ham olamiz (lokal food_id ni central ID ga aylantiramiz)
                DataTable foods = ReadAll(local,
                    $"SELECT of2.food_id, of2.quantity, of2.note, of2.sync_token, " +
                    $"ISNULL(f.central_id, of2.food_id) AS central_food_id " +
                    $"FROM order_food of2 " +
                    $"LEFT JOIN food f ON f.id=of2.food_id " +
                    $"WHERE of2.order_id={localOrderId}");

                // Central da lokal ro'yxatda yo'q qatorlarni o'chiramiz (o'chirilgan taomlar)
                Exec(central, "DELETE FROM order_food WHERE order_id=@oid", P("@oid", centralOrderId));

                foreach (DataRow f in foods.Rows)
                {
                    Exec(central,
                        "IF EXISTS(SELECT 1 FROM order_food WHERE sync_token=@tok) " +
                        "  UPDATE order_food SET order_id=@oid,food_id=@fid,quantity=@qty,note=@note WHERE sync_token=@tok " +
                        "ELSE " +
                        "  INSERT INTO order_food(order_id,food_id,quantity,note,sync_token) VALUES(@oid,@fid,@qty,@note,@tok)",
                        P("@oid",  centralOrderId), P("@fid",  f["central_food_id"]),
                        P("@qty",  f["quantity"]),   P("@note", f["note"]),
                        P("@tok",  f["sync_token"]));
                    count++;
                }

                // Ushbu buyurtmaning barcha order_food larini sinxronlandi deb belgilaymiz
                Exec(local,
                    $"UPDATE order_food SET is_synced=1 WHERE order_id={localOrderId}");
            }
            return count;
        }

        // ── 4. To'lovlar ────────────────────────────────────────────────────
        private static int SyncOrderPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT p.*, o.central_id AS order_central_id, " +
                "  ISNULL(pay.central_id, p.payment_id) AS central_payment_id " +
                "FROM order_payments p " +
                "JOIN [order] o ON o.id = p.order_id " +
                "LEFT JOIN payment pay ON pay.id = p.payment_id " +
                "WHERE p.is_synced = 0 AND o.central_id IS NOT NULL");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM order_payments WHERE sync_token = @t", "@t", tok);

                if (exists == null)
                    Exec(central,
                        "INSERT INTO order_payments (order_id, payment_id, amount, sync_token) " +
                        "VALUES (@oid, @pid, @amt, @tok)",
                        P("@oid", r["order_central_id"]), P("@pid", r["central_payment_id"]),
                        P("@amt", r["amount"]),            P("@tok", tok));
                else
                    Exec(central,
                        "UPDATE order_payments SET payment_id=@pid, amount=@amt WHERE sync_token=@tok",
                        P("@pid", r["central_payment_id"]), P("@amt", r["amount"]), P("@tok", tok));

                Exec(local, "UPDATE order_payments SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 5. Qarzlar — yangi va to'langan ────────────────────────────────
        private static int SyncOrderDebts(SqlConnection local, SqlConnection central)
        {
            int count = 0;

            // 5a. Yangi qarzlar (is_synced=0)
            DataTable rows = ReadAll(local,
                "SELECT d.*, o.central_id AS order_central_id " +
                "FROM order_debt d " +
                "JOIN [order] o ON o.id = d.order_id " +
                "WHERE d.is_synced = 0 AND o.central_id IS NOT NULL");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM order_debt WHERE sync_token = @t", "@t", tok);

                if (exists == null)
                    Exec(central,
                        "INSERT INTO order_debt " +
                        "  (order_id, debtor_name, debtor_phone, debt_note, amount, created_at, is_paid, paid_at, sync_token) " +
                        "VALUES (@oid,@dn,@dp,@no,@amt,@cat,@paid,@pat,@tok)",
                        P("@oid",  r["order_central_id"]), P("@dn",   r["debtor_name"]),
                        P("@dp",   r["debtor_phone"]),      P("@no",   r["debt_note"]),
                        P("@amt",  r["amount"]),             P("@cat",  r["created_at"]),
                        P("@paid", r["is_paid"]),            P("@pat",  r["paid_at"]),
                        P("@tok",  tok));
                else
                    // Mavjud bo'lsa to'lov holatini yangilash
                    Exec(central,
                        "UPDATE order_debt SET is_paid=@paid, paid_at=@pat WHERE sync_token=@tok",
                        P("@paid", r["is_paid"]), P("@pat", r["paid_at"]), P("@tok", tok));

                Exec(local, "UPDATE order_debt SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
            }

            // 5b. To'langan qarzlarni yangilash (is_synced=1, lekin to'lov holati o'zgardi)
            DataTable paid = ReadAll(local,
                "SELECT d.sync_token, d.paid_at " +
                "FROM order_debt d " +
                "WHERE d.is_synced=1 AND d.is_paid=1 AND d.paid_at IS NOT NULL " +
                "  AND NOT EXISTS (SELECT 1 FROM order_debt_paid_sync WHERE debt_sync_token=d.sync_token)");

            foreach (DataRow r in paid.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                Exec(central,
                    "UPDATE order_debt SET is_paid=1, paid_at=@pat WHERE sync_token=@tok AND is_paid=0",
                    P("@pat", r["paid_at"]), P("@tok", tok));
                // Qayta yuborilmasligi uchun lokal jadvalga yozamiz
                try
                {
                    Exec(local,
                        "IF NOT EXISTS(SELECT 1 FROM order_debt_paid_sync WHERE debt_sync_token=@tok) " +
                        "  INSERT INTO order_debt_paid_sync(debt_sync_token) VALUES(@tok)",
                        P("@tok", tok));
                }
                catch { /* jadval yo'q bo'lsa o'tkazib yuboramiz */ }
                count++;
            }

            return count;
        }

        // ── 6. Bekor qilishlar ───────────────────────────────────────────────
        private static int SyncCancellationLogs(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT l.*, o.central_id AS order_central_id " +
                "FROM order_cancellation_log l " +
                "JOIN [order] o ON o.id = l.order_id " +
                "WHERE l.is_synced = 0 AND o.central_id IS NOT NULL");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM order_cancellation_log WHERE sync_token = @t", "@t", tok);

                if (exists == null)
                    Exec(central,
                        "INSERT INTO order_cancellation_log " +
                        "  (order_id, food_id, food_name, food_category, cancelled_qty, cancelled_by, cancelled_at, sync_token) " +
                        "VALUES (@oid,@fid,@fn,@fc,@qty,@by,@at,@tok)",
                        P("@oid", r["order_central_id"]), P("@fid", r["food_id"]),
                        P("@fn",  r["food_name"]),         P("@fc",  r["food_category"]),
                        P("@qty", r["cancelled_qty"]),     P("@by",  r["cancelled_by"]),
                        P("@at",  r["cancelled_at"]),      P("@tok", tok));

                Exec(local, "UPDATE order_cancellation_log SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 7. Kassa harakatlari ─────────────────────────────────────────────
        private static int SyncCashTransactions(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT * FROM cash_transaction WHERE is_synced = 0");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM cash_transaction WHERE sync_token = @t", "@t", tok);

                if (exists == null)
                    Exec(central,
                        "INSERT INTO cash_transaction " +
                        "  (type, category, amount, description, created_by, created_at, sync_token) " +
                        "VALUES (@tp,@cat,@amt,@desc,@by,@at,@tok)",
                        P("@tp",   r["type"]),       P("@cat",  r["category"]),
                        P("@amt",  r["amount"]),      P("@desc", r["description"]),
                        P("@by",   r["created_by"]), P("@at",   r["created_at"]),
                        P("@tok",  tok));

                Exec(local, "UPDATE cash_transaction SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ══════════════════════════════════════════════════════════════════════
        // DOWNLOAD: Central → Local  (reference ma'lumotlar)
        // ══════════════════════════════════════════════════════════════════════
        public static SyncResult DownloadAll()
        {
            var result = new SyncResult();
            if (!Session.IsOnline || Session.TenantId == 0) return result;

            // Har sync/download oldidan lokal sxemani yangilash
            dbconnect.FixLocalDefaults();

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                // Har bir jadval mustaqil — biri xato bo'lsa qolganlari davom etadi
                // ── Referens ma'lumotlar ──────────────────────────────────────
                TryDl(() => DlSettings(local, central),             result);
                TryDl(() => DlUserCategories(local, central),       result);
                TryDl(() => DlUsers(local, central),                result);
                TryDl(() => DlFoodCategories(local, central),       result);
                TryDl(() => DlFoods(local, central),                result);
                TryDl(() => DlPlaceCategories(local, central),      result);
                TryDl(() => DlPlaceOuts(local, central),            result);
                TryDl(() => DlPlaceIns(local, central),             result);
                TryDl(() => DlPayments(local, central),             result);
                TryDl(() => DlIngredients(local, central),          result);
                TryDl(() => DlRecipeIngredients(local, central),    result);
                // ── Tranzaksiya ma'lumotlari (so'nggi 1 yil) ─────────────────
                TryDl(() => DlCustomers(local, central),            result);
                TryDl(() => DlOrders(local, central),               result);
                TryDl(() => DlOrderFoods(local, central),           result);
                TryDl(() => DlOrderPayments(local, central),        result);
                TryDl(() => DlOrderDebts(local, central),           result);
                TryDl(() => DlCancellationLogs(local, central),     result);
                TryDl(() => DlCashTransactions(local, central),     result);
                TryDl(() => DlIngredientPurchases(local, central),  result);
                TryDl(() => DlFoodPurchases(local, central),        result);
            }
            catch (Exception ex)
            {
                result.LastError = ex.Message;
                result.Errors++;
            }
            finally
            {
                if (local   != null) { local.Close();   local.Dispose(); }
                if (central != null) { central.Close(); central.Dispose(); }
            }
            return result;
        }

        private static void TryDl(Func<int> action, SyncResult result)
        {
            try { result.Synced += action(); }
            catch (Exception ex) { result.Errors++; if (result.LastError == null) result.LastError = ex.Message; }
        }

        private static int DlSettings(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // Tenant kontekst allaqachon central ulanishda o'rnatilgan
            DataTable rows = ReadAll(central,
                "SELECT [key], value FROM settings WHERE tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM settings WHERE [key]=@k AND tenant_id=0) " +
                    "  UPDATE settings SET value=@v WHERE [key]=@k AND tenant_id=0 " +
                    "ELSE INSERT INTO settings([key],value,tenant_id) VALUES(@k,@v,0)",
                    P("@k", r["key"]), P("@v", r["value"]));
                count++;
            }
            return count;
        }

        private static int DlUserCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central, "SELECT id,name,role_type,color FROM user_category");
            foreach (DataRow r in rows.Rows)
            {
                // BUG FIX: avval central_id ga mos lokal yozuvni qidirish (duplikat oldini olish)
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM user_category WHERE central_id=@id) " +
                    "  UPDATE user_category SET name=@n,role_type=@rt,color=@c WHERE central_id=@id " +
                    "ELSE IF EXISTS (SELECT 1 FROM user_category WHERE id=@id) " +
                    "  UPDATE user_category SET name=@n,role_type=@rt,color=@c,central_id=@id WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT user_category ON; " +
                    "  INSERT INTO user_category(id,name,role_type,color,central_id) VALUES(@id,@n,@rt,@c,@id); " +
                    "  SET IDENTITY_INSERT user_category OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]),
                    P("@rt", r["role_type"]), P("@c", r["color"]));
                count++;
            }
            return count;
        }

        private static int DlUsers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,name,user_category_id,login,password,app_password,phone_number,created_at,updated_at,sort_order FROM [user]");
            foreach (DataRow r in rows.Rows)
            {
                // BUG FIX: login bo'yicha avval lokal yozuvni topish (duplikat oldini olish)
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM [user] WHERE central_id=@id) " +
                    "  UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw,app_password=@ap," +
                    "    phone_number=@ph,updated_at=@ua,sort_order=@so WHERE central_id=@id " +
                    "ELSE IF EXISTS (SELECT 1 FROM [user] WHERE login=@l) " +
                    "  UPDATE [user] SET name=@n,user_category_id=@uc,password=@pw,app_password=@ap," +
                    "    phone_number=@ph,updated_at=@ua,sort_order=@so,central_id=@id WHERE login=@l " +
                    "ELSE IF EXISTS (SELECT 1 FROM [user] WHERE id=@id) " +
                    "  UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw,app_password=@ap," +
                    "    phone_number=@ph,updated_at=@ua,sort_order=@so,central_id=@id WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT [user] ON; " +
                    "  INSERT INTO [user](id,name,user_category_id,login,password,app_password,phone_number,created_at,updated_at,sort_order,central_id) " +
                    "  VALUES(@id,@n,@uc,@l,@pw,@ap,@ph,@ca,@ua,@so,@id); " +
                    "  SET IDENTITY_INSERT [user] OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]), P("@uc", r["user_category_id"]),
                    P("@l", r["login"]), P("@pw", r["password"]), P("@ap", r["app_password"]),
                    P("@ph", r["phone_number"]), P("@ca", r["created_at"]),
                    P("@ua", r["updated_at"]), P("@so", r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int DlFoodCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central, "SELECT id,name,printer_name,sort_order FROM food_category");
            foreach (DataRow r in rows.Rows)
            {
                // BUG FIX: central_id ga mos lokal yozuvni topish (duplikat oldini olish)
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM food_category WHERE central_id=@id) " +
                    "  UPDATE food_category SET name=@n,printer_name=@pn,sort_order=@so WHERE central_id=@id " +
                    "ELSE IF EXISTS (SELECT 1 FROM food_category WHERE id=@id) " +
                    "  UPDATE food_category SET name=@n,printer_name=@pn,sort_order=@so,central_id=@id WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT food_category ON; " +
                    "  INSERT INTO food_category(id,name,printer_name,sort_order,central_id) VALUES(@id,@n,@pn,@so,@id); " +
                    "  SET IDENTITY_INSERT food_category OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]),
                    P("@pn", r["printer_name"]), P("@so", r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int DlFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,food_category_id,name,count,original_price,selling_price," +
                "photo,created_at,updated_at,unit,description,is_unlimited,sort_order FROM food");
            foreach (DataRow r in rows.Rows)
            {
                // BUG FIX: central_id ga mos lokal yozuvni topish (duplikat oldini olish)
                // Avval central_id=@id tekshiriladi (eski lokal yozuv), keyin id=@id
                // Faqat ikkisi ham yo'q bo'lsagina IDENTITY_INSERT qilinadi va central_id o'rnatiladi
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM food WHERE central_id=@id) " +
                    "  UPDATE food SET food_category_id=@fc,name=@n,count=@cnt,original_price=@op," +
                    "    selling_price=@sp,photo=@ph,updated_at=@ua,unit=@u,description=@d," +
                    "    is_unlimited=@iu,sort_order=@so WHERE central_id=@id " +
                    "ELSE IF EXISTS (SELECT 1 FROM food WHERE id=@id) " +
                    "  UPDATE food SET food_category_id=@fc,name=@n,count=@cnt,original_price=@op," +
                    "    selling_price=@sp,photo=@ph,updated_at=@ua,unit=@u,description=@d," +
                    "    is_unlimited=@iu,sort_order=@so,central_id=@id WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT food ON; " +
                    "  INSERT INTO food(id,food_category_id,name,count,original_price,selling_price," +
                    "    photo,created_at,updated_at,unit,description,is_unlimited,sort_order,central_id) " +
                    "  VALUES(@id,@fc,@n,@cnt,@op,@sp,@ph,@ca,@ua,@u,@d,@iu,@so,@id); " +
                    "  SET IDENTITY_INSERT food OFF " +
                    "END",
                    P("@id", r["id"]), P("@fc", r["food_category_id"]), P("@n", r["name"]),
                    P("@cnt", r["count"]), P("@op", r["original_price"]), P("@sp", r["selling_price"]),
                    P("@ph", r["photo"]), P("@ca", r["created_at"]), P("@ua", r["updated_at"]),
                    P("@u", r["unit"]), P("@d", r["description"]),
                    P("@iu", r["is_unlimited"]), P("@so", r["sort_order"]));
                count++;
            }
            return count;
        }

        // DlFoods + webdan o'chirilgan taomlarni lokaldan ham o'chirish
        private static int DlFoodsWithDelete(SqlConnection local, SqlConnection central)
        {
            int count = DlFoods(local, central);

            // Markaziy DB da yo'q, lekin lokalda central_id bilan mavjud taomlarni o'chirish
            DataTable centralIds = ReadAll(central, "SELECT id FROM food");
            if (centralIds.Rows.Count == 0) return count;

            var ids = new System.Text.StringBuilder();
            foreach (DataRow r in centralIds.Rows)
            {
                if (ids.Length > 0) ids.Append(',');
                ids.Append(r["id"]);
            }

            // order_food da ishlatilmagan taomlarni o'chir
            Exec(local,
                $"DELETE FROM food WHERE central_id IS NOT NULL " +
                $"AND central_id NOT IN ({ids}) " +
                $"AND id NOT IN (SELECT DISTINCT food_id FROM order_food)");

            return count;
        }

        private static int DlPlaceCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central, "SELECT id,name FROM place_category");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM place_category WHERE id=@id) " +
                    "  UPDATE place_category SET name=@n WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT place_category ON; " +
                    "  INSERT INTO place_category(id,name) VALUES(@id,@n); " +
                    "  SET IDENTITY_INSERT place_category OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]));
                count++;
            }
            return count;
        }

        private static int DlPlaceOuts(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,place_category_id,name,place_count,created_at,updated_at,serviceFee,price,sort_order FROM place_out");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM place_out WHERE id=@id) " +
                    "  UPDATE place_out SET place_category_id=@pc,name=@n,place_count=@cnt," +
                    "    updated_at=@ua,serviceFee=@sf,price=@pr,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT place_out ON; " +
                    "  INSERT INTO place_out(id,place_category_id,name,place_count,created_at,updated_at,serviceFee,price,sort_order) " +
                    "  VALUES(@id,@pc,@n,@cnt,@ca,@ua,@sf,@pr,@so); " +
                    "  SET IDENTITY_INSERT place_out OFF " +
                    "END",
                    P("@id", r["id"]), P("@pc", r["place_category_id"]), P("@n", r["name"]),
                    P("@cnt", r["place_count"]), P("@ca", r["created_at"]), P("@ua", r["updated_at"]),
                    P("@sf", r["serviceFee"]), P("@pr", r["price"]), P("@so", r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int DlPlaceIns(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,place_out_id,room_name,empty,created_at,user_id,price FROM place_in");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM place_in WHERE id=@id) " +
                    "  UPDATE place_in SET place_out_id=@po,room_name=@rn,user_id=@uid,price=@pr WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT place_in ON; " +
                    "  INSERT INTO place_in(id,place_out_id,room_name,empty,created_at,user_id,price) " +
                    "  VALUES(@id,@po,@rn,@e,@ca,@uid,@pr); " +
                    "  SET IDENTITY_INSERT place_in OFF " +
                    "END",
                    P("@id", r["id"]), P("@po", r["place_out_id"]), P("@rn", r["room_name"]),
                    P("@e", r["empty"]), P("@ca", r["created_at"]),
                    P("@uid", r["user_id"]), P("@pr", r["price"]));
                count++;
            }
            return count;
        }

        private static int DlPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central, "SELECT id,name,sort_order FROM payment");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM payment WHERE id=@id) " +
                    "  UPDATE payment SET name=@n,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT payment ON; " +
                    "  INSERT INTO payment(id,name,sort_order) VALUES(@id,@n,@so); " +
                    "  SET IDENTITY_INSERT payment OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]), P("@so", r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int DlIngredients(SqlConnection local, SqlConnection central)
        {
            // synced_qty ustunini yaratish (agar mavjud bo'lmasa)
            try
            {
                using (var cmd = new SqlCommand(
                    "IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('ingredient') AND name='synced_qty')" +
                    " ALTER TABLE ingredient ADD synced_qty DECIMAL(18,4) NULL", local))
                    cmd.ExecuteNonQuery();
                using (var cmd = new SqlCommand(
                    "UPDATE ingredient SET synced_qty=quantity WHERE synced_qty IS NULL", local))
                    cmd.ExecuteNonQuery();
            }
            catch { }

            bool hasSyncedQty;
            using (var chk = new SqlCommand(
                "SELECT COUNT(1) FROM sys.columns WHERE object_id=OBJECT_ID('ingredient') AND name='synced_qty'", local))
                hasSyncedQty = Convert.ToInt32(chk.ExecuteScalar()) > 0;

            int count = 0;
            // sync_token ham olamiz — WinForms dan yuklangan ingredientlar uchun matching
            DataTable rows = ReadAll(central,
                "SELECT id,name,unit,quantity,price_per_unit,min_quantity," +
                "ISNULL(sync_token,'00000000-0000-0000-0000-000000000000') AS sync_token FROM ingredient");
            foreach (DataRow r in rows.Rows)
            {
                int  cid = Convert.ToInt32(r["id"]);
                Guid tok = (Guid)r["sync_token"];

                // 1. central_id bo'yicha qidirish (eng ishonchli)
                int? lid = ScalarOrNull(local, "SELECT id FROM ingredient WHERE central_id=@c", "@c", cid);
                // 2. sync_token bo'yicha (WinForms yuklagan ingredient uchun)
                if (lid == null && tok != Guid.Empty)
                    lid = ScalarOrNull(local, "SELECT id FROM ingredient WHERE sync_token=@t", "@t", tok);
                // 3. nom bo'yicha fallback — mustaqil yaratilgan masalliqlar birlashtiriladi
                if (lid == null)
                    lid = ScalarOrNull(local,
                        "SELECT TOP 1 id FROM ingredient WHERE name=@n AND central_id IS NULL", "@n", r["name"]);

                if (lid != null)
                {
                    // Mavjud ingredient yangilash
                    if (hasSyncedQty)
                        Exec(local,
                            "UPDATE ingredient SET name=@n,unit=@u,quantity=@q,synced_qty=@q," +
                            " price_per_unit=@pp,min_quantity=@mq,central_id=@cid WHERE id=@lid",
                            P("@n",r["name"]),P("@u",r["unit"]),P("@q",r["quantity"]),
                            P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),
                            P("@cid",cid),P("@lid",lid.Value));
                    else
                        Exec(local,
                            "UPDATE ingredient SET name=@n,unit=@u,quantity=@q," +
                            " price_per_unit=@pp,min_quantity=@mq,central_id=@cid WHERE id=@lid",
                            P("@n",r["name"]),P("@u",r["unit"]),P("@q",r["quantity"]),
                            P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),
                            P("@cid",cid),P("@lid",lid.Value));
                }
                else
                {
                    // Yangi ingredient (webdan yaratilgan) — IDENTITY_INSERT yo'q, central_id saqlaymiz
                    if (hasSyncedQty)
                        Exec(local,
                            "INSERT INTO ingredient(name,unit,quantity,synced_qty,price_per_unit,min_quantity,central_id,sync_token)" +
                            " VALUES(@n,@u,@q,@q,@pp,@mq,@cid,@tok)",
                            P("@n",r["name"]),P("@u",r["unit"]),P("@q",r["quantity"]),
                            P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),
                            P("@cid",cid),P("@tok",tok));
                    else
                        Exec(local,
                            "INSERT INTO ingredient(name,unit,quantity,price_per_unit,min_quantity,central_id,sync_token)" +
                            " VALUES(@n,@u,@q,@pp,@mq,@cid,@tok)",
                            P("@n",r["name"]),P("@u",r["unit"]),P("@q",r["quantity"]),
                            P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]),
                            P("@cid",cid),P("@tok",tok));
                }
                count++;
            }
            return count;
        }

        private static int DlRecipeIngredients(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,food_id,ingredient_id,quantity_per_portion FROM recipe_ingredient");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS(SELECT 1 FROM recipe_ingredient WHERE id=@id)" +
                    " UPDATE recipe_ingredient SET food_id=@fid,ingredient_id=@iid,quantity_per_portion=@qpp WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT recipe_ingredient ON;" +
                    " INSERT INTO recipe_ingredient(id,food_id,ingredient_id,quantity_per_portion) VALUES(@id,@fid,@iid,@qpp);" +
                    " SET IDENTITY_INSERT recipe_ingredient OFF END",
                    P("@id",r["id"]),P("@fid",r["food_id"]),
                    P("@iid",r["ingredient_id"]),P("@qpp",r["quantity_per_portion"]));
                count++;
            }
            return count;
        }

        // ── Ingredient miqdorlarini markaziy serverga yozish (delta sync) ───
        // FIX: To'liq overwrite o'rniga delta (farq) qo'llanadi.
        // synced_qty = oxirgi sync da bo'lgan miqdor.
        // delta = joriy_qty - synced_qty → central ga qo'shiladi/ayiriladi.
        private static int SyncIngredientQuantities(SqlConnection local, SqlConnection central)
        {
            // synced_qty yo'q bo'lsa delta 0 — xavfsiz skip
            bool hasSyncedQty;
            using (var chk = new SqlCommand(
                "SELECT COUNT(1) FROM sys.columns WHERE object_id=OBJECT_ID('ingredient') AND name='synced_qty'", local))
                hasSyncedQty = Convert.ToInt32(chk.ExecuteScalar()) > 0;
            if (!hasSyncedQty) return 0;

            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT id, quantity, ISNULL(synced_qty, quantity) AS synced_qty, " +
                "price_per_unit, min_quantity, central_id FROM ingredient");

            foreach (DataRow r in rows.Rows)
            {
                int     localId   = Convert.ToInt32(r["id"]);
                decimal localQty  = Convert.ToDecimal(r["quantity"]);
                decimal syncedQty = Convert.ToDecimal(r["synced_qty"]);
                decimal delta     = localQty - syncedQty;

                // central_id mavjud bo'lmasa — hali sync bo'lmagan, skip
                if (r["central_id"] == DBNull.Value) continue;
                int centralId = Convert.ToInt32(r["central_id"]);

                int? exists = ScalarOrNull(central,
                    "SELECT id FROM ingredient WHERE id=@id", "@id", centralId);

                if (exists != null)
                {
                    // Delta ni centraldagi miqdorga qo'shamiz, 0 dan past ketmasin
                    Exec(central,
                        "UPDATE ingredient SET " +
                        "  quantity = CASE WHEN quantity + @d < 0 THEN 0 ELSE quantity + @d END, " +
                        "  price_per_unit=@pp, min_quantity=@mq " +
                        "WHERE id=@cid",
                        P("@d",  delta),
                        P("@pp", r["price_per_unit"]),
                        P("@mq", r["min_quantity"]),
                        P("@cid", centralId));

                    // Lokal synced_qty ni joriy quantity ga tenglaymiz
                    Exec(local,
                        "UPDATE ingredient SET synced_qty=quantity WHERE id=@id",
                        P("@id", localId));
                    count++;
                }
            }
            return count;
        }

        // ── 8. Sozlamalar (local → central) ─────────────────────────────────
        // FIX: Oflayn saqlab qo'yilgan sozlamalar central ga yuborilmay qolardi
        private static int SyncSettings(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT [key], value FROM settings WHERE tenant_id=0");
            foreach (DataRow r in rows.Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM settings WHERE [key]=@k " +
                    "  AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)) " +
                    "  UPDATE settings SET value=@v WHERE [key]=@k " +
                    "    AND tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT) " +
                    "ELSE INSERT INTO settings([key],value,tenant_id) " +
                    "  VALUES(@k,@v,CAST(SESSION_CONTEXT(N'tenant_id') AS INT))",
                    P("@k", r["key"]), P("@v", r["value"]));
                count++;
            }
            return count;
        }

        // ═══════════════════════════════════════════════════════════════════
        // SYNC QUEUE — tez, maqsadli sync
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// SyncQueue dagi kutayotgan yozuvlarni central ga yuboradi.
        /// SyncAll dan farqi: faqat SyncQueue da ko'rsatilgan entitylarni sync qiladi.
        /// </summary>
        public static SyncResult ProcessSyncQueue()
        {
            var result = new SyncResult();
            if (!Session.IsOnline || Session.TenantId == 0) return result;
            if (!Monitor.TryEnter(_syncLock)) return result;

            dbconnect.FixLocalDefaults();

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                // Eng ko'pi 50 ta — tez bajarish uchun
                DataTable queue = ReadAll(local,
                    "SELECT TOP 50 Id, EntityName, EntityId, ActionType " +
                    "FROM SyncQueue WHERE IsSynced=0 AND RetryCount < 5 " +
                    "ORDER BY CreatedAt");

                foreach (DataRow row in queue.Rows)
                {
                    int    qId       = Convert.ToInt32(row["Id"]);
                    string entity    = row["EntityName"].ToString();
                    int    entityId  = Convert.ToInt32(row["EntityId"]);
                    string action    = row["ActionType"].ToString();

                    try
                    {
                        switch (entity)
                        {
                            case SyncQueueHelper.Orders:
                                SyncSingleOrder(local, central, entityId);
                                break;
                            case SyncQueueHelper.OrderFood:
                                SyncOrderFoodsForOrder(local, central, entityId);
                                break;
                            case SyncQueueHelper.OrderPayments:
                                SyncOrderPaymentsForOrder(local, central, entityId);
                                break;
                            case SyncQueueHelper.OrderDebts:
                                SyncSingleOrderDebt(local, central, entityId);
                                break;
                            case SyncQueueHelper.CashTransactions:
                                SyncSingleCashTransaction(local, central, entityId);
                                break;
                            case SyncQueueHelper.Customers:
                                SyncSingleCustomer(local, central, entityId);
                                break;
                            case SyncQueueHelper.IngredientPurchases:
                                SyncSingleIngredientPurchase(local, central, entityId);
                                break;
                            case SyncQueueHelper.FoodPurchases:
                                SyncSingleFoodPurchase(local, central, entityId);
                                break;
                            case "FoodDelete":
                                // entityId = central_id. order_food da yo'q bo'lsa o'chiradi
                                try {
                                    int fRefs = Convert.ToInt32(ScalarOrNull(central,
                                        "SELECT COUNT(*) FROM order_food WHERE food_id=@id", "@id", entityId) ?? 0);
                                    if (fRefs == 0)
                                        Exec(central, "DELETE FROM food WHERE id=@id", P("@id", entityId));
                                } catch { }
                                break;
                            case "IngredientDelete":
                                // entityId = central_id. bog'liq yozuvlarni ham o'chiradi
                                try {
                                    Exec(central, "DELETE FROM recipe_ingredient WHERE ingredient_id=@id", P("@id", entityId));
                                    Exec(central, "DELETE FROM ingredient_purchase WHERE ingredient_id=@id", P("@id", entityId));
                                    Exec(central, "DELETE FROM ingredient WHERE id=@id", P("@id", entityId));
                                } catch { }
                                break;
                            case "PurchaseDelete":
                                // entityId = central_id of ingredient_purchase
                                try {
                                    var purIngId = ScalarOrNull(central, "SELECT ingredient_id FROM ingredient_purchase WHERE id=@id", "@id", entityId);
                                    var purQty   = ScalarOrNull(central, "SELECT quantity FROM ingredient_purchase WHERE id=@id", "@id", entityId);
                                    if (purIngId != null) {
                                        Exec(central, "DELETE FROM ingredient_purchase WHERE id=@id", P("@id", entityId));
                                        if (purQty != null)
                                            Exec(central, "UPDATE ingredient SET quantity=ISNULL(quantity,0)-@q WHERE id=@iid",
                                                P("@q", Convert.ToDecimal(purQty)), P("@iid", Convert.ToInt32(purIngId)));
                                    }
                                } catch { }
                                break;
                        }

                        Exec(local,
                            "UPDATE SyncQueue SET IsSynced=1, SyncedAt=GETDATE() WHERE Id=@id",
                            P("@id", qId));
                        result.Synced++;
                    }
                    catch (Exception ex)
                    {
                        string msg = ex.Message.Length > 450
                            ? ex.Message.Substring(0, 450) : ex.Message;
                        Exec(local,
                            "UPDATE SyncQueue SET RetryCount=RetryCount+1, ErrorMsg=@e WHERE Id=@id",
                            P("@e", msg), P("@id", qId));
                        result.Errors++;
                        if (result.LastError == null) result.LastError = msg;
                    }
                }
            }
            catch (Exception ex) { result.Errors++; result.LastError = ex.Message; }
            finally
            {
                if (local   != null) { local.Close();   local.Dispose(); }
                if (central != null) { central.Close(); central.Dispose(); }
                Monitor.Exit(_syncLock);
            }
            return result;
        }

        // ── Maqsadli sync metodlar ───────────────────────────────────────────

        private static void SyncSingleOrder(SqlConnection local, SqlConnection central, int localId)
        {
            // BUG FIX: lokal user_id/payment_id/payment2_id → central_id ga mapping qo'shildi
            DataTable rows = ReadAll(local,
                $"SELECT o.*, " +
                $"c.central_id  AS c_central_id, " +
                $"u.central_id  AS u_central_id, " +
                $"p.central_id  AS p_central_id, " +
                $"p2.central_id AS p2_central_id, " +
                $"pi.room_name  AS place_room, " +
                $"po.name       AS place_zone " +
                $"FROM [order] o " +
                $"LEFT JOIN customer c  ON c.id  = o.customer_id " +
                $"LEFT JOIN [user]   u  ON u.id  = o.user_id " +
                $"LEFT JOIN payment  p  ON p.id  = o.payment_id " +
                $"LEFT JOIN payment  p2 ON p2.id = o.payment2_id " +
                $"LEFT JOIN place_in pi ON pi.id = o.place_id " +
                $"LEFT JOIN place_out po ON po.id = pi.place_out_id " +
                $"WHERE o.id={localId}");
            if (rows.Rows.Count == 0) return;

            DataRow r = rows.Rows[0];
            Guid tok = (Guid)r["sync_token"];
            int? centralId = ScalarOrNull(central, "SELECT id FROM [order] WHERE sync_token=@t", "@t", tok);

            // Lokal ID lerni markaziy ID larga map qilamiz
            object centralUserId  = r["u_central_id"]  == DBNull.Value ? r["user_id"]    : r["u_central_id"];
            object centralPayId   = r["p_central_id"]  == DBNull.Value ? r["payment_id"] : r["p_central_id"];
            object centralPay2Id  = r["p2_central_id"] == DBNull.Value ? r["payment2_id"]: r["p2_central_id"];
            object centralCustId  = r["c_central_id"]  == DBNull.Value ? (object)DBNull.Value : r["c_central_id"];

            // place_in markaziy ID ni xona nomi + zona nomi bo'yicha topamiz
            object centralPlaceId = DBNull.Value;
            if (r["place_room"] != DBNull.Value && r["place_zone"] != DBNull.Value)
            {
                try
                {
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT TOP 1 pi.id FROM place_in pi " +
                        "JOIN place_out po ON po.id=pi.place_out_id " +
                        "WHERE pi.room_name=@rn AND po.name=@pn " +
                        "AND pi.tenant_id=CAST(SESSION_CONTEXT(N'tenant_id') AS INT)", central))
                    {
                        cmd.Parameters.AddWithValue("@rn", r["place_room"]);
                        cmd.Parameters.AddWithValue("@pn", r["place_zone"]);
                        var val = cmd.ExecuteScalar();
                        if (val != null && val != DBNull.Value)
                            centralPlaceId = Convert.ToInt32(val);
                    }
                }
                catch { }
            }

            if (centralId == null)
            {
                object inserted = Exec(central,
                    "INSERT INTO [order](user_id,place_id,payment_id,created_at,paid,total," +
                    "  discount_amount,discount_pct,customer_id,customer_name,delivery_phone," +
                    "  delivery_address,is_delivery,order_note,custom_svc_fee,custom_svc_type," +
                    "  payment2_id,payment2_amount,sync_token) " +
                    "OUTPUT INSERTED.id " +
                    "VALUES(@uid,@pid,@pay,@cat,@paid,@tot,@disc,@discp,@cust,@custn," +
                    "  @dph,@dadr,@isdel,@note,@svf,@svt,@p2,@p2a,@tok)",
                    P("@uid",centralUserId),P("@pid",centralPlaceId),P("@pay",centralPayId),
                    P("@cat",r["created_at"]),P("@paid",r["paid"]),P("@tot",r["total"]),
                    P("@disc",r["discount_amount"]),P("@discp",r["discount_pct"]),
                    P("@cust",centralCustId),P("@custn",r["customer_name"]),
                    P("@dph",r["delivery_phone"]),P("@dadr",r["delivery_address"]),
                    P("@isdel",r["is_delivery"]),P("@note",r["order_note"]),
                    P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                    P("@p2",centralPay2Id),P("@p2a",r["payment2_amount"]),P("@tok",tok));
                centralId = Convert.ToInt32(inserted);
            }
            else
            {
                Exec(central,
                    "UPDATE [order] SET paid=@paid,total=@tot,discount_amount=@disc," +
                    "  discount_pct=@discp,payment_id=@pay,custom_svc_fee=@svf," +
                    "  custom_svc_type=@svt,order_note=@note,payment2_id=@p2,payment2_amount=@p2a," +
                    "  customer_id=@cust,customer_name=@custn," +
                    "  delivery_phone=@dph,delivery_address=@dadr,is_delivery=@isdel " +
                    "WHERE id=@cid",
                    P("@paid",r["paid"]),P("@tot",r["total"]),P("@disc",r["discount_amount"]),
                    P("@discp",r["discount_pct"]),P("@pay",centralPayId),
                    P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                    P("@note",r["order_note"]),P("@p2",centralPay2Id),
                    P("@p2a",r["payment2_amount"]),
                    P("@cust",centralCustId),P("@custn",r["customer_name"]),
                    P("@dph",r["delivery_phone"]),P("@dadr",r["delivery_address"]),
                    P("@isdel",r["is_delivery"]),
                    P("@cid",centralId.Value));
            }

            Exec(local, $"UPDATE [order] SET is_synced=1, central_id={centralId} WHERE id={localId}");

            // Order sync bo'lgandan keyin darhol foods ham sync qilamiz
            // SyncQueue tartibiga bog'liq bo'lmay, har doim birga ketsin
            try { SyncOrderFoodsForOrder(local, central, localId); } catch { }
        }

        private static void SyncOrderFoodsForOrder(SqlConnection local, SqlConnection central, int localOrderId)
        {
            int? centralOrderId = ScalarOrNull(local,
                "SELECT central_id FROM [order] WHERE id=@id", "@id", localOrderId);
            if (centralOrderId == null) return;

            // BUG FIX: lokal food_id → food.central_id ga mapping qo'shildi
            DataTable foods = ReadAll(local,
                $"SELECT of2.food_id, f.central_id AS f_central_id, " +
                $"of2.quantity, of2.note, of2.sync_token " +
                $"FROM order_food of2 " +
                $"LEFT JOIN food f ON f.id = of2.food_id " +
                $"WHERE of2.order_id={localOrderId}");

            Exec(central, "DELETE FROM order_food WHERE order_id=@oid", P("@oid", centralOrderId.Value));

            foreach (DataRow f in foods.Rows)
            {
                object centralFoodId = f["f_central_id"] == DBNull.Value ? f["food_id"] : f["f_central_id"];
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM order_food WHERE sync_token=@tok) " +
                    "  UPDATE order_food SET order_id=@oid,food_id=@fid,quantity=@qty,note=@note WHERE sync_token=@tok " +
                    "ELSE " +
                    "  INSERT INTO order_food(order_id,food_id,quantity,note,sync_token) VALUES(@oid,@fid,@qty,@note,@tok)",
                    P("@oid",centralOrderId.Value),P("@fid",centralFoodId),
                    P("@qty",f["quantity"]),P("@note",f["note"]),P("@tok",f["sync_token"]));
            }

            Exec(local, $"UPDATE order_food SET is_synced=1 WHERE order_id={localOrderId}");
        }

        private static void SyncOrderPaymentsForOrder(SqlConnection local, SqlConnection central, int localOrderId)
        {
            int? centralOrderId = ScalarOrNull(local,
                "SELECT central_id FROM [order] WHERE id=@id", "@id", localOrderId);
            if (centralOrderId == null) return;

            // BUG FIX: lokal payment_id → payment.central_id ga mapping qo'shildi
            DataTable payments = ReadAll(local,
                $"SELECT op.*, p.central_id AS p_central_id " +
                $"FROM order_payments op " +
                $"LEFT JOIN payment p ON p.id = op.payment_id " +
                $"WHERE op.order_id={localOrderId} AND op.is_synced=0");
            foreach (DataRow p in payments.Rows)
            {
                object centralPayId = p["p_central_id"] == DBNull.Value ? p["payment_id"] : p["p_central_id"];
                Guid tok = (Guid)p["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM order_payments WHERE sync_token=@t", "@t", tok);
                if (exists == null)
                    Exec(central,
                        "INSERT INTO order_payments(order_id,payment_id,amount,sync_token)" +
                        " VALUES(@oid,@pid,@amt,@tok)",
                        P("@oid",centralOrderId.Value),P("@pid",centralPayId),
                        P("@amt",p["amount"]),P("@tok",tok));
                else
                    Exec(central,
                        "UPDATE order_payments SET amount=@amt WHERE sync_token=@tok",
                        P("@amt",p["amount"]),P("@tok",tok));
                Exec(local, "UPDATE order_payments SET is_synced=1 WHERE id=@id", P("@id",p["id"]));
            }
        }

        private static void SyncSingleOrderDebt(SqlConnection local, SqlConnection central, int localId)
        {
            DataTable rows = ReadAll(local,
                $"SELECT d.*, o.central_id AS o_cid FROM order_debt d " +
                $"JOIN [order] o ON o.id=d.order_id WHERE d.id={localId}");
            if (rows.Rows.Count == 0) return;
            DataRow r = rows.Rows[0];
            if (r["o_cid"] == DBNull.Value) return;

            Guid tok = (Guid)r["sync_token"];
            int? exists = ScalarOrNull(central, "SELECT id FROM order_debt WHERE sync_token=@t", "@t", tok);
            if (exists == null)
                Exec(central,
                    "INSERT INTO order_debt(order_id,debtor_name,debtor_phone,amount,is_paid," +
                    "  paid_at,debt_note,created_at,sync_token)" +
                    " VALUES(@oid,@dn,@dph,@amt,@ip,@pat,@nt,@cat,@tok)",
                    P("@oid",r["o_cid"]),P("@dn",r["debtor_name"]),P("@dph",r["debtor_phone"]),
                    P("@amt",r["amount"]),P("@ip",r["is_paid"]),P("@pat",r["paid_at"]),
                    P("@nt",r["debt_note"]),P("@cat",r["created_at"]),P("@tok",tok));
            else
                Exec(central,
                    "UPDATE order_debt SET is_paid=@ip,paid_at=@pat WHERE sync_token=@tok",
                    P("@ip",r["is_paid"]),P("@pat",r["paid_at"]),P("@tok",tok));

            Exec(local, $"UPDATE order_debt SET is_synced=1 WHERE id={localId}");
        }

        private static void SyncSingleCashTransaction(SqlConnection local, SqlConnection central, int localId)
        {
            DataTable rows = ReadAll(local,
                $"SELECT * FROM cash_transaction WHERE id={localId}");
            if (rows.Rows.Count == 0) return;
            DataRow r = rows.Rows[0];
            Guid tok = (Guid)r["sync_token"];
            if (ScalarOrNull(central, "SELECT id FROM cash_transaction WHERE sync_token=@t", "@t", tok) == null)
                Exec(central,
                    "INSERT INTO cash_transaction(type,category,amount,description,created_by,created_at,sync_token)" +
                    " VALUES(@t,@cat,@amt,@desc,@cb,@crat,@tok)",
                    P("@t",r["type"]),P("@cat",r["category"]),P("@amt",r["amount"]),
                    P("@desc",r["description"]),P("@cb",r["created_by"]),
                    P("@crat",r["created_at"]),P("@tok",tok));
            Exec(local, $"UPDATE cash_transaction SET is_synced=1 WHERE id={localId}");
        }

        private static void SyncSingleCustomer(SqlConnection local, SqlConnection central, int localId)
        {
            DataTable rows = ReadAll(local, $"SELECT * FROM customer WHERE id={localId}");
            if (rows.Rows.Count == 0) return;
            DataRow r = rows.Rows[0];
            Guid tok = (Guid)r["sync_token"];
            int? centralId = ScalarOrNull(central, "SELECT id FROM customer WHERE sync_token=@t", "@t", tok);
            if (centralId == null)
            {
                object inserted = Exec(central,
                    "INSERT INTO customer(name,phone,notes,created_at,sync_token)" +
                    " OUTPUT INSERTED.id VALUES(@n,@ph,@nt,@cat,@tok)",
                    P("@n",r["name"]),P("@ph",r["phone"]),P("@nt",r["notes"]),
                    P("@cat",r["created_at"]),P("@tok",tok));
                centralId = Convert.ToInt32(inserted);
            }
            else
                Exec(central, "UPDATE customer SET name=@n,phone=@ph,notes=@nt WHERE sync_token=@tok",
                    P("@n",r["name"]),P("@ph",r["phone"]),P("@nt",r["notes"]),P("@tok",tok));
            Exec(local, $"UPDATE customer SET is_synced=1, central_id={centralId} WHERE id={localId}");
        }

        private static void SyncSingleIngredientPurchase(SqlConnection local, SqlConnection central, int localId)
        {
            DataTable rows = ReadAll(local, $"SELECT ip.*, i.central_id AS ing_central_id FROM ingredient_purchase ip JOIN ingredient i ON i.id=ip.ingredient_id WHERE ip.id={localId}");
            if (rows.Rows.Count == 0) return;
            DataRow r = rows.Rows[0];
            if (r["sync_token"] == DBNull.Value) return;
            Guid tok = (Guid)r["sync_token"];
            var ingCentralId = r["ing_central_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["ing_central_id"]);
            if (ingCentralId == null) return; // ingredient henuz sync bo'lmagan, keyinroq urinadi
            var existId = ScalarOrNull(central, "SELECT id FROM ingredient_purchase WHERE sync_token=@t", "@t", tok);
            if (existId == null)
                Exec(central,
                    "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token)" +
                    " VALUES(@iid,@qty,@pp,@tp,@pa,@n,@tok)",
                    P("@iid",ingCentralId.Value),P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),
                    P("@tp",r["total_price"]),P("@pa",r["purchased_at"]),P("@n",r["notes"]),P("@tok",tok));
            else
                Exec(central,
                    "UPDATE ingredient_purchase SET quantity=@qty,price_per_unit=@pp,total_price=@tp,notes=@n WHERE sync_token=@tok",
                    P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),P("@tp",r["total_price"]),P("@n",r["notes"]),P("@tok",tok));
            Exec(local, $"UPDATE ingredient_purchase SET is_synced=1 WHERE id={localId}");
        }

        private static void SyncSingleFoodPurchase(SqlConnection local, SqlConnection central, int localId)
        {
            DataTable rows = ReadAll(local, $"SELECT * FROM food_purchase WHERE id={localId}");
            if (rows.Rows.Count == 0) return;
            DataRow r = rows.Rows[0];
            Guid tok = (Guid)r["sync_token"];
            if (ScalarOrNull(central, "SELECT id FROM food_purchase WHERE sync_token=@t", "@t", tok) == null)
                Exec(central,
                    "INSERT INTO food_purchase(food_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token)" +
                    " VALUES(@fid,@qty,@pp,@tp,@pa,@n,@tok)",
                    P("@fid",r["food_id"]),P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),
                    P("@tp",r["total_price"]),P("@pa",r["purchased_at"]),P("@n",r["notes"]),P("@tok",tok));
            Exec(local, $"UPDATE food_purchase SET is_synced=1 WHERE id={localId}");
        }

        // ═══════════════════════════════════════════════════════════════════
        // YANGI DL METODLAR — tranzaksiya ma'lumotlari
        // ═══════════════════════════════════════════════════════════════════

        private static int DlCustomers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // Central API: name, phone, notes — email/address yo'q
            DataTable rows = ReadAll(central,
                "SELECT id, ISNULL(name,'') AS name, ISNULL(phone,'') AS phone, " +
                "  ISNULL(notes,'') AS notes, ISNULL(created_at,GETDATE()) AS created_at," +
                "  ISNULL(sync_token,NEWID()) AS sync_token " +
                "FROM customer");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM customer WHERE central_id=@c", "@c", cid)
                        ?? ScalarOrNull(local, "SELECT id FROM customer WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE customer SET name=@n,phone=@ph,notes=@nt,central_id=@cid,is_synced=1 WHERE id=@id",
                        P("@n",r["name"]),P("@ph",r["phone"]),P("@nt",r["notes"]),
                        P("@cid",cid),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT customer ON;" +
                        "INSERT INTO customer(id,name,phone,notes,created_at,sync_token,is_synced,central_id)" +
                        " VALUES(@id,@n,@ph,@nt,@cat,@tok,1,@id);" +
                        "SET IDENTITY_INSERT customer OFF",
                        P("@id",cid),P("@n",r["name"]),P("@ph",r["phone"]),P("@nt",r["notes"]),
                        P("@cat",r["created_at"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlOrders(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, user_id, ISNULL(place_id,0) AS place_id, payment_id, created_at, paid," +
                "  ISNULL(total,0) AS total," +
                "  ISNULL(discount_amount,0) AS discount_amount," +
                "  ISNULL(discount_pct,0) AS discount_pct," +
                "  customer_id, ISNULL(customer_name,'') AS customer_name," +
                "  ISNULL(delivery_phone,'') AS delivery_phone," +
                "  ISNULL(delivery_address,'') AS delivery_address," +
                "  ISNULL(is_delivery,0) AS is_delivery," +
                "  ISNULL(order_note,'') AS order_note," +
                "  ISNULL(custom_svc_fee,0) AS custom_svc_fee," +
                "  ISNULL(custom_svc_type,'pct') AS custom_svc_type," +
                "  payment2_id, ISNULL(payment2_amount,0) AS payment2_amount," +
                "  ISNULL(sync_token,NEWID()) AS sync_token" +
                " FROM [order]" +
                " WHERE created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM [order] WHERE central_id=@c", "@c", cid)
                        ?? ScalarOrNull(local, "SELECT id FROM [order] WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE [order] SET paid=@paid,total=@tot,discount_amount=@disc," +
                        "  discount_pct=@discp,payment_id=@pay,custom_svc_fee=@svf," +
                        "  custom_svc_type=@svt,payment2_id=@p2,payment2_amount=@p2a," +
                        "  order_note=@note,central_id=@cid,is_synced=1 WHERE id=@id",
                        P("@paid",r["paid"]),P("@tot",r["total"]),P("@disc",r["discount_amount"]),
                        P("@discp",r["discount_pct"]),P("@pay",r["payment_id"]),
                        P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                        P("@p2",r["payment2_id"]),P("@p2a",r["payment2_amount"]),
                        P("@note",r["order_note"]),P("@cid",cid),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT [order] ON;" +
                        "INSERT INTO [order](id,user_id,place_id,payment_id,created_at,paid,total," +
                        "  discount_amount,discount_pct,customer_id,customer_name,delivery_phone," +
                        "  delivery_address,is_delivery,order_note,custom_svc_fee,custom_svc_type," +
                        "  payment2_id,payment2_amount,sync_token,is_synced,central_id)" +
                        " VALUES(@id,@uid,@plid,@pay,@cat,@paid,@tot,@disc,@discp,@cust,@custn," +
                        "  @dph,@dadr,@isdel,@note,@svf,@svt,@p2,@p2a,@tok,1,@id);" +
                        "SET IDENTITY_INSERT [order] OFF",
                        P("@id",cid),P("@uid",r["user_id"]),P("@plid",r["place_id"]),
                        P("@pay",r["payment_id"]),P("@cat",r["created_at"]),
                        P("@paid",r["paid"]),P("@tot",r["total"]),
                        P("@disc",r["discount_amount"]),P("@discp",r["discount_pct"]),
                        P("@cust",r["customer_id"]),P("@custn",r["customer_name"]),
                        P("@dph",r["delivery_phone"]),P("@dadr",r["delivery_address"]),
                        P("@isdel",r["is_delivery"]),P("@note",r["order_note"]),
                        P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                        P("@p2",r["payment2_id"]),P("@p2a",r["payment2_amount"]),
                        P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlOrderFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT f.id, f.order_id, f.food_id, f.quantity," +
                "  ISNULL(f.note,'') AS note, f.sync_token" +
                " FROM order_food f" +
                " JOIN [order] o ON o.id=f.order_id" +
                " WHERE o.created_at >= DATEADD(YEAR,-1,GETDATE())" +
                "   AND f.sync_token IS NOT NULL");
            foreach (DataRow r in rows.Rows)
            {
                int cid     = Convert.ToInt32(r["id"]);
                Guid tok    = (Guid)r["sync_token"];
                int orderId = Convert.ToInt32(r["order_id"]);
                int? orderLid = ScalarOrNull(local,
                    "SELECT id FROM [order] WHERE central_id=@c OR id=@c", "@c", orderId);
                if (orderLid == null) continue;

                // sync_token bo'yicha tekshir (lokal ID != central ID bo'lishi mumkin)
                int? lid = ScalarOrNull(local, "SELECT id FROM order_food WHERE sync_token=@c", "@c", tok);
                if (lid == null)
                    lid = ScalarOrNull(local, "SELECT id FROM order_food WHERE id=@c", "@c", cid);

                if (lid != null)
                    Exec(local,
                        "UPDATE order_food SET food_id=@fid,quantity=@qty,note=@note,is_synced=1 WHERE id=@id",
                        P("@fid",r["food_id"]),P("@qty",r["quantity"]),
                        P("@note",r["note"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT order_food ON;" +
                        "INSERT INTO order_food(id,order_id,food_id,quantity,note,sync_token,is_synced)" +
                        " VALUES(@id,@oid,@fid,@qty,@note,@tok,1);" +
                        "SET IDENTITY_INSERT order_food OFF",
                        P("@id",cid),P("@oid",orderLid.Value),P("@fid",r["food_id"]),
                        P("@qty",r["quantity"]),P("@note",r["note"]),P("@tok",tok));
                count++;
            }
            return count;
        }

        private static int DlOrderPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT op.id, op.order_id, op.payment_id, op.amount," +
                "  ISNULL(op.sync_token,NEWID()) AS sync_token" +
                " FROM order_payments op" +
                " JOIN [order] o ON o.id=op.order_id" +
                " WHERE o.created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int orderId = Convert.ToInt32(r["order_id"]);
                int? orderLid = ScalarOrNull(local, "SELECT id FROM [order] WHERE id=@c", "@c", orderId);
                if (orderLid == null) continue;

                int? lid = ScalarOrNull(local, "SELECT id FROM order_payments WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE order_payments SET payment_id=@pid,amount=@amt,is_synced=1 WHERE id=@id",
                        P("@pid",r["payment_id"]),P("@amt",r["amount"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT order_payments ON;" +
                        "INSERT INTO order_payments(id,order_id,payment_id,amount,sync_token,is_synced)" +
                        " VALUES(@id,@oid,@pid,@amt,@tok,1);" +
                        "SET IDENTITY_INSERT order_payments OFF",
                        P("@id",cid),P("@oid",orderId),P("@pid",r["payment_id"]),
                        P("@amt",r["amount"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlOrderDebts(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT d.id, d.order_id," +
                "  ISNULL(d.debtor_name,'') AS debtor_name, ISNULL(d.debtor_phone,'') AS debtor_phone," +
                "  ISNULL(d.amount,0) AS amount, ISNULL(d.is_paid,0) AS is_paid," +
                "  d.paid_at, ISNULL(d.debt_note,'') AS debt_note," +
                "  ISNULL(d.created_at,GETDATE()) AS created_at," +
                "  ISNULL(d.sync_token,NEWID()) AS sync_token" +
                " FROM order_debt d" +
                " JOIN [order] o ON o.id=d.order_id" +
                " WHERE o.created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int orderId = Convert.ToInt32(r["order_id"]);
                int? orderLid = ScalarOrNull(local, "SELECT id FROM [order] WHERE id=@c", "@c", orderId);
                if (orderLid == null) continue;

                int? lid = ScalarOrNull(local, "SELECT id FROM order_debt WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE order_debt SET debtor_name=@dn,debtor_phone=@dph,amount=@amt," +
                        "  is_paid=@ip,paid_at=@pat,debt_note=@nt,is_synced=1 WHERE id=@id",
                        P("@dn",r["debtor_name"]),P("@dph",r["debtor_phone"]),
                        P("@amt",r["amount"]),P("@ip",r["is_paid"]),P("@pat",r["paid_at"]),
                        P("@nt",r["debt_note"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT order_debt ON;" +
                        "INSERT INTO order_debt(id,order_id,debtor_name,debtor_phone,amount,is_paid," +
                        "  paid_at,debt_note,created_at,sync_token,is_synced)" +
                        " VALUES(@id,@oid,@dn,@dph,@amt,@ip,@pat,@nt,@cat,@tok,1);" +
                        "SET IDENTITY_INSERT order_debt OFF",
                        P("@id",cid),P("@oid",orderId),P("@dn",r["debtor_name"]),
                        P("@dph",r["debtor_phone"]),P("@amt",r["amount"]),P("@ip",r["is_paid"]),
                        P("@pat",r["paid_at"]),P("@nt",r["debt_note"]),P("@cat",r["created_at"]),
                        P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlCancellationLogs(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT l.id, l.order_id, l.food_id, ISNULL(l.food_name,'') AS food_name," +
                "  ISNULL(l.food_category,'') AS food_category, ISNULL(l.cancelled_qty,0) AS cancelled_qty," +
                "  ISNULL(l.cancelled_by,'') AS cancelled_by," +
                "  ISNULL(l.cancelled_at,GETDATE()) AS cancelled_at," +
                "  ISNULL(l.sync_token,NEWID()) AS sync_token" +
                " FROM order_cancellation_log l" +
                " JOIN [order] o ON o.id=l.order_id" +
                " WHERE o.created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int orderId = Convert.ToInt32(r["order_id"]);
                int? orderLid = ScalarOrNull(local, "SELECT id FROM [order] WHERE id=@c", "@c", orderId);
                if (orderLid == null) continue;

                int? lid = ScalarOrNull(local, "SELECT id FROM order_cancellation_log WHERE id=@c", "@c", cid);
                if (lid == null)
                    Exec(local,
                        "SET IDENTITY_INSERT order_cancellation_log ON;" +
                        "INSERT INTO order_cancellation_log(id,order_id,food_id,food_name,food_category," +
                        "  cancelled_qty,cancelled_by,cancelled_at,sync_token,is_synced)" +
                        " VALUES(@id,@oid,@fid,@fn,@fc,@qty,@by,@at,@tok,1);" +
                        "SET IDENTITY_INSERT order_cancellation_log OFF",
                        P("@id",cid),P("@oid",orderId),P("@fid",r["food_id"]),
                        P("@fn",r["food_name"]),P("@fc",r["food_category"]),
                        P("@qty",r["cancelled_qty"]),P("@by",r["cancelled_by"]),
                        P("@at",r["cancelled_at"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlCashTransactions(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // Local schema: type, category, amount, description, created_by, created_at, sync_token
            DataTable rows = ReadAll(central,
                "SELECT id, ISNULL(type,'') AS type, ISNULL(category,'') AS category," +
                "  ISNULL(amount,0) AS amount, ISNULL(description,'') AS description," +
                "  created_by, ISNULL(created_at,GETDATE()) AS created_at," +
                "  ISNULL(sync_token,NEWID()) AS sync_token" +
                " FROM cash_transaction" +
                " WHERE created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM cash_transaction WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE cash_transaction SET type=@t,category=@cat2,amount=@amt," +
                        "  description=@desc,created_by=@cb,is_synced=1 WHERE id=@id",
                        P("@t",r["type"]),P("@cat2",r["category"]),P("@amt",r["amount"]),
                        P("@desc",r["description"]),P("@cb",r["created_by"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT cash_transaction ON;" +
                        "INSERT INTO cash_transaction(id,type,category,amount,description,created_by,created_at,sync_token,is_synced)" +
                        " VALUES(@id,@t,@cat2,@amt,@desc,@cb,@cat,@tok,1);" +
                        "SET IDENTITY_INSERT cash_transaction OFF",
                        P("@id",cid),P("@t",r["type"]),P("@cat2",r["category"]),P("@amt",r["amount"]),
                        P("@desc",r["description"]),P("@cb",r["created_by"]),
                        P("@cat",r["created_at"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlIngredientPurchases(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, ingredient_id, ISNULL(quantity,0) AS quantity," +
                "  ISNULL(price_per_unit,0) AS price_per_unit, ISNULL(total_price,0) AS total_price," +
                "  ISNULL(purchased_at,GETDATE()) AS purchased_at," +
                "  ISNULL(notes,'') AS notes, ISNULL(sync_token,NEWID()) AS sync_token" +
                " FROM ingredient_purchase" +
                " WHERE purchased_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int centralIngId = Convert.ToInt32(r["ingredient_id"]);
                Guid tok = (Guid)r["sync_token"];

                // ingredient_id ni lokal IDga moslashtirish
                int? localIngId = ScalarOrNull(local,
                    "SELECT id FROM ingredient WHERE central_id=@c", "@c", centralIngId);
                if (localIngId == null) continue; // lokal da bu ingredient yo'q, skip

                // sync_token bo'yicha qidirish
                int? lid = ScalarOrNull(local,
                    "SELECT id FROM ingredient_purchase WHERE sync_token=@t", "@t", tok);

                if (lid != null)
                    Exec(local,
                        "UPDATE ingredient_purchase SET quantity=@qty,price_per_unit=@pp," +
                        "  total_price=@tp,notes=@nt,is_synced=1 WHERE id=@id",
                        P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),
                        P("@tp",r["total_price"]),P("@nt",r["notes"]),P("@id",lid.Value));
                else
                    // IDENTITY_INSERT yo'q — lokal ingredient_id ishlatiladi, central_id saqlanadi
                    Exec(local,
                        "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit," +
                        "  total_price,purchased_at,notes,sync_token,is_synced)" +
                        " VALUES(@iid,@qty,@pp,@tp,@pat,@nt,@tok,1)",
                        P("@iid",localIngId.Value),P("@qty",r["quantity"]),
                        P("@pp",r["price_per_unit"]),P("@tp",r["total_price"]),
                        P("@pat",r["purchased_at"]),P("@nt",r["notes"]),P("@tok",tok));
                count++;
            }
            return count;
        }

        private static int DlFoodPurchases(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, food_id, ISNULL(quantity,0) AS quantity," +
                "  ISNULL(price_per_unit,0) AS price_per_unit, ISNULL(total_price,0) AS total_price," +
                "  ISNULL(purchased_at,GETDATE()) AS purchased_at," +
                "  ISNULL(notes,'') AS notes, ISNULL(sync_token,NEWID()) AS sync_token" +
                " FROM food_purchase" +
                " WHERE purchased_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM food_purchase WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE food_purchase SET quantity=@qty,price_per_unit=@pp," +
                        "  total_price=@tp,notes=@nt,is_synced=1 WHERE id=@id",
                        P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),
                        P("@tp",r["total_price"]),P("@nt",r["notes"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT food_purchase ON;" +
                        "INSERT INTO food_purchase(id,food_id,quantity,price_per_unit," +
                        "  total_price,purchased_at,notes,sync_token,is_synced)" +
                        " VALUES(@id,@fid,@qty,@pp,@tp,@pat,@nt,@tok,1);" +
                        "SET IDENTITY_INSERT food_purchase OFF",
                        P("@id",cid),P("@fid",r["food_id"]),P("@qty",r["quantity"]),
                        P("@pp",r["price_per_unit"]),P("@tp",r["total_price"]),
                        P("@pat",r["purchased_at"]),P("@nt",r["notes"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        // ═══════════════════════════════════════════════════════════════════
        // TEZKOR DOWNLOAD — faqat aktiv buyurtmalar (3-5 soniyada bir)
        // Mobil ilovadan kelgan zakaslami WinFormda darhol ko'rsatish uchun
        // ═══════════════════════════════════════════════════════════════════
        public static SyncResult DownloadOrdersFast()
        {
            var result = new SyncResult();
            if (!Session.IsOnline || Session.TenantId == 0) return result;

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                // 1. Bugungi va kechagi aktiv buyurtmalar
                TryDl(() => DlOrdersFast(local, central), result);
                // 2. Ular uchun taomlar ro'yxati
                TryDl(() => DlOrderFoodsFast(local, central), result);
                // 3. Stol holati (band/bo'sh)
                TryDl(() => DlPlaceIns(local, central), result);
            }
            catch (Exception ex) { result.Errors++; result.LastError = ex.Message; }
            finally
            {
                if (local   != null) { local.Close();   local.Dispose(); }
                if (central != null) { central.Close(); central.Dispose(); }
            }
            return result;
        }

        // Faqat to'lanmagan + so'nggi 48 soat ichidagi buyurtmalar
        private static int DlOrdersFast(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, user_id, ISNULL(place_id,0) AS place_id, payment_id, created_at, paid," +
                "  ISNULL(total,0) AS total," +
                "  ISNULL(discount_amount,0) AS discount_amount," +
                "  ISNULL(discount_pct,0) AS discount_pct," +
                "  customer_id, ISNULL(customer_name,'') AS customer_name," +
                "  ISNULL(delivery_phone,'') AS delivery_phone," +
                "  ISNULL(delivery_address,'') AS delivery_address," +
                "  ISNULL(is_delivery,0) AS is_delivery," +
                "  ISNULL(order_note,'') AS order_note," +
                "  ISNULL(custom_svc_fee,0) AS custom_svc_fee," +
                "  ISNULL(custom_svc_type,'pct') AS custom_svc_type," +
                "  payment2_id, ISNULL(payment2_amount,0) AS payment2_amount," +
                "  ISNULL(sync_token,NEWID()) AS sync_token" +
                " FROM [order]" +
                " WHERE paid='NO' OR created_at >= DATEADD(HOUR,-48,GETDATE())");

            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                // FAQAT central_id bo'yicha qidiramiz — id bo'yicha emas (xavfsiz)
                int? lid = ScalarOrNull(local,
                    "SELECT id FROM [order] WHERE central_id=@c", "@c", cid);

                if (lid != null)
                {
                    Exec(local,
                        "UPDATE [order] SET paid=@paid,total=@tot,discount_amount=@disc," +
                        "  discount_pct=@discp,payment_id=@pay,custom_svc_fee=@svf," +
                        "  custom_svc_type=@svt,payment2_id=@p2,payment2_amount=@p2a," +
                        "  order_note=@note,is_synced=1 WHERE id=@id",
                        P("@paid",r["paid"]),P("@tot",r["total"]),P("@disc",r["discount_amount"]),
                        P("@discp",r["discount_pct"]),P("@pay",r["payment_id"]),
                        P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                        P("@p2",r["payment2_id"]),P("@p2a",r["payment2_amount"]),
                        P("@note",r["order_note"]),P("@id",lid.Value));
                }
                else
                {
                    // Yangi buyurtma (mobil yoki boshqa qurilmadan) — INSERT
                    try
                    {
                        Exec(local,
                            "SET IDENTITY_INSERT [order] ON;" +
                            "INSERT INTO [order](id,user_id,place_id,payment_id,created_at,paid,total," +
                            "  discount_amount,discount_pct,customer_id,customer_name,delivery_phone," +
                            "  delivery_address,is_delivery,order_note,custom_svc_fee,custom_svc_type," +
                            "  payment2_id,payment2_amount,sync_token,is_synced,central_id)" +
                            " VALUES(@id,@uid,@plid,@pay,@cat,@paid,@tot,@disc,@discp,@cust,@custn," +
                            "  @dph,@dadr,@isdel,@note,@svf,@svt,@p2,@p2a,@tok,1,@id);" +
                            "SET IDENTITY_INSERT [order] OFF",
                            P("@id",cid),P("@uid",r["user_id"]),P("@plid",r["place_id"]),
                            P("@pay",r["payment_id"]),P("@cat",r["created_at"]),
                            P("@paid",r["paid"]),P("@tot",r["total"]),
                            P("@disc",r["discount_amount"]),P("@discp",r["discount_pct"]),
                            P("@cust",r["customer_id"]),P("@custn",r["customer_name"]),
                            P("@dph",r["delivery_phone"]),P("@dadr",r["delivery_address"]),
                            P("@isdel",r["is_delivery"]),P("@note",r["order_note"]),
                            P("@svf",r["custom_svc_fee"]),P("@svt",r["custom_svc_type"]),
                            P("@p2",r["payment2_id"]),P("@p2a",r["payment2_amount"]),
                            P("@tok",r["sync_token"]));
                    }
                    catch { /* ID konflikt bo'lsa o'tkazib yuboramiz */ }
                }
                count++;
            }
            return count;
        }

        // Aktiv buyurtmalar uchun order_food ni yangilash
        private static int DlOrderFoodsFast(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT f.id, f.order_id, f.food_id, f.quantity," +
                "  ISNULL(f.note,'') AS note, f.sync_token" +
                " FROM order_food f" +
                " JOIN [order] o ON o.id=f.order_id" +
                " WHERE (o.paid='NO' OR o.created_at >= DATEADD(HOUR,-48,GETDATE()))" +
                "   AND f.sync_token IS NOT NULL");

            foreach (DataRow r in rows.Rows)
            {
                int cid     = Convert.ToInt32(r["id"]);
                Guid tok    = (Guid)r["sync_token"];
                int orderId = Convert.ToInt32(r["order_id"]);

                int? orderLid = ScalarOrNull(local,
                    "SELECT id FROM [order] WHERE central_id=@c OR id=@c", "@c", orderId);
                if (orderLid == null) continue;

                // sync_token bo'yicha tekshir — lokal ID != central ID bo'lishi mumkin
                int? lid = ScalarOrNull(local, "SELECT id FROM order_food WHERE sync_token=@c", "@c", tok);
                if (lid == null)
                    lid = ScalarOrNull(local, "SELECT id FROM order_food WHERE id=@c", "@c", cid);

                if (lid != null)
                    Exec(local,
                        "UPDATE order_food SET food_id=@fid,quantity=@qty,note=@note,is_synced=1 WHERE id=@id",
                        P("@fid",r["food_id"]),P("@qty",r["quantity"]),
                        P("@note",r["note"]),P("@id",lid.Value));
                else
                {
                    try
                    {
                        Exec(local,
                            "SET IDENTITY_INSERT order_food ON;" +
                            "INSERT INTO order_food(id,order_id,food_id,quantity,note,sync_token,is_synced)" +
                            " VALUES(@id,@oid,@fid,@qty,@note,@tok,1);" +
                            "SET IDENTITY_INSERT order_food OFF",
                            P("@id",cid),P("@oid",orderLid.Value),P("@fid",r["food_id"]),
                            P("@qty",r["quantity"]),P("@note",r["note"]),P("@tok",tok));
                    }
                    catch { }
                }
                count++;
            }
            return count;
        }

        // ── ADO.NET yordamchilar ─────────────────────────────────────────────
        private static DataTable ReadAll(SqlConnection conn, string sql)
        {
            var dt = new DataTable();
            using (var da = new SqlDataAdapter(new SqlCommand(sql, conn)))
                da.Fill(dt);
            return dt;
        }

        private static object Exec(SqlConnection conn, string sql, params SqlParameter[] prms)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                foreach (var p in prms) cmd.Parameters.Add(p);
                return sql.Contains("OUTPUT") ? cmd.ExecuteScalar() : (object)cmd.ExecuteNonQuery();
            }
        }

        private static int? ScalarOrNull(SqlConnection conn, string sql, string pName, object pVal)
        {
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(pName, pVal);
                object v = cmd.ExecuteScalar();
                return (v == null || v == DBNull.Value) ? (int?)null : Convert.ToInt32(v);
            }
        }

        private static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? (object)DBNull.Value);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SyncQueueHelper — har bir write operatsiyasidan keyin chaqiriladi
    // ═══════════════════════════════════════════════════════════════════════
    public static class SyncQueueHelper
    {
        public const string Orders              = "Orders";
        public const string OrderFood           = "OrderFood";
        public const string OrderPayments       = "OrderPayments";
        public const string OrderDebts          = "OrderDebts";
        public const string CancellationLogs    = "CancellationLogs";
        public const string CashTransactions    = "CashTransactions";
        public const string Customers           = "Customers";
        public const string IngredientPurchases = "IngredientPurchases";
        public const string FoodPurchases       = "FoodPurchases";

        public static void Add(string entityName, int entityId,
                               string actionType = "Insert")
        {
            EnsureTable();
            try
            {
                using (var conn = dbconnect.OpenLocalForSync())
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    "INSERT INTO SyncQueue(EntityName,EntityId,ActionType,IsSynced,CreatedAt)" +
                    " VALUES(@en,@eid,@at,0,GETDATE())", conn))
                {
                    cmd.Parameters.AddWithValue("@en",  entityName);
                    cmd.Parameters.AddWithValue("@eid", entityId);
                    cmd.Parameters.AddWithValue("@at",  actionType);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
            // Task.Run try-catch TASHQARISIDA — har holda chaqiriladi
            if (Session.IsOnline && Session.TenantId > 0)
                System.Threading.Tasks.Task.Run(() => SyncEngine.ProcessSyncQueue());
        }

        public static void AddBatch(
            params (string entity, int id, string action)[] items)
        {
            EnsureTable();
            try
            {
                using (var conn = dbconnect.OpenLocalForSync())
                {
                    foreach (var (entity, id, action) in items)
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "INSERT INTO SyncQueue(EntityName,EntityId,ActionType,IsSynced,CreatedAt)" +
                        " VALUES(@en,@eid,@at,0,GETDATE())", conn))
                    {
                        cmd.Parameters.AddWithValue("@en",  entity);
                        cmd.Parameters.AddWithValue("@eid", id);
                        cmd.Parameters.AddWithValue("@at",  action);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
            // Task.Run try-catch TASHQARISIDA — har holda chaqiriladi
            if (Session.IsOnline && Session.TenantId > 0)
                System.Threading.Tasks.Task.Run(() => SyncEngine.ProcessSyncQueue());
        }

        // SyncQueue jadvali mavjudligini kafolatlaydi
        private static void EnsureTable()
        {
            try
            {
                using (var conn = dbconnect.OpenLocalForSync())
                using (var cmd = new System.Data.SqlClient.SqlCommand(@"
                    IF OBJECT_ID('dbo.SyncQueue','U') IS NULL
                    CREATE TABLE SyncQueue (
                        Id         INT IDENTITY(1,1) PRIMARY KEY,
                        EntityName NVARCHAR(100) NOT NULL,
                        EntityId   INT           NOT NULL,
                        ActionType NVARCHAR(20)  NOT NULL DEFAULT 'Insert',
                        IsSynced   BIT           NOT NULL DEFAULT 0,
                        CreatedAt  DATETIME      NOT NULL DEFAULT GETDATE(),
                        SyncedAt   DATETIME      NULL,
                        RetryCount INT           NOT NULL DEFAULT 0,
                        ErrorMsg   NVARCHAR(500) NULL
                    )", conn))
                    cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}
