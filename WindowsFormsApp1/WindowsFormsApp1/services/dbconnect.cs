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
            string cfg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");
            if (File.Exists(cfg))
            {
                string s = File.ReadAllText(cfg).Trim();
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return ConfigurationManager.ConnectionStrings["FoodX"]?.ConnectionString
                ?? @"Data Source=192.168.35.230,1433;Initial Catalog=FoodX;User ID=sa;Password=Ac0323301;TrustServerCertificate=True;Encrypt=False";
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

        // Mavjud SQL Server instansini avtomatik topadi (master ga ulanib tekshiradi)
        private static string DetectLocalServer()
        {
            string[] candidates = {
                @".\SQLEXPRESS",
                @".\MSSQLSERVER",
                @".",
                @"localhost",
                System.Environment.MachineName,
                @"(LocalDB)\MSSQLLocalDB",   // oxirgi — faqat developer mashinasida
            };

            foreach (string ds in candidates)
            {
                try
                {
                    string test = string.Format(
                        "Data Source={0};Initial Catalog=master;Integrated Security=True;" +
                        "TrustServerCertificate=True;Connect Timeout=2", ds);
                    using (var c = new SqlConnection(test))
                    {
                        c.Open();
                        return string.Format(
                            "Data Source={0};Initial Catalog=FoodX;Integrated Security=True;" +
                            "TrustServerCertificate=True", ds);
                    }
                }
                catch { }
            }

            // Hech narsa topilmasa — LocalDB ni default qilib qaytaramiz
            return @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=FoodX;" +
                   @"Integrated Security=True;TrustServerCertificate=True";
        }

        private SqlConnection _conn;

        public dbconnect()
        {
            _conn = new SqlConnection(Session.IsOnline ? _central : _local);
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

        public static bool CheckCentral()
        {
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
            try
            {
                using (var c = new SqlConnection(_local + ";Connect Timeout=3"))
                {
                    c.Open();
                    return true;
                }
            }
            catch { return false; }
        }

        // Mahalliy bazani tekshiradi; yo'q bo'lsa install_local_db.sql dan yaratadi.
        public static bool EnsureLocalDatabase()
        {
            if (CheckLocal()) return true;

            try
            {
                // _local dagi server manziliga master orqali ulanamiz
                var builder = new SqlConnectionStringBuilder(_local);
                builder.InitialCatalog = "master";
                builder.ConnectTimeout = 5;

                string sqlFile = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "install_local_db.sql");
                if (!File.Exists(sqlFile)) return false;

                string script = File.ReadAllText(sqlFile, System.Text.Encoding.UTF8);

                string[] batches = System.Text.RegularExpressions.Regex.Split(
                    script, @"^\s*GO\s*$",
                    System.Text.RegularExpressions.RegexOptions.Multiline |
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                using (var c = new SqlConnection(builder.ConnectionString))
                {
                    c.Open();
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

                return CheckLocal();
            }
            catch { return false; }
        }

        public static SqlConnection OpenCentralForSync(int tenantId)
        {
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
