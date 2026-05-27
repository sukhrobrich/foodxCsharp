using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.cashflow
{
    public class CashFlowForm : Form
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);

        private readonly string _defaultType; // "KIRIM" or "CHIQIM"
        private ComboBox  cboType;
        private ComboBox  cboCategory;
        private TextBox   txtAmount;
        private TextBox   txtDescription;

        private static readonly string[] KirimCategories  = { "Kassa kirim", "Boshqa kirim" };
        private static readonly string[] ChiqimCategories = { "Ofitsiant maoshi", "Kassa chiqim", "Boshqa chiqim" };

        public CashFlowForm(string defaultType = "CHIQIM")
        {
            _defaultType = defaultType;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size            = new Size(440, 380);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = Color.White;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Gold });

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 20, 28, 20) };
            this.Controls.Add(main);

            int y = 16;

            main.Controls.Add(new Label
            {
                Text = "Kassa operatsiyasi",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(0, y)
            });
            y += 44;

            // Type
            main.Controls.Add(Lbl("Tur *", y)); y += 22;
            cboType = new ComboBox
            {
                Location = new Point(0, y), Width = 384, Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage
            };
            cboType.Items.AddRange(new[] { "KIRIM", "CHIQIM" });
            cboType.SelectedItem = _defaultType;
            cboType.SelectedIndexChanged += (s, e) => RefreshCategories();
            main.Controls.Add(cboType);
            y += 46;

            // Category
            main.Controls.Add(Lbl("Kategoriya *", y)); y += 22;
            cboCategory = new ComboBox
            {
                Location = new Point(0, y), Width = 384, Height = 32,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 11), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage
            };
            main.Controls.Add(cboCategory);
            y += 46;

            // Amount
            main.Controls.Add(Lbl("Summa (so'm) *", y)); y += 22;
            txtAmount = new TextBox
            {
                Location = new Point(0, y), Width = 384, Height = 32,
                Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark
            };
            main.Controls.Add(txtAmount);
            y += 46;

            // Description
            main.Controls.Add(Lbl("Izoh (ixtiyoriy)", y)); y += 22;
            txtDescription = new TextBox
            {
                Location = new Point(0, y), Width = 384, Height = 32,
                Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle,
                BackColor = BgPage, ForeColor = TextDark
            };
            main.Controls.Add(txtDescription);
            y += 50;

            // Buttons
            Button btnSave = new Button
            {
                Text = "Saqlash",
                Location = new Point(0, y), Width = 188, Height = 40,
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
            btnCancel.FlatAppearance.BorderSize  = 1;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            main.Controls.Add(btnCancel);

            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };

            RefreshCategories();
        }

        private void RefreshCategories()
        {
            cboCategory.Items.Clear();
            string[] cats = (cboType.SelectedItem?.ToString() == "KIRIM") ? KirimCategories : ChiqimCategories;
            cboCategory.Items.AddRange(cats);
            if (cboCategory.Items.Count > 0) cboCategory.SelectedIndex = 0;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string type = cboType.SelectedItem?.ToString();
            string cat  = cboCategory.SelectedItem?.ToString();

            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(cat))
            {
                MessageBox.Show("Tur va kategoriyani tanlang.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal amount;
            string raw = txtAmount.Text.Trim().Replace(',', '.').Replace(" ", "");
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out amount) || amount <= 0)
            {
                txtAmount.BackColor = Color.FromArgb(254, 226, 226);
                MessageBox.Show("To'g'ri summani kiriting.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtAmount.BackColor = BgPage;
                txtAmount.Focus(); txtAmount.SelectAll();
                return;
            }

            string desc = txtDescription.Text.Trim();

            try
            {
                var db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand(
                    @"INSERT INTO cash_transaction(type, category, amount, description, created_by)
                      VALUES(@t, @c, @a, @d, @u)",
                    db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@t", type);
                    cmd.Parameters.AddWithValue("@c", cat);
                    cmd.Parameters.AddWithValue("@a", amount);
                    cmd.Parameters.AddWithValue("@d", string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc);
                    cmd.Parameters.AddWithValue("@u", Session.UserId > 0 ? (object)Session.UserId : DBNull.Value);
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
