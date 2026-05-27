using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.warehouse
{
    public class RecipeIngredientForm : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);

        private readonly int _foodId;
        private ComboBox cboIngredient;
        private TextBox  txtQpp;
        private Label    lblUnit;

        public RecipeIngredientForm(int foodId)
        {
            _foodId = foodId;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(420, 310);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = Color.White;
            BuildUI();
            LoadIngredients();
        }

        private void BuildUI()
        {
            this.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Gold });

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 20, 28, 20) };
            this.Controls.Add(main);

            int y = 16;

            main.Controls.Add(new Label
            {
                Text = "Retseptga masalliq qo'shish",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(0, y)
            });
            y += 36;

            main.Controls.Add(new Label
            {
                Text = "1 porsiya uchun kerakli miqdorni kiriting",
                Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                AutoSize = true, Location = new Point(0, y)
            });
            y += 32;

            // Ingredient
            main.Controls.Add(Lbl("Masalliq *", y)); y += 22;
            cboIngredient = new ComboBox
            {
                Location = new Point(0, y), Width = 364, Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage
            };
            cboIngredient.SelectedIndexChanged += OnIngredientChanged;
            main.Controls.Add(cboIngredient);
            y += 46;

            // Qty per portion — TextBox, accepts "0.2" and "0,2"
            main.Controls.Add(Lbl("1 porsiyaga kerak miqdor *  (masalan: 0.2 yoki 0,2)", y)); y += 22;
            txtQpp = new TextBox
            {
                Location    = new Point(0, y),
                Width       = 180,
                Height      = 32,
                Font        = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = BgPage,
                ForeColor   = TextDark,
                Text        = "0"
            };
            main.Controls.Add(txtQpp);
            lblUnit = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Gold,
                AutoSize  = true,
                Location  = new Point(190, y + 6)
            };
            main.Controls.Add(lblUnit);
            y += 54;

            // Buttons
            Button btnSave = new Button
            {
                Text = "Saqlash",
                Location = new Point(0, y), Width = 176, Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Success, ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            main.Controls.Add(btnSave);

            Button btnCancel = new Button
            {
                Text = "Bekor",
                Location = new Point(188, y), Width = 176, Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextMuted,
                Font = new Font("Segoe UI", 11), Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Border;
            btnCancel.FlatAppearance.BorderSize  = 1;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            main.Controls.Add(btnCancel);

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private void LoadIngredients()
        {
            try
            {
                var db = new dbconnect();
                var dt = new DataTable();
                using (var da = new SqlDataAdapter(
                    "SELECT id, name, unit FROM ingredient WHERE id NOT IN (SELECT ingredient_id FROM recipe_ingredient WHERE food_id=@fid) ORDER BY name",
                    db.GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@fid", _foodId);
                    da.Fill(dt);
                }
                cboIngredient.DataSource    = dt;
                cboIngredient.DisplayMember = "name";
                cboIngredient.ValueMember   = "id";
                if (dt.Rows.Count > 0) OnIngredientChanged(null, null);
            }
            catch (Exception ex) { MessageBox.Show("Masalliqlarni yuklashda xatolik: " + ex.Message); }
        }

        private void OnIngredientChanged(object sender, EventArgs e)
        {
            if (cboIngredient.SelectedItem is DataRowView row)
                lblUnit.Text = row["unit"].ToString();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (cboIngredient.SelectedValue == null)
            {
                MessageBox.Show("Masalliqni tanlang.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal qpp;
            string raw = txtQpp.Text.Trim().Replace(',', '.');
            if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out qpp) || qpp <= 0)
            {
                txtQpp.BackColor = Color.FromArgb(254, 226, 226);
                MessageBox.Show("Noto'g'ri qiymat.\nMasalan: 0.2 yoki 0,2", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtQpp.BackColor = BgPage;
                txtQpp.Focus(); txtQpp.SelectAll();
                return;
            }

            int ingId = Convert.ToInt32(cboIngredient.SelectedValue);

            try
            {
                var db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand(
                    @"IF EXISTS(SELECT 1 FROM recipe_ingredient WHERE food_id=@fid AND ingredient_id=@iid)
                          UPDATE recipe_ingredient SET quantity_per_portion=@q WHERE food_id=@fid AND ingredient_id=@iid
                      ELSE
                          INSERT INTO recipe_ingredient(food_id,ingredient_id,quantity_per_portion) VALUES(@fid,@iid,@q)",
                    db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@fid", _foodId);
                    cmd.Parameters.AddWithValue("@iid", ingId);
                    cmd.Parameters.AddWithValue("@q",   qpp);
                    cmd.ExecuteNonQuery();
                }
                db.CloseCon();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Saqlashda xatolik: " + ex.Message); }
        }

        private static Label Lbl(string text, int y) =>
            new Label { Text = text, Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(0, y) };
    }
}
