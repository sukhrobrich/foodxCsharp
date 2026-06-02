using System;
using System.Data;
using System.Data.SqlClient;

namespace WindowsFormsApp1.services
{
    internal static class SyncEngine
    {
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

        // ── REFERENS JADVALLAR: local → central (id bo'yicha) ────────────────

        private static int SyncRefUserCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name,role_type,color FROM user_category").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM user_category WHERE id=@id)" +
                    " UPDATE user_category SET name=@n,role_type=@rt,color=@c WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT user_category ON;" +
                    " INSERT INTO user_category(id,name,role_type,color) VALUES(@id,@n,@rt,@c);" +
                    " SET IDENTITY_INSERT user_category OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@rt",r["role_type"]),P("@c",r["color"]));
                count++;
            }
            return count;
        }

        private static int SyncRefUsers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,name,user_category_id,login,password,app_password,phone_number,created_at,updated_at,sort_order FROM [user]").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM [user] WHERE id=@id)" +
                    " UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw,app_password=@ap," +
                    "   phone_number=@ph,updated_at=@ua,sort_order=@so WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT [user] ON;" +
                    " INSERT INTO [user](id,name,user_category_id,login,password,app_password,phone_number,created_at,updated_at,sort_order)" +
                    " VALUES(@id,@n,@uc,@l,@pw,@ap,@ph,@ca,@ua,@so);" +
                    " SET IDENTITY_INSERT [user] OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@uc",r["user_category_id"]),
                    P("@l",r["login"]),P("@pw",r["password"]),P("@ap",r["app_password"]),
                    P("@ph",r["phone_number"]),P("@ca",r["created_at"]),P("@ua",r["updated_at"]),P("@so",r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int SyncRefFoodCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name,printer_name,sort_order FROM food_category").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM food_category WHERE id=@id)" +
                    " UPDATE food_category SET name=@n,printer_name=@pn,sort_order=@so WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT food_category ON;" +
                    " INSERT INTO food_category(id,name,printer_name,sort_order) VALUES(@id,@n,@pn,@so);" +
                    " SET IDENTITY_INSERT food_category OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@pn",r["printer_name"]),P("@so",r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int SyncRefFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,food_category_id,name,count,original_price,selling_price," +
                "photo,created_at,updated_at,unit,description,is_unlimited,sort_order FROM food").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM food WHERE id=@id)" +
                    " UPDATE food SET food_category_id=@fc,name=@n,count=@cnt,original_price=@op," +
                    "   selling_price=@sp,photo=@ph,updated_at=@ua,unit=@u,description=@d," +
                    "   is_unlimited=@iu,sort_order=@so WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT food ON;" +
                    " INSERT INTO food(id,food_category_id,name,count,original_price,selling_price," +
                    "   photo,created_at,updated_at,unit,description,is_unlimited,sort_order)" +
                    " VALUES(@id,@fc,@n,@cnt,@op,@sp,@ph,@ca,@ua,@u,@d,@iu,@so);" +
                    " SET IDENTITY_INSERT food OFF END",
                    P("@id",r["id"]),P("@fc",r["food_category_id"]),P("@n",r["name"]),
                    P("@cnt",r["count"]),P("@op",r["original_price"]),P("@sp",r["selling_price"]),
                    P("@ph",r["photo"]),P("@ca",r["created_at"]),P("@ua",r["updated_at"]),
                    P("@u",r["unit"]),P("@d",r["description"]),
                    P("@iu",r["is_unlimited"]),P("@so",r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int SyncRefPlaceCategories(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name FROM place_category").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM place_category WHERE id=@id)" +
                    " UPDATE place_category SET name=@n WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT place_category ON;" +
                    " INSERT INTO place_category(id,name) VALUES(@id,@n);" +
                    " SET IDENTITY_INSERT place_category OFF END",
                    P("@id",r["id"]),P("@n",r["name"]));
                count++;
            }
            return count;
        }

        private static int SyncRefPlaceOuts(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,place_category_id,name,place_count,created_at,updated_at,serviceFee,price,sort_order FROM place_out").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM place_out WHERE id=@id)" +
                    " UPDATE place_out SET place_category_id=@pc,name=@n,place_count=@cnt," +
                    "   updated_at=@ua,serviceFee=@sf,price=@pr,sort_order=@so WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT place_out ON;" +
                    " INSERT INTO place_out(id,place_category_id,name,place_count,created_at,updated_at,serviceFee,price,sort_order)" +
                    " VALUES(@id,@pc,@n,@cnt,@ca,@ua,@sf,@pr,@so);" +
                    " SET IDENTITY_INSERT place_out OFF END",
                    P("@id",r["id"]),P("@pc",r["place_category_id"]),P("@n",r["name"]),
                    P("@cnt",r["place_count"]),P("@ca",r["created_at"]),P("@ua",r["updated_at"]),
                    P("@sf",r["serviceFee"]),P("@pr",r["price"]),P("@so",r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int SyncRefPlaceIns(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            // FIX: empty va user_id ni sync qilmaymiz — ular real-vaqt holati,
            // mobil yoki online rejim tomonidan boshqariladi. Overwrite qilish
            // faol buyurtmalarni o'chirishi mumkin edi.
            foreach (DataRow r in ReadAll(local,
                "SELECT id,place_out_id,room_name,created_at,price FROM place_in").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM place_in WHERE id=@id)" +
                    " UPDATE place_in SET place_out_id=@po,room_name=@rn,price=@pr WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT place_in ON;" +
                    " INSERT INTO place_in(id,place_out_id,room_name,empty,created_at,price)" +
                    " VALUES(@id,@po,@rn,'YES',@ca,@pr);" +
                    " SET IDENTITY_INSERT place_in OFF END",
                    P("@id",r["id"]),P("@po",r["place_out_id"]),P("@rn",r["room_name"]),
                    P("@ca",r["created_at"]),P("@pr",r["price"]));
                count++;
            }
            return count;
        }

        private static int SyncRefPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local, "SELECT id,name,sort_order FROM payment").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM payment WHERE id=@id)" +
                    " UPDATE payment SET name=@n,sort_order=@so WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT payment ON;" +
                    " INSERT INTO payment(id,name,sort_order) VALUES(@id,@n,@so);" +
                    " SET IDENTITY_INSERT payment OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@so",r["sort_order"]));
                count++;
            }
            return count;
        }

        private static int SyncRefIngredients(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            foreach (DataRow r in ReadAll(local,
                "SELECT id,name,unit,quantity,price_per_unit,min_quantity FROM ingredient").Rows)
            {
                Exec(central,
                    "IF EXISTS(SELECT 1 FROM ingredient WHERE id=@id)" +
                    " UPDATE ingredient SET name=@n,unit=@u,quantity=@q,price_per_unit=@pp,min_quantity=@mq WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT ingredient ON;" +
                    " INSERT INTO ingredient(id,name,unit,quantity,price_per_unit,min_quantity) VALUES(@id,@n,@u,@q,@pp,@mq);" +
                    " SET IDENTITY_INSERT ingredient OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@u",r["unit"]),
                    P("@q",r["quantity"]),P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]));
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
                "SELECT id,ingredient_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token " +
                "FROM ingredient_purchase WHERE is_synced=0").Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                if (ScalarOrNull(central,"SELECT id FROM ingredient_purchase WHERE sync_token=@t","@t",tok) == null)
                    Exec(central,
                        "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit,total_price,purchased_at,notes,sync_token)" +
                        " VALUES(@iid,@q,@pp,@tp,@pa,@n,@tok)",
                        P("@iid",r["ingredient_id"]),P("@q",r["quantity"]),P("@pp",r["price_per_unit"]),
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
                Guid tok = (Guid)r["sync_token"];
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
                "SELECT o.*, c.central_id AS c_central_id " +
                "FROM [order] o " +
                "LEFT JOIN customer c ON c.id = o.customer_id " +
                "WHERE o.is_synced = 0");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];

                int? centralId = ScalarOrNull(central,
                    "SELECT id FROM [order] WHERE sync_token = @t", "@t", tok);

                if (centralId == null)
                {
                    object centralCustId = (r["c_central_id"] == DBNull.Value)
                        ? (object)DBNull.Value
                        : r["c_central_id"];

                    object inserted = Exec(central,
                        "INSERT INTO [order] " +
                        "  (user_id, place_id, payment_id, created_at, paid, total, " +
                        "   discount_amount, discount_pct, customer_id, customer_name, " +
                        "   delivery_phone, delivery_address, is_delivery, order_note, " +
                        "   custom_svc_fee, custom_svc_type, payment2_id, payment2_amount, sync_token) " +
                        "OUTPUT INSERTED.id " +
                        "VALUES (@uid,@pid,@pay,@cat,@paid,@tot,@disc,@discp,@cust,@custn," +
                        "        @dph,@dadr,@isdel,@note,@svf,@svt,@p2,@p2a,@tok)",
                        P("@uid",   r["user_id"]),          P("@pid",   r["place_id"]),
                        P("@pay",   r["payment_id"]),        P("@cat",   r["created_at"]),
                        P("@paid",  r["paid"]),              P("@tot",   r["total"]),
                        P("@disc",  r["discount_amount"]),   P("@discp", r["discount_pct"]),
                        P("@cust",  centralCustId),          P("@custn", r["customer_name"]),
                        P("@dph",   r["delivery_phone"]),    P("@dadr",  r["delivery_address"]),
                        P("@isdel", r["is_delivery"]),       P("@note",  r["order_note"]),
                        P("@svf",   r["custom_svc_fee"]),    P("@svt",   r["custom_svc_type"]),
                        P("@p2",    r["payment2_id"]),        P("@p2a",   r["payment2_amount"]),
                        P("@tok",   tok));
                    centralId = Convert.ToInt32(inserted);
                }
                else
                {
                    // FIX: Mavjud buyurtmani yangilash (avval bu blok yo'q edi)
                    Exec(central,
                        "UPDATE [order] SET " +
                        "  paid=@paid, total=@tot, discount_amount=@disc, discount_pct=@discp, " +
                        "  payment_id=@pay, custom_svc_fee=@svf, custom_svc_type=@svt, " +
                        "  order_note=@note, payment2_id=@p2, payment2_amount=@p2a " +
                        "WHERE id=@cid",
                        P("@paid",  r["paid"]),              P("@tot",   r["total"]),
                        P("@disc",  r["discount_amount"]),   P("@discp", r["discount_pct"]),
                        P("@pay",   r["payment_id"]),         P("@svf",   r["custom_svc_fee"]),
                        P("@svt",   r["custom_svc_type"]),   P("@note",  r["order_note"]),
                        P("@p2",    r["payment2_id"]),        P("@p2a",   r["payment2_amount"]),
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

                // Central dagi eski order_food larni o'chirib tashlaymiz (clean slate)
                Exec(central, "DELETE FROM order_food WHERE order_id=@oid", P("@oid", centralOrderId));

                // Lokal barcha order_food ni qayta yuboramiz (joriy holat)
                DataTable foods = ReadAll(local,
                    $"SELECT food_id, quantity, note, sync_token FROM order_food WHERE order_id={localOrderId}");

                foreach (DataRow f in foods.Rows)
                {
                    Exec(central,
                        "INSERT INTO order_food (order_id, food_id, quantity, note, sync_token) " +
                        "VALUES (@oid, @fid, @qty, @note, @tok)",
                        P("@oid",  centralOrderId), P("@fid",  f["food_id"]),
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
                "SELECT p.*, o.central_id AS order_central_id " +
                "FROM order_payments p " +
                "JOIN [order] o ON o.id = p.order_id " +
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
                        P("@oid", r["order_central_id"]), P("@pid", r["payment_id"]),
                        P("@amt", r["amount"]),            P("@tok", tok));
                else
                    // FIX: Mavjud to'lov yozuvini yangilash
                    Exec(central,
                        "UPDATE order_payments SET payment_id=@pid, amount=@amt WHERE sync_token=@tok",
                        P("@pid", r["payment_id"]), P("@amt", r["amount"]), P("@tok", tok));

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
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM user_category WHERE id=@id) " +
                    "  UPDATE user_category SET name=@n,role_type=@rt,color=@c WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT user_category ON; " +
                    "  INSERT INTO user_category(id,name,role_type,color) VALUES(@id,@n,@rt,@c); " +
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
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM [user] WHERE id=@id) " +
                    "  UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw,app_password=@ap," +
                    "    phone_number=@ph,updated_at=@ua,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT [user] ON; " +
                    "  INSERT INTO [user](id,name,user_category_id,login,password,app_password,phone_number,created_at,updated_at,sort_order) " +
                    "  VALUES(@id,@n,@uc,@l,@pw,@ap,@ph,@ca,@ua,@so); " +
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
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM food_category WHERE id=@id) " +
                    "  UPDATE food_category SET name=@n,printer_name=@pn,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT food_category ON; " +
                    "  INSERT INTO food_category(id,name,printer_name,sort_order) VALUES(@id,@n,@pn,@so); " +
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
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM food WHERE id=@id) " +
                    "  UPDATE food SET food_category_id=@fc,name=@n,count=@cnt,original_price=@op," +
                    "    selling_price=@sp,photo=@ph,updated_at=@ua,unit=@u,description=@d," +
                    "    is_unlimited=@iu,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT food ON; " +
                    "  INSERT INTO food(id,food_category_id,name,count,original_price,selling_price," +
                    "    photo,created_at,updated_at,unit,description,is_unlimited,sort_order) " +
                    "  VALUES(@id,@fc,@n,@cnt,@op,@sp,@ph,@ca,@ua,@u,@d,@iu,@so); " +
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
                    "  UPDATE place_in SET place_out_id=@po,room_name=@rn,empty=@e,user_id=@uid,price=@pr WHERE id=@id " +
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
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id,name,unit,quantity,price_per_unit,min_quantity FROM ingredient");
            foreach (DataRow r in rows.Rows)
            {
                // FIX: synced_qty = yuklab olingan miqdor (delta hisoblash uchun asos)
                Exec(local,
                    "IF EXISTS(SELECT 1 FROM ingredient WHERE id=@id)" +
                    " UPDATE ingredient SET name=@n,unit=@u,quantity=@q,synced_qty=@q," +
                    "   price_per_unit=@pp,min_quantity=@mq WHERE id=@id" +
                    " ELSE BEGIN SET IDENTITY_INSERT ingredient ON;" +
                    " INSERT INTO ingredient(id,name,unit,quantity,synced_qty,price_per_unit,min_quantity)" +
                    "   VALUES(@id,@n,@u,@q,@q,@pp,@mq);" +
                    " SET IDENTITY_INSERT ingredient OFF END",
                    P("@id",r["id"]),P("@n",r["name"]),P("@u",r["unit"]),
                    P("@q",r["quantity"]),P("@pp",r["price_per_unit"]),P("@mq",r["min_quantity"]));
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
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT id, quantity, ISNULL(synced_qty, quantity) AS synced_qty, " +
                "price_per_unit, min_quantity FROM ingredient");

            foreach (DataRow r in rows.Rows)
            {
                int     ingId     = Convert.ToInt32(r["id"]);
                decimal localQty  = Convert.ToDecimal(r["quantity"]);
                decimal syncedQty = Convert.ToDecimal(r["synced_qty"]);
                decimal delta     = localQty - syncedQty;

                int? exists = ScalarOrNull(central,
                    "SELECT id FROM ingredient WHERE id=@id", "@id", ingId);

                if (exists != null)
                {
                    // Delta ni centraldagi miqdorga qo'shamiz, 0 dan past ketmasin
                    Exec(central,
                        "UPDATE ingredient SET " +
                        "  quantity = CASE WHEN quantity + @d < 0 THEN 0 ELSE quantity + @d END, " +
                        "  price_per_unit=@pp, min_quantity=@mq " +
                        "WHERE id=@id",
                        P("@d",  delta),
                        P("@pp", r["price_per_unit"]),
                        P("@mq", r["min_quantity"]),
                        P("@id", ingId));

                    // Lokal synced_qty ni joriy quantity ga tenglaymiz
                    Exec(local,
                        "UPDATE ingredient SET synced_qty=quantity WHERE id=@id",
                        P("@id", ingId));
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
        // YANGI DL METODLAR — tranzaksiya ma'lumotlari
        // ═══════════════════════════════════════════════════════════════════

        private static int DlCustomers(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, ISNULL(name,'') AS name, ISNULL(phone,'') AS phone, " +
                "  ISNULL(email,'') AS email, ISNULL(address,'') AS address, " +
                "  ISNULL(notes,'') AS notes, ISNULL(created_at,GETDATE()) AS created_at, sync_token " +
                "FROM customer");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM customer WHERE central_id=@c", "@c", cid)
                        ?? ScalarOrNull(local, "SELECT id FROM customer WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE customer SET name=@n,phone=@ph,email=@em,address=@adr," +
                        "  notes=@nt,central_id=@cid,is_synced=1 WHERE id=@id",
                        P("@n",r["name"]),P("@ph",r["phone"]),P("@em",r["email"]),
                        P("@adr",r["address"]),P("@nt",r["notes"]),P("@cid",cid),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT customer ON;" +
                        "INSERT INTO customer(id,name,phone,email,address,notes,created_at,sync_token,is_synced,central_id)" +
                        " VALUES(@id,@n,@ph,@em,@adr,@nt,@cat,@tok,1,@id);" +
                        "SET IDENTITY_INSERT customer OFF",
                        P("@id",cid),P("@n",r["name"]),P("@ph",r["phone"]),P("@em",r["email"]),
                        P("@adr",r["address"]),P("@nt",r["notes"]),P("@cat",r["created_at"]),
                        P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlOrders(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT id, user_id, place_id, payment_id, created_at, paid," +
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
                "  payment2_id, ISNULL(payment2_amount,0) AS payment2_amount, sync_token" +
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
                " WHERE o.created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM order_food WHERE id=@c", "@c", cid);
                int orderId = Convert.ToInt32(r["order_id"]);
                // FK: order local da bo'lishi kerak
                int? orderLid = ScalarOrNull(local, "SELECT id FROM [order] WHERE id=@c", "@c", orderId);
                if (orderLid == null) continue;

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
                        P("@id",cid),P("@oid",orderId),P("@fid",r["food_id"]),
                        P("@qty",r["quantity"]),P("@note",r["note"]),P("@tok",r["sync_token"]));
                count++;
            }
            return count;
        }

        private static int DlOrderPayments(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(central,
                "SELECT op.id, op.order_id, op.payment_id, op.amount, op.sync_token" +
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
                "  ISNULL(d.created_at,GETDATE()) AS created_at, d.sync_token" +
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
                "  ISNULL(l.cancelled_at,GETDATE()) AS cancelled_at, l.sync_token" +
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
            DataTable rows = ReadAll(central,
                "SELECT id, ISNULL(type,'') AS type, ISNULL(amount,0) AS amount," +
                "  ISNULL(note,'') AS note, ISNULL(created_at,GETDATE()) AS created_at," +
                "  user_id, sync_token" +
                " FROM cash_transaction" +
                " WHERE created_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM cash_transaction WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE cash_transaction SET type=@t,amount=@amt,note=@nt,is_synced=1 WHERE id=@id",
                        P("@t",r["type"]),P("@amt",r["amount"]),P("@nt",r["note"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT cash_transaction ON;" +
                        "INSERT INTO cash_transaction(id,type,amount,note,created_at,user_id,sync_token,is_synced)" +
                        " VALUES(@id,@t,@amt,@nt,@cat,@uid,@tok,1);" +
                        "SET IDENTITY_INSERT cash_transaction OFF",
                        P("@id",cid),P("@t",r["type"]),P("@amt",r["amount"]),P("@nt",r["note"]),
                        P("@cat",r["created_at"]),P("@uid",r["user_id"]),P("@tok",r["sync_token"]));
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
                "  ISNULL(notes,'') AS notes, sync_token" +
                " FROM ingredient_purchase" +
                " WHERE purchased_at >= DATEADD(YEAR,-1,GETDATE())");
            foreach (DataRow r in rows.Rows)
            {
                int cid = Convert.ToInt32(r["id"]);
                int? lid = ScalarOrNull(local, "SELECT id FROM ingredient_purchase WHERE id=@c", "@c", cid);
                if (lid != null)
                    Exec(local,
                        "UPDATE ingredient_purchase SET quantity=@qty,price_per_unit=@pp," +
                        "  total_price=@tp,notes=@nt,is_synced=1 WHERE id=@id",
                        P("@qty",r["quantity"]),P("@pp",r["price_per_unit"]),
                        P("@tp",r["total_price"]),P("@nt",r["notes"]),P("@id",lid.Value));
                else
                    Exec(local,
                        "SET IDENTITY_INSERT ingredient_purchase ON;" +
                        "INSERT INTO ingredient_purchase(id,ingredient_id,quantity,price_per_unit," +
                        "  total_price,purchased_at,notes,sync_token,is_synced)" +
                        " VALUES(@id,@iid,@qty,@pp,@tp,@pat,@nt,@tok,1);" +
                        "SET IDENTITY_INSERT ingredient_purchase OFF",
                        P("@id",cid),P("@iid",r["ingredient_id"]),P("@qty",r["quantity"]),
                        P("@pp",r["price_per_unit"]),P("@tp",r["total_price"]),
                        P("@pat",r["purchased_at"]),P("@nt",r["notes"]),P("@tok",r["sync_token"]));
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
                "  ISNULL(notes,'') AS notes, sync_token" +
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
}
