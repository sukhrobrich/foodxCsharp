using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace WindowsFormsApp1.services
{
    internal class dbconnect
    {
        // ── Ulanish satrlari ──────────────────────────────────────────────────
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
            return ConfigurationManager.ConnectionStrings["FoodXLocal"]?.ConnectionString
                ?? @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=FoodX;Integrated Security=True;TrustServerCertificate=True";
        }

        // ── Instance ─────────────────────────────────────────────────────────
        private SqlConnection _conn;

        public dbconnect()
        {
            _conn = new SqlConnection(Session.IsOnline ? _central : _local);
        }

        public SqlConnection GetCon() => _conn;

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

        // ── SESSION_CONTEXT orqali RLS ni yoqish ──────────────────────────────
        private void SetTenantContext()
        {
            using var cmd = new SqlCommand(
                "EXEC sys.sp_set_session_context N'tenant_id', @tid, @readonly", _conn);
            cmd.Parameters.AddWithValue("@tid",      Session.TenantId);
            cmd.Parameters.AddWithValue("@readonly", false);
            cmd.ExecuteNonQuery();
        }

        // ── Statik yordamchi: markaziy serverga ulanish mumkinmi? ─────────────
        public static bool CheckCentral()
        {
            try
            {
                using var c = new SqlConnection(_central + ";Connect Timeout=3");
                c.Open();
                return true;
            }
            catch { return false; }
        }

        // ── SyncEngine uchun ochiq ulanishlar ─────────────────────────────────
        public static SqlConnection OpenCentralForSync(int tenantId)
        {
            var c = new SqlConnection(_central);
            c.Open();
            using var cmd = new SqlCommand(
                "EXEC sys.sp_set_session_context N'tenant_id', @t, @r", c);
            cmd.Parameters.AddWithValue("@t", tenantId);
            cmd.Parameters.AddWithValue("@r", false);
            cmd.ExecuteNonQuery();
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
