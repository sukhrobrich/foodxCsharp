using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.warehouse
{
    public class PurchaseForm : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);

        private readonly int _preselectedId;
        private ComboBox      cboIngredient;
        private NumericUpDown numQty, numPrice;
        private TextBox       txtNotes;
        private Label         lblTotal;

        public PurchaseForm(int preselectedIngredientId)
        {
            _preselectedId = preselectedIngredientId;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(460, 440);
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
                Text = "Xarid qo'shish",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(0, y)
            });
            y += 40;

            // Ingredient
            main.Controls.Add(Lbl("Masalliq *", y)); y += 22;
            cboIngredient = new ComboBox
            {
                Location = new Point(0, y), Width = 404, Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage
            };
            main.Controls.Add(cboIngredient);
            y += 46;

            // Qty + Price
            main.Controls.Add(Lbl("Miqdor *", y));
            main.Controls.Add(new Label { Text = "Narx / birlik (so'm) *", Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(210, y) });
            y += 22;
            numQty   = NumField(main, 0,   y, 200);
            numPrice = NumField(main, 210, y, 194);
            numQty.DecimalPlaces   = 3;
            numPrice.DecimalPlaces = 2;
            numQty.Maximum = numPrice.Maximum = 999999999;
            AllowDotAsDecimal(numQty);
            AllowDotAsDecimal(numPrice);
            numQty.ValueChanged   += UpdateTotal;
            numPrice.ValueChanged += UpdateTotal;
            y += 46;

            // Total
            main.Controls.Add(Lbl("Jami summa", y)); y += 22;
            lblTotal = new Label
            {
                Text = "0 so'm",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true,
                Location = new Point(0, y)
            };
            main.Controls.Add(lblTotal);
            y += 46;

            // Notes
            main.Controls.Add(Lbl("Izoh (ixtiyoriy)", y)); y += 22;
            txtNotes = new TextBox
            {
                Location = new Point(0, y), Width = 404, Height = 32,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark
            };
            main.Controls.Add(txtNotes);
            y += 50;

            // Buttons
            Button btnSave = new Button
            {
                Text = "Xaridni saqlash",
                Location = new Point(0, y), Width = 200, Height = 40,
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
                Location = new Point(210, y), Width = 194, Height = 40,
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
                using (var da = new SqlDataAdapter("SELECT id, name+' ('+unit+')' AS display FROM ingredient ORDER BY name", db.GetCon()))
                    da.Fill(dt);

                cboIngredient.DataSource    = dt;
                cboIngredient.DisplayMember = "display";
                cboIngredient.ValueMember   = "id";

                if (_preselectedId > 0)
                {
                    foreach (DataRowView row in cboIngredient.Items)
                    {
                        if (Convert.ToInt32(row["id"]) == _preselectedId)
                        {
                            cboIngredient.SelectedItem = row;
                            break;
                        }
                    }

                    // Pre-fill last known price
                    try
                    {
                        db.OpenCon();
                        using (var cmd = new SqlCommand("SELECT price_per_unit FROM ingredient WHERE id=@id", db.GetCon()))
                        {
                            cmd.Parameters.AddWithValue("@id", _preselectedId);
                            object val = cmd.ExecuteScalar();
                            if (val != null) numPrice.Value = Convert.ToDecimal(val);
                        }
                        db.CloseCon();
                    }
                    catch { }
                }
            }
            catch (Exception ex) { MessageBox.Show("Masalliqlarni yuklashda xatolik: " + ex.Message); }
        }

        private void UpdateTotal(object sender, EventArgs e)
        {
            decimal total = numQty.Value * numPrice.Value;
            lblTotal.Text = total.ToString("N0") + " so'm";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (cboIngredient.SelectedValue == null)
            {
                MessageBox.Show("Masalliqni tanlang.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (numQty.Value <= 0)
            {
                MessageBox.Show("Miqdorni kiriting.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                numQty.Focus();
                return;
            }
            if (numPrice.Value <= 0)
            {
                MessageBox.Show("Narxni kiriting.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                numPrice.Focus();
                return;
            }

            int    ingId = Convert.ToInt32(cboIngredient.SelectedValue);
            decimal qty  = numQty.Value;
            decimal ppu  = numPrice.Value;
            decimal tot  = qty * ppu;
            string notes = txtNotes.Text.Trim();

            try
            {
                var db = new dbconnect();
                db.OpenCon();

                int newPurchaseId;
                using (var cmd = new SqlCommand(
                    "INSERT INTO ingredient_purchase(ingredient_id,quantity,price_per_unit,total_price,notes) OUTPUT INSERTED.id VALUES(@i,@q,@p,@t,@n)",
                    db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@i", ingId);
                    cmd.Parameters.AddWithValue("@q", qty);
                    cmd.Parameters.AddWithValue("@p", ppu);
                    cmd.Parameters.AddWithValue("@t", tot);
                    cmd.Parameters.AddWithValue("@n", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                    newPurchaseId = (int)cmd.ExecuteScalar();
                }

                // Update stock and price
                using (var cmd = new SqlCommand(
                    "UPDATE ingredient SET quantity=quantity+@q, price_per_unit=@p WHERE id=@i",
                    db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@q", qty);
                    cmd.Parameters.AddWithValue("@p", ppu);
                    cmd.Parameters.AddWithValue("@i", ingId);
                    cmd.ExecuteNonQuery();
                }

                db.CloseCon();
                // Darhol sync trigger
                WindowsFormsApp1.services.SyncQueueHelper.Add(
                    WindowsFormsApp1.services.SyncQueueHelper.IngredientPurchases, newPurchaseId, "Insert");
                System.Threading.Tasks.Task.Run(() => WindowsFormsApp1.services.SyncEngine.SyncAll());
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MessageBox.Show("Saqlashda xatolik: " + ex.Message); }
        }

        private static Label Lbl(string text, int y) =>
            new Label { Text = text, Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(0, y) };

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

        private static void AllowDotAsDecimal(NumericUpDown nud)
        {
            char sep = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            nud.KeyPress += (s, e) => { if (e.KeyChar == '.' || e.KeyChar == ',') e.KeyChar = sep; };
        }
    }
}
