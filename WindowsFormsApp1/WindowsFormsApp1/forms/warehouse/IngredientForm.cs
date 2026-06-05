using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.warehouse
{
    public class IngredientForm : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);

        private readonly int _id;
        private TextBox       txtName;
        private NumericUpDown numQty, numPrice, numMin;
        private ComboBox      cboUnit;

        private static readonly string[] Units = { "kg", "gr", "litr", "tonna", "m", "mm", "mg", "dona" };

        public IngredientForm(int id)
        {
            _id = id;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(440, 420);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = Color.White;
            BuildUI();
            if (id > 0) LoadExisting();
        }

        private void BuildUI()
        {
            // Gold top accent
            this.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Gold });

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 20, 28, 20) };
            this.Controls.Add(main);

            int y = 16;

            // Title
            main.Controls.Add(new Label
            {
                Text = _id == 0 ? "Yangi masalliq qo'shish" : "Masalliqni tahrirlash",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(0, y)
            });
            y += 40;

            // Name
            main.Controls.Add(Lbl("Masalliq nomi *", y)); y += 22;
            txtName = Field(main, y); y += 46;

            // Unit row
            main.Controls.Add(Lbl("Birlik (o'lchov birligi) *", y)); y += 22;
            cboUnit = new ComboBox
            {
                Location = new Point(0, y), Width = 180, Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage
            };
            cboUnit.Items.AddRange(Units);
            cboUnit.SelectedIndex = 0;
            main.Controls.Add(cboUnit);
            y += 46;

            // Qty + Price row
            main.Controls.Add(Lbl("Joriy miqdor", y));
            main.Controls.Add(new Label { Text = "Narx / birlik (so'm)", Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(196, y) });
            y += 22;
            numQty   = NumField(main, 0, y, 180);
            numPrice = NumField(main, 196, y, 188);
            numQty.DecimalPlaces   = 3;
            numPrice.DecimalPlaces = 2;
            numQty.Maximum = numPrice.Maximum = 999999999;
            AllowDotAsDecimal(numQty);
            AllowDotAsDecimal(numPrice);
            y += 46;

            // Min qty
            main.Controls.Add(Lbl("Minimum miqdor (ogohlantirish uchun)", y)); y += 22;
            numMin = NumField(main, 0, y, 180);
            numMin.DecimalPlaces = 3;
            numMin.Maximum = 999999999;
            AllowDotAsDecimal(numMin);
            y += 54;

            // Buttons
            Button btnSave = new Button
            {
                Text = _id == 0 ? "Saqlash" : "Yangilash",
                Location = new Point(0, y), Width = 180, Height = 40,
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
                Location = new Point(196, y), Width = 188, Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextMuted,
                Font = new Font("Segoe UI", 11), Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Border;
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            main.Controls.Add(btnCancel);

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private void LoadExisting()
        {
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand("SELECT name,unit,quantity,price_per_unit,min_quantity FROM ingredient WHERE id=@id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@id", _id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            txtName.Text       = r["name"].ToString();
                            numQty.Value       = Convert.ToDecimal(r["quantity"]);
                            numPrice.Value     = Convert.ToDecimal(r["price_per_unit"]);
                            numMin.Value       = Convert.ToDecimal(r["min_quantity"]);
                            string u = r["unit"].ToString();
                            int idx = Array.IndexOf(Units, u);
                            cboUnit.SelectedIndex = idx >= 0 ? idx : 0;
                        }
                    }
                }
                db.CloseCon();
            }
            catch (Exception ex) { MessageBox.Show("Yuklashda xatolik: " + ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Masalliq nomini kiriting.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                if (_id == 0)
                {
                    int newId = 0;
                    using (var cmd = new SqlCommand(
                        "INSERT INTO ingredient(name,unit,quantity,price_per_unit,min_quantity) VALUES(@n,@u,@q,@p,@m); SELECT SCOPE_IDENTITY();",
                        db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@u", cboUnit.SelectedItem.ToString());
                        cmd.Parameters.AddWithValue("@q", numQty.Value);
                        cmd.Parameters.AddWithValue("@p", numPrice.Value);
                        cmd.Parameters.AddWithValue("@m", numMin.Value);
                        object scalarResult = cmd.ExecuteScalar();
                        if (scalarResult != null && scalarResult != DBNull.Value)
                            newId = Convert.ToInt32(scalarResult);
                    }
                    // Boshlang'ich miqdor xarajat sifatida kiritiladi
                    if (newId > 0 && numQty.Value > 0 && numPrice.Value > 0)
                    {
                        decimal total = numQty.Value * numPrice.Value;
                        using (var cmd = new SqlCommand(
                            "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit,total_price,purchased_at) VALUES(@iid,@qty,@ppu,@tot,GETDATE())",
                            db.GetCon()))
                        {
                            cmd.Parameters.AddWithValue("@iid", newId);
                            cmd.Parameters.AddWithValue("@qty", numQty.Value);
                            cmd.Parameters.AddWithValue("@ppu", numPrice.Value);
                            cmd.Parameters.AddWithValue("@tot", total);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand(
                        "UPDATE ingredient SET name=@n,unit=@u,quantity=@q,price_per_unit=@p,min_quantity=@m WHERE id=@id",
                        db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@u", cboUnit.SelectedItem.ToString());
                        cmd.Parameters.AddWithValue("@q", numQty.Value);
                        cmd.Parameters.AddWithValue("@p", numPrice.Value);
                        cmd.Parameters.AddWithValue("@m", numMin.Value);
                        cmd.Parameters.AddWithValue("@id", _id);
                        cmd.ExecuteNonQuery();
                    }
                }
                db.CloseCon();
                if (Session.IsOnline && Session.TenantId > 0)
                    System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Saqlashda xatolik: " + ex.Message); }
        }

        private static Label Lbl(string text, int y) =>
            new Label { Text = text, Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(0, y) };

        private static TextBox Field(Panel parent, int y)
        {
            var tb = new TextBox
            {
                Location = new Point(0, y), Width = 384, Height = 32,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark
            };
            parent.Controls.Add(tb);
            return tb;
        }

        private static void AllowDotAsDecimal(NumericUpDown nud)
        {
            char sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            nud.KeyPress += (s, e) => { if (e.KeyChar == '.' || e.KeyChar == ',') e.KeyChar = sep; };
        }

        private static NumericUpDown NumField(Panel parent, int x, int y, int w)
        {
            var n = new NumericUpDown
            {
                Location = new Point(x, y), Width = w, Height = 32,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark,
                ThousandsSeparator = true, Minimum = 0
            };
            parent.Controls.Add(n);
            return n;
        }
    }
}
