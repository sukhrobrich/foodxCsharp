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

        public static SyncResult SyncAll()
        {
            var result = new SyncResult();
            if (!Session.IsOnline || Session.TenantId == 0) return result;

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                result.Synced += SyncCustomers(local, central);
                result.Synced += SyncOrders(local, central);
                result.Synced += SyncOrderFoods(local, central);
                result.Synced += SyncOrderPayments(local, central);
                result.Synced += SyncOrderDebts(local, central);
                result.Synced += SyncCancellationLogs(local, central);
                result.Synced += SyncCashTransactions(local, central);
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

                Exec(local,
                    "UPDATE [order] SET is_synced = 1, central_id = @cid WHERE id = @id",
                    P("@cid", centralId), P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 3. Buyurtma tarkibi ──────────────────────────────────────────────
        private static int SyncOrderFoods(SqlConnection local, SqlConnection central)
        {
            int count = 0;
            DataTable rows = ReadAll(local,
                "SELECT f.*, o.central_id AS order_central_id " +
                "FROM order_food f " +
                "JOIN [order] o ON o.id = f.order_id " +
                "WHERE f.is_synced = 0 AND o.central_id IS NOT NULL");

            foreach (DataRow r in rows.Rows)
            {
                Guid tok = (Guid)r["sync_token"];
                int? exists = ScalarOrNull(central,
                    "SELECT id FROM order_food WHERE sync_token = @t", "@t", tok);

                if (exists == null)
                    Exec(central,
                        "INSERT INTO order_food (order_id, food_id, quantity, note, sync_token) " +
                        "VALUES (@oid, @fid, @qty, @note, @tok)",
                        P("@oid",  r["order_central_id"]), P("@fid",  r["food_id"]),
                        P("@qty",  r["quantity"]),          P("@note", r["note"]),
                        P("@tok",  tok));

                Exec(local, "UPDATE order_food SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
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

                Exec(local, "UPDATE order_payments SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
                count++;
            }
            return count;
        }

        // ── 5. Qarzlar ──────────────────────────────────────────────────────
        private static int SyncOrderDebts(SqlConnection local, SqlConnection central)
        {
            int count = 0;
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

                Exec(local, "UPDATE order_debt SET is_synced = 1 WHERE id = @id", P("@id", r["id"]));
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

            SqlConnection local   = null;
            SqlConnection central = null;
            try
            {
                local   = dbconnect.OpenLocalForSync();
                central = dbconnect.OpenCentralForSync(Session.TenantId);

                // Har bir jadval mustaqil — biri xato bo'lsa qolganlari davom etadi
                TryDl(() => DlSettings(local, central),        result);
                TryDl(() => DlUserCategories(local, central),  result);
                TryDl(() => DlUsers(local, central),           result);
                TryDl(() => DlFoodCategories(local, central),  result);
                TryDl(() => DlFoods(local, central),           result);
                TryDl(() => DlPlaceCategories(local, central), result);
                TryDl(() => DlPlaceOuts(local, central),       result);
                TryDl(() => DlPlaceIns(local, central),        result);
                TryDl(() => DlPayments(local, central),        result);
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
            DataTable rows = ReadAll(central, "SELECT [key], value FROM settings");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM settings WHERE [key]=@k) " +
                    "  UPDATE settings SET value=@v WHERE [key]=@k " +
                    "ELSE INSERT INTO settings([key],value) VALUES(@k,@v)",
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
                "SELECT id,name,user_category_id,login,password,phone_number,created_at,updated_at,sort_order FROM [user]");
            foreach (DataRow r in rows.Rows)
            {
                Exec(local,
                    "IF EXISTS (SELECT 1 FROM [user] WHERE id=@id) " +
                    "  UPDATE [user] SET name=@n,user_category_id=@uc,login=@l,password=@pw," +
                    "    phone_number=@ph,updated_at=@ua,sort_order=@so WHERE id=@id " +
                    "ELSE BEGIN " +
                    "  SET IDENTITY_INSERT [user] ON; " +
                    "  INSERT INTO [user](id,name,user_category_id,login,password,phone_number,created_at,updated_at,sort_order) " +
                    "  VALUES(@id,@n,@uc,@l,@pw,@ph,@ca,@ua,@so); " +
                    "  SET IDENTITY_INSERT [user] OFF " +
                    "END",
                    P("@id", r["id"]), P("@n", r["name"]), P("@uc", r["user_category_id"]),
                    P("@l", r["login"]), P("@pw", r["password"]), P("@ph", r["phone_number"]),
                    P("@ca", r["created_at"]), P("@ua", r["updated_at"]), P("@so", r["sort_order"]));
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
