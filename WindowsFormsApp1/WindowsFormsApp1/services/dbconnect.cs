using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace WindowsFormsApp1.services
{
    internal class dbconnect
    {
        private static readonly string _central = LoadCentral();
        private static readonly string _local   = LoadLocal();

        private static string LoadCentral()
        {
            // 1. connection.cfg dan o'qiymiz (asosiy manba)
            string cfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");
            if (File.Exists(cfg))
            {
                string s = File.ReadAllText(cfg).Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    try { _ = new SqlConnectionStringBuilder(s); return s; }
                    catch { }
                }
            }

            // 2. App.config dan o'qiymiz
            string fromConfig = ConfigurationManager.ConnectionStrings["FoodX"]?.ConnectionString;
            if (!string.IsNullOrEmpty(fromConfig)) return fromConfig;

            // 3. connection.cfg yo'q — sozlamalar ko'rsatilishi kerak (bo'sh qaytaramiz)
            return string.Empty;
        }

        private static string LoadLocal()
        {
            string cfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local_connection.cfg");
            if (File.Exists(cfg))
            {
                string s = File.ReadAllText(cfg).Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            string fromAppConfig = ConfigurationManager.ConnectionStrings["FoodXLocal"]?.ConnectionString;
            if (!string.IsNullOrEmpty(fromAppConfig)) return fromAppConfig;

            return DetectLocalServer();
        }

        // Mavjud SQL Server instansini avtomatik topadi (faqat Windows Auth)
        private static string DetectLocalServer()
        {
            string[] candidates = {
                @".\SQLEXPRESS",
                @".\MSSQLSERVER",
                @".",
                @"localhost",
                System.Environment.MachineName,
                @"(LocalDB)\MSSQLLocalDB",
            };

            foreach (string ds in candidates)
            {
                try
                {
                    string test = string.Format(
                        "Data Source={0};Initial Catalog=master;Integrated Security=True;" +
                        "TrustServerCertificate=True;Connect Timeout=2", ds);
                    using (var c = new SqlConnection(test)) { c.Open(); }
                    return string.Format(
                        "Data Source={0};Initial Catalog=FoodX;Integrated Security=True;" +
                        "TrustServerCertificate=True", ds);
                }
                catch { }
            }

            // LocalDB fallback
            return @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=FoodX;" +
                   @"Integrated Security=True;TrustServerCertificate=True";
        }

        private SqlConnection _conn;

        public dbconnect()
        {
            // LOCAL-FIRST: har doim local DB dan o'qiymiz va yozamiz
            // Central ga yozish SyncQueue orqali background da bajariladi
            _conn = new SqlConnection(_local);

            // SqlDataAdapter.Fill() ham ishlatganda tenant context avtomatik o'rnatilsin
            _conn.StateChange += (sender, args) =>
            {
                if (args.CurrentState == System.Data.ConnectionState.Open
                    && Session.IsOnline && Session.TenantId > 0)
                {
                    try { SetTenantContext(); } catch { }
                }
            };
        }

        public SqlConnection GetCon() { return _conn; }

        public void OpenCon()
        {
            if (_conn.State == ConnectionState.Closed)
            {
                _conn.Open();
                if (Session.IsOnline && Session.TenantId > 0)
                    SetTenantContext();
            }
        }

        public void CloseCon()
        {
            if (_conn.State == ConnectionState.Open)
                _conn.Close();
        }

        private void SetTenantContext()
        {
            using (var cmd = new SqlCommand(
                "EXEC sys.sp_set_session_context N'tenant_id', @tid, @readonly", _conn))
            {
                cmd.Parameters.AddWithValue("@tid",      Session.TenantId);
                cmd.Parameters.AddWithValue("@readonly", false);
                cmd.ExecuteNonQuery();
            }
        }

        public static bool IsCentralConfigured =>
            !string.IsNullOrWhiteSpace(_central);

        public static bool CheckCentral()
        {
            if (!IsCentralConfigured) return false;
            try
            {
                using (var c = new SqlConnection(_central + ";Connect Timeout=3"))
                {
                    c.Open();
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool CheckLocal()
        {
            // LocalDB ishga tushishi uchun 2 ta urinish (birinchi ulanishda sekin bo'lishi mumkin)
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (var c = new SqlConnection(_local + ";Connect Timeout=8"))
                    {
                        c.Open();
                        return true;
                    }
                }
                catch
                {
                    if (attempt == 0) System.Threading.Thread.Sleep(1500);
                }
            }
            return false;
        }

        // Mahalliy bazani tekshiradi; yo'q bo'lsa install_local_db.sql dan yaratadi.
        // out errorMessage — aniq xato sababi (null = muvaffaqiyatli)
        public static bool EnsureLocalDatabase(out string errorMessage)
        {
            errorMessage = null;
            if (CheckLocal())
            {
                FixLocalDefaults();
                return true;
            }

            // install_local_db.sql ni bir nechta joylarda qidirish
            string sqlFile = null;
            string[] searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "install_local_db.sql"),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "install_local_db.sql"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "install_local_db.sql"),
            };
            foreach (var p in searchPaths)
            {
                try { if (File.Exists(p)) { sqlFile = Path.GetFullPath(p); break; } } catch { }
            }

            if (sqlFile == null)
            {
                errorMessage = $"install_local_db.sql fayli topilmadi.\n\nQidirilgan joylar:\n{string.Join("\n", searchPaths)}";
                return false;
            }

            try
            {
                // _local dagi server manziliga master orqali ulanamiz
                var builder = new SqlConnectionStringBuilder(_local);
                builder.InitialCatalog = "master";
                builder.ConnectTimeout = 5;

                string script = File.ReadAllText(sqlFile, System.Text.Encoding.UTF8);

                string[] batches = System.Text.RegularExpressions.Regex.Split(
                    script, @"^\s*GO\s*$",
                    System.Text.RegularExpressions.RegexOptions.Multiline |
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                using (var c = new SqlConnection(builder.ConnectionString))
                {
                    try { c.Open(); }
                    catch (Exception ex)
                    {
                        errorMessage = $"SQL Server ga ulanib bo'lmadi.\n\nServer: {builder.DataSource}\nXato: {ex.Message}";
                        return false;
                    }

                    foreach (string batch in batches)
                    {
                        string b = batch.Trim();
                        if (string.IsNullOrEmpty(b)) continue;
                        using (var cmd = new SqlCommand(b, c))
                        {
                            cmd.CommandTimeout = 30;
                            try { cmd.ExecuteNonQuery(); }
                            catch (SqlException ex)
                            {
                                if (ex.Number != 1801) throw; // 1801 = baza allaqachon bor
                            }
                        }
                    }
                }

                // CREATE DATABASE dan keyin SQL Server bazani online holatga olib chiqishga vaqt kerak
                // sys.databases dan to'g'ri tekshiramiz, keyin CheckLocal() qayta urinadi
                bool dbExistsInSys = false;
                using (var c2 = new SqlConnection(builder.ConnectionString))
                {
                    c2.Open();
                    using (var cmd2 = new SqlCommand("SELECT COUNT(1) FROM sys.databases WHERE name='FoodX'", c2))
                        dbExistsInSys = (int)cmd2.ExecuteScalar() > 0;
                }

                if (!dbExistsInSys)
                {
                    errorMessage = "SQL skript bajarildi, lekin sys.databases da FoodX ko'rinmaydi.";
                    return false;
                }

                // Baza yaratilgan — CheckLocal() uchun biroz kutamiz (SQL Server online holatga o'tkazsin)
                for (int i = 0; i < 5; i++)
                {
                    if (CheckLocal()) { FixLocalDefaults(); return true; }
                    System.Threading.Thread.Sleep(1000);
                }

                errorMessage = $"Baza sys.databases da bor, lekin ulanib bo'lmaydi.\n" +
                               $"Connection string: {_local}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Baza yaratishda xato:\n{ex.Message}";
                return false;
            }
        }

        // Eski signature — orqaga moslik uchun
        public static bool EnsureLocalDatabase()
        {
            return EnsureLocalDatabase(out _);
        }

        // Mavjud local DB dagi noto'g'ri DEFAULT larni tuzatadi
        public static void FixLocalDefaults()
        {
            try
            {
                using (var c = new SqlConnection(_local + ";Connect Timeout=3"))
                {
                    c.Open();

                    // settings jadvaliga tenant_id qo'shish (eski bazalar uchun)
                    using (var cmd = new SqlCommand(@"
                        IF EXISTS(SELECT 1 FROM sys.objects WHERE object_id=OBJECT_ID(N'settings') AND type=N'U')
                        AND NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='settings' AND COLUMN_NAME='tenant_id')
                        BEGIN
                            ALTER TABLE settings ADD tenant_id INT NOT NULL DEFAULT 0;
                            DECLARE @pk NVARCHAR(200)=(SELECT TOP 1 CONSTRAINT_NAME
                                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                                WHERE TABLE_NAME='settings' AND CONSTRAINT_TYPE='PRIMARY KEY');
                            IF @pk IS NOT NULL EXEC('ALTER TABLE settings DROP CONSTRAINT ['+@pk+']');
                            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='PK_settings')
                                ALTER TABLE settings ADD CONSTRAINT PK_settings PRIMARY KEY ([key],tenant_id);
                        END", c))
                    { try { cmd.ExecuteNonQuery(); } catch { } }

                    // order_debt_paid_sync
                    using (var cmd = new SqlCommand(
                        "IF OBJECT_ID('dbo.order_debt_paid_sync','U') IS NULL " +
                        "CREATE TABLE order_debt_paid_sync(debt_sync_token UNIQUEIDENTIFIER NOT NULL PRIMARY KEY)", c))
                    { try { cmd.ExecuteNonQuery(); } catch { } }

                    // SyncQueue jadvali
                    using (var cmd = new SqlCommand(@"
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
                        )", c))
                    { try { cmd.ExecuteNonQuery(); } catch { } }

                    // Eski xatolikdan qolgan stuck SyncQueue yozuvlarni reset qilish
                    using (var cmd = new SqlCommand(
                        "UPDATE SyncQueue SET RetryCount=0, ErrorMsg=NULL " +
                        "WHERE IsSynced=0 AND RetryCount>=5", c))
                    { try { cmd.ExecuteNonQuery(); } catch { } }

                    // Lokal DB dagi duplikat food/food_category yozuvlarni tozalash
                    // (DownloadAll IDENTITY_INSERT qilgan lekin central_id o'rnatmagan yozuvlar)
                    string[] dupCleanup = {
                        // order_food: sync_token bir xil bo'lib, ikki xil ID bilan saqlangan duplikatlarni o'chirish
                        // (lokal ID kichik, central download ID katta — kattasini o'chiramiz)
                        @"DELETE of1 FROM order_food of1
                          WHERE of1.sync_token IS NOT NULL
                            AND EXISTS (
                                SELECT 1 FROM order_food of2
                                WHERE of2.sync_token = of1.sync_token
                                  AND of2.id < of1.id
                            )",
                        // food_category: central_id bor va id != central_id bo'lgan yozuvlarni o'chirish
                        @"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='food_category' AND COLUMN_NAME='central_id')
                          BEGIN
                            -- food_category duplikatlarini o'chirish (central_id ga mos id bor bo'lsa)
                            DELETE fc FROM food_category fc
                            WHERE fc.central_id IS NOT NULL
                              AND fc.id = fc.central_id
                              AND EXISTS(SELECT 1 FROM food_category fc2
                                         WHERE fc2.central_id = fc.id AND fc2.id != fc.id)
                          END",
                        // food: central_id bor va id = central_id bo'lgan (IDENTITY_INSERT) yozuvlarni o'chirish
                        @"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME='food' AND COLUMN_NAME='central_id')
                          BEGIN
                            UPDATE order_food SET food_id=(
                                SELECT MIN(f2.id) FROM food f2 WHERE f2.name=food.name)
                            FROM order_food JOIN food ON food.id=order_food.food_id
                            WHERE food.id=food.central_id
                              AND EXISTS(SELECT 1 FROM food f3 WHERE f3.name=food.name AND f3.id!=food.id);
                            DELETE FROM food
                            WHERE id=central_id
                              AND EXISTS(SELECT 1 FROM food f2 WHERE f2.name=food.name AND f2.id!=food.id);
                          END",
                    };
                    foreach (string sql in dupCleanup)
                        using (var cmd = new SqlCommand(sql, c))
                        { cmd.CommandTimeout = 15; try { cmd.ExecuteNonQuery(); } catch { } }

                    // Lokal DB sxemasi central bilan bir xil bo'lishi uchun etishmayotgan ustunlar
                    string[] schemaMigrations = {
                        // [user] jadvalidagi yangi ustunlar
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='app_password') ALTER TABLE [user] ADD app_password NVARCHAR(64) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='updated_at') ALTER TABLE [user] ADD updated_at DATETIME NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='sort_order') ALTER TABLE [user] ADD sort_order INT NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='created_at') ALTER TABLE [user] ADD created_at DATETIME NULL DEFAULT GETDATE()",

                        // user_category
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user_category' AND COLUMN_NAME='role_type') ALTER TABLE user_category ADD role_type NVARCHAR(20) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user_category' AND COLUMN_NAME='color') ALTER TABLE user_category ADD color NVARCHAR(20) NULL",

                        // order_cancellation_log — sync uchun kerakli ustunlar
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_cancellation_log' AND COLUMN_NAME='sync_token') ALTER TABLE order_cancellation_log ADD sync_token UNIQUEIDENTIFIER NULL DEFAULT NEWID()",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_cancellation_log' AND COLUMN_NAME='food_id') ALTER TABLE order_cancellation_log ADD food_id INT NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_cancellation_log' AND COLUMN_NAME='food_name') ALTER TABLE order_cancellation_log ADD food_name NVARCHAR(200) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_cancellation_log' AND COLUMN_NAME='food_category') ALTER TABLE order_cancellation_log ADD food_category NVARCHAR(200) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_cancellation_log' AND COLUMN_NAME='cancelled_qty') ALTER TABLE order_cancellation_log ADD cancelled_qty INT NULL",

                        // [order] — payment2 va sync_token
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='payment2_id') ALTER TABLE [order] ADD payment2_id INT NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='payment2_amount') ALTER TABLE [order] ADD payment2_amount DECIMAL(18,2) NULL",
                        // [order] — yetkazib berish fieldlari
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='delivery_phone') ALTER TABLE [order] ADD delivery_phone NVARCHAR(50) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='delivery_address') ALTER TABLE [order] ADD delivery_address NVARCHAR(500) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='is_delivery') ALTER TABLE [order] ADD is_delivery BIT NULL DEFAULT 0",

                        // order_payments — sync uchun kerakli ustunlar
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_payments' AND COLUMN_NAME='sync_token') BEGIN ALTER TABLE order_payments ADD sync_token UNIQUEIDENTIFIER NULL DEFAULT NEWID(); UPDATE order_payments SET sync_token=NEWID() WHERE sync_token IS NULL END",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order_payments' AND COLUMN_NAME='is_synced') ALTER TABLE order_payments ADD is_synced BIT NOT NULL DEFAULT 1",

                        // ingredient — delta sync uchun (EXEC ishlatamiz — runtime kompilatsiya)
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient' AND COLUMN_NAME='synced_qty') ALTER TABLE ingredient ADD synced_qty DECIMAL(18,4) NULL",
                        "EXEC('UPDATE ingredient SET synced_qty = quantity WHERE synced_qty IS NULL')",

                        // sync_token va central_id — reference jadvallar uchun (multi-tenant ID konflikti)
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food' AND COLUMN_NAME='sync_token') ALTER TABLE food ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food' AND COLUMN_NAME='central_id') ALTER TABLE food ADD central_id INT NULL",
                        "EXEC('UPDATE food SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food_category' AND COLUMN_NAME='sync_token') ALTER TABLE food_category ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food_category' AND COLUMN_NAME='central_id') ALTER TABLE food_category ADD central_id INT NULL",
                        "EXEC('UPDATE food_category SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='sync_token') ALTER TABLE [user] ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user' AND COLUMN_NAME='central_id') ALTER TABLE [user] ADD central_id INT NULL",
                        "EXEC('UPDATE [user] SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user_category' AND COLUMN_NAME='sync_token') ALTER TABLE user_category ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='user_category' AND COLUMN_NAME='central_id') ALTER TABLE user_category ADD central_id INT NULL",
                        "EXEC('UPDATE user_category SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='payment' AND COLUMN_NAME='sync_token') ALTER TABLE payment ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='payment' AND COLUMN_NAME='central_id') ALTER TABLE payment ADD central_id INT NULL",
                        "EXEC('UPDATE payment SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient' AND COLUMN_NAME='sync_token') ALTER TABLE ingredient ADD sync_token UNIQUEIDENTIFIER NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient' AND COLUMN_NAME='central_id') ALTER TABLE ingredient ADD central_id INT NULL",
                        "EXEC('UPDATE ingredient SET sync_token=NEWID() WHERE sync_token IS NULL')",

                        // ingredient_purchase va food_purchase uchun sync_token (eski DB da yo'q bo'lishi mumkin)
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient_purchase' AND COLUMN_NAME='sync_token') ALTER TABLE ingredient_purchase ADD sync_token UNIQUEIDENTIFIER NULL",
                        "EXEC('UPDATE ingredient_purchase SET sync_token=NEWID() WHERE sync_token IS NULL')",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient_purchase' AND COLUMN_NAME='is_synced') ALTER TABLE ingredient_purchase ADD is_synced BIT NOT NULL DEFAULT 0",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food_purchase' AND COLUMN_NAME='sync_token') ALTER TABLE food_purchase ADD sync_token UNIQUEIDENTIFIER NULL",
                        "EXEC('UPDATE food_purchase SET sync_token=NEWID() WHERE sync_token IS NULL')",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='food_purchase' AND COLUMN_NAME='is_synced') ALTER TABLE food_purchase ADD is_synced BIT NOT NULL DEFAULT 0",

                        // ingredient_purchase.central_id — DlIngredientPurchases dublikat oldini oladi
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ingredient_purchase' AND COLUMN_NAME='central_id') ALTER TABLE ingredient_purchase ADD central_id INT NULL",

                        // place_out va place_in uchun central_id (UPDATE/DELETE sync uchun)
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='place_out' AND COLUMN_NAME='central_id') ALTER TABLE place_out ADD central_id INT NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='place_in' AND COLUMN_NAME='central_id') ALTER TABLE place_in ADD central_id INT NULL",

                        // customer uchun login va password (mijoz hisobi)
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='customer' AND COLUMN_NAME='login') ALTER TABLE customer ADD login NVARCHAR(100) NULL",
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='customer' AND COLUMN_NAME='password') ALTER TABLE customer ADD password NVARCHAR(200) NULL",

                        // order.is_customer_order — mobil mijoz buyurtmalari belgisi
                        "IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='order' AND COLUMN_NAME='is_customer_order') ALTER TABLE [order] ADD is_customer_order BIT NOT NULL DEFAULT 0",

                        // order.user_id va place_id — mijoz orderlari uchun NULL qabul qilsin
                        "IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'[order]') AND name='user_id'  AND is_nullable=0) EXEC('ALTER TABLE [order] ALTER COLUMN user_id  INT NULL')",
                        "IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'[order]') AND name='place_id' AND is_nullable=0) EXEC('ALTER TABLE [order] ALTER COLUMN place_id INT NULL')",
                    };
                    foreach (string sql in schemaMigrations)
                        using (var cmd = new SqlCommand(sql, c))
                        { try { cmd.ExecuteNonQuery(); } catch { } }

                    string[] fixes = {
                        // [order].is_synced default 1 → 0
                        "IF EXISTS (SELECT 1 FROM sys.default_constraints dc " +
                        "  JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "  WHERE OBJECT_NAME(dc.parent_object_id)='order' AND col.name='is_synced' AND dc.definition='((1))') " +
                        "BEGIN " +
                        "  DECLARE @cn1 NVARCHAR(200)=(SELECT dc.name FROM sys.default_constraints dc " +
                        "    JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "    WHERE OBJECT_NAME(dc.parent_object_id)='order' AND col.name='is_synced'); " +
                        "  EXEC('ALTER TABLE [order] DROP CONSTRAINT ['+@cn1+']'); " +
                        "  ALTER TABLE [order] ADD DEFAULT 0 FOR is_synced " +
                        "END",

                        // order_debt.is_synced default 1 → 0
                        "IF EXISTS (SELECT 1 FROM sys.default_constraints dc " +
                        "  JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "  WHERE OBJECT_NAME(dc.parent_object_id)='order_debt' AND col.name='is_synced' AND dc.definition='((1))') " +
                        "BEGIN " +
                        "  DECLARE @cn2 NVARCHAR(200)=(SELECT dc.name FROM sys.default_constraints dc " +
                        "    JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "    WHERE OBJECT_NAME(dc.parent_object_id)='order_debt' AND col.name='is_synced'); " +
                        "  EXEC('ALTER TABLE order_debt DROP CONSTRAINT ['+@cn2+']'); " +
                        "  ALTER TABLE order_debt ADD DEFAULT 0 FOR is_synced " +
                        "END",

                        // order_cancellation_log.is_synced default 1 → 0
                        "IF EXISTS (SELECT 1 FROM sys.default_constraints dc " +
                        "  JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "  WHERE OBJECT_NAME(dc.parent_object_id)='order_cancellation_log' AND col.name='is_synced' AND dc.definition='((1))') " +
                        "BEGIN " +
                        "  DECLARE @cn3 NVARCHAR(200)=(SELECT dc.name FROM sys.default_constraints dc " +
                        "    JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "    WHERE OBJECT_NAME(dc.parent_object_id)='order_cancellation_log' AND col.name='is_synced'); " +
                        "  EXEC('ALTER TABLE order_cancellation_log DROP CONSTRAINT ['+@cn3+']'); " +
                        "  ALTER TABLE order_cancellation_log ADD DEFAULT 0 FOR is_synced " +
                        "END",

                    // ingredient_purchase.is_synced default 1 → 0
                        "IF EXISTS (SELECT 1 FROM sys.default_constraints dc " +
                        "  JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "  WHERE OBJECT_NAME(dc.parent_object_id)='ingredient_purchase' AND col.name='is_synced' AND dc.definition='((1))') " +
                        "BEGIN " +
                        "  DECLARE @cn4 NVARCHAR(200)=(SELECT dc.name FROM sys.default_constraints dc " +
                        "    JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "    WHERE OBJECT_NAME(dc.parent_object_id)='ingredient_purchase' AND col.name='is_synced'); " +
                        "  EXEC('ALTER TABLE ingredient_purchase DROP CONSTRAINT ['+@cn4+']'); " +
                        "  ALTER TABLE ingredient_purchase ADD DEFAULT 0 FOR is_synced " +
                        "END",

                    // food_purchase.is_synced default 1 → 0
                        "IF EXISTS (SELECT 1 FROM sys.default_constraints dc " +
                        "  JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "  WHERE OBJECT_NAME(dc.parent_object_id)='food_purchase' AND col.name='is_synced' AND dc.definition='((1))') " +
                        "BEGIN " +
                        "  DECLARE @cn5 NVARCHAR(200)=(SELECT dc.name FROM sys.default_constraints dc " +
                        "    JOIN sys.columns col ON dc.parent_object_id=col.object_id AND dc.parent_column_id=col.column_id " +
                        "    WHERE OBJECT_NAME(dc.parent_object_id)='food_purchase' AND col.name='is_synced'); " +
                        "  EXEC('ALTER TABLE food_purchase DROP CONSTRAINT ['+@cn5+']'); " +
                        "  ALTER TABLE food_purchase ADD DEFAULT 0 FOR is_synced " +
                        "END",

                    // Eski draft qarz yozuvlari (amount=0, yopilmagan order) — central ga ketmasin
                    "UPDATE order_debt SET is_synced=1 " +
                    "WHERE ISNULL(amount,0)=0 AND ISNULL(is_paid,0)=0 " +
                    "  AND EXISTS(SELECT 1 FROM [order] o WHERE o.id=order_debt.order_id AND o.paid='NO')",

                    // order_food.food_id: central_id da saqlangan eski yozuvlarni lokal food.id ga tuzatish
                    // (DlOrderFoodsFast avval central food_id saqlardi, endi lokal id saqlaydi)
                    @"IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME='food' AND COLUMN_NAME='central_id')
                      UPDATE ofd SET ofd.food_id = f.id
                      FROM order_food ofd
                      JOIN food f ON f.central_id = ofd.food_id
                      WHERE NOT EXISTS (SELECT 1 FROM food WHERE id = ofd.food_id)",

                    // Eski DlIngredients IDENTITY_INSERT bilan yaratgan dublikat ingredientlarni o'chirish
                    "DELETE FROM ingredient " +
                    "WHERE central_id IS NULL " +
                    "  AND id IN (SELECT central_id FROM ingredient WHERE central_id IS NOT NULL) " +
                    "  AND id NOT IN (SELECT ISNULL(ingredient_id,0) FROM recipe_ingredient) " +
                    "  AND id NOT IN (SELECT ISNULL(ingredient_id,0) FROM ingredient_purchase)",

                    // DlIngredientPurchases NEWID() bilan yaratgan dublikat xaridlarni tozalash
                    // Bir xil (ingredient_id, purchased_at, quantity, price_per_unit) bo'lsa — eski yozuvni saqlaydi
                    "DELETE FROM ingredient_purchase " +
                    "WHERE id NOT IN ( " +
                    "  SELECT MIN(id) FROM ingredient_purchase " +
                    "  GROUP BY ingredient_id, purchased_at, quantity, price_per_unit " +
                    ")",
                    };
                    foreach (string sql in fixes)
                    {
                        using (var cmd = new SqlCommand(sql, c))
                        {
                            cmd.CommandTimeout = 10;
                            try { cmd.ExecuteNonQuery(); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        public static SqlConnection OpenCentralForSync(int tenantId)
        {
            if (!IsCentralConfigured)
                throw new InvalidOperationException(
                    "Markaziy server sozlanmagan. 'connection.cfg' faylini tekshiring.");
            var c = new SqlConnection(_central);
            c.Open();
            using (var cmd = new SqlCommand(
                "EXEC sys.sp_set_session_context N'tenant_id', @t, @r", c))
            {
                cmd.Parameters.AddWithValue("@t", tenantId);
                cmd.Parameters.AddWithValue("@r", false);
                cmd.ExecuteNonQuery();
            }
            return c;
        }

        public static SqlConnection OpenLocalForSync()
        {
            var c = new SqlConnection(_local);
            c.Open();
            return c;
        }
    }
}
