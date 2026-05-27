using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;

namespace WindowsFormsApp1.services
{
    internal class dbconnect
    {
        private static readonly string _connString = LoadConnectionString();

        private static string LoadConnectionString()
        {
            string cfgFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.cfg");
            if (File.Exists(cfgFile))
            {
                string cs = File.ReadAllText(cfgFile).Trim();
                if (!string.IsNullOrEmpty(cs)) return cs;
            }
            return ConfigurationManager.ConnectionStrings["FoodX"]?.ConnectionString
                ?? @"Data Source=DESKTOP-MH76F5N;Initial Catalog=FoodX;Integrated Security=True;TrustServerCertificate=True";
        }

        private SqlConnection connection = new SqlConnection(_connString);

        public SqlConnection GetCon()
        {
            return connection;
        }

        public void OpenCon()
        {
            if (connection.State == System.Data.ConnectionState.Closed)
                connection.Open();
        }

        public void CloseCon()
        {
            if (connection.State == System.Data.ConnectionState.Open)
                connection.Close();
        }
    }

}
