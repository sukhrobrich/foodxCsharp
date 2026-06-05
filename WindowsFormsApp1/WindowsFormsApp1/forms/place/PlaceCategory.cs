using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.place
{
    public partial class PlaceCategory : UserControl
    {
        public PlaceCategory()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string name = textBox1.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Kategoriya nomini kiriting!");
                return;
            }

            dbconnect db = new dbconnect();
            string query = "INSERT INTO place_category (name) VALUES (@name)";

            try
            {
                using (SqlCommand cmd = new SqlCommand(query, db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    db.OpenCon();
                    cmd.ExecuteNonQuery();
                    db.CloseCon();
                }

                MessageBox.Show("Kategoriya qo’shildi!");
                //txtCategoryName.Clear();
                LoadCategories();
                //LoadUserCategoriesCombo(); // ComboBox yangilash
                if (Session.IsOnline && Session.TenantId > 0)
                    System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
            }
        }


        private void LoadCategories()
        {
            dbconnect db = new dbconnect();
            DataTable dt = new DataTable();
            using (SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM place_category", db.GetCon()))
            {
                da.Fill(dt);
            }
            dgvCategories.DataSource = dt;
        }

        private void PlaceCategory_Load(object sender, EventArgs e)
        {
            LoadCategories();

        }
    }
}
