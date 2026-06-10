using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.customer
{
    public class CustomerPanel : UserControl
    {
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted= Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);

        private DataGridView grid;
        private TextBox txtSearch;

        public CustomerPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = BgPage;
            BuildUI();
            this.Load += (s, e) => LoadData();
        }

        private void BuildUI()
        {
            // Header bar
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBg };
            bar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, bar.Height - 1, bar.Width, bar.Height - 1);

            bar.Controls.Add(new Label
            {
                Text = "Mijozlar", Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark, Location = new Point(24, 18), AutoSize = true
            });

            txtSearch = new TextBox
            {
                Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle,
                Width = 220, Height = 30
            };
            txtSearch.TextChanged += (s, e) => LoadData(txtSearch.Text);

            Button btnAdd = new Button
            {
                Text = "+ Yangi mijoz", Height = 36, Width = 140,
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => ShowEditDialog(0, "", "", "", "", "");

            bar.Controls.Add(txtSearch);
            bar.Controls.Add(btnAdd);
            bar.Resize += (s, e) =>
            {
                btnAdd.Location = new Point(bar.Width - 156, 14);
                txtSearch.Location = new Point(bar.Width - 386, 17);
            };

            // Grid
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = BgPage,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 44 },
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                Font = new Font("Segoe UI", 10),
                GridColor = Border
            };
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
            grid.ColumnHeadersDefaultCellStyle.BackColor = CardBg;
            grid.DefaultCellStyle.SelectionBackColor = GoldBg;
            grid.DefaultCellStyle.SelectionForeColor = TextDark;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelected(); };

            // Action buttons row
            Panel actBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = CardBg };
            actBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 0, actBar.Width, 0);

            Button btnEdit = new Button
            {
                Text = "Tahrirlash", Location = new Point(16, 10), Height = 36, Width = 130,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
            };
            btnEdit.FlatAppearance.BorderSize = 1;
            btnEdit.FlatAppearance.BorderColor = Border;
            btnEdit.Click += (s, e) => EditSelected();

            Button btnDel = new Button
            {
                Text = "O'chirish", Location = new Point(158, 10), Height = 36, Width = 120,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(254, 226, 226),
                ForeColor = Danger, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderSize = 1;
            btnDel.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
            btnDel.Click += (s, e) => DeleteSelected();

            actBar.Controls.Add(btnEdit);
            actBar.Controls.Add(btnDel);

            this.Controls.Add(grid);
            this.Controls.Add(actBar);
            this.Controls.Add(bar);
        }

        private void LoadData(string filter = "")
        {
            try
            {
                var db = new dbconnect();
                string sql = @"SELECT id, name, ISNULL(phone,'') AS phone, ISNULL(notes,'') AS notes,
                               ISNULL(login,'') AS login,
                               FORMAT(created_at,'dd.MM.yyyy') AS sana
                               FROM customer
                               WHERE (@f='' OR name LIKE '%'+@f+'%' OR phone LIKE '%'+@f+'%')
                               ORDER BY name";
                var dt = new DataTable();
                using (var da = new SqlDataAdapter(sql, db.GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@f", filter ?? "");
                    da.Fill(dt);
                }
                grid.DataSource = dt;
                if (grid.Columns.Contains("id"))    grid.Columns["id"].Visible = false;
                if (grid.Columns.Contains("name"))  { grid.Columns["name"].HeaderText  = "Ism";       grid.Columns["name"].FillWeight  = 28; }
                if (grid.Columns.Contains("phone")) { grid.Columns["phone"].HeaderText = "Telefon";   grid.Columns["phone"].FillWeight = 22; }
                if (grid.Columns.Contains("login")) { grid.Columns["login"].HeaderText = "Login";     grid.Columns["login"].FillWeight = 20; }
                if (grid.Columns.Contains("notes")) { grid.Columns["notes"].HeaderText = "Izoh";      grid.Columns["notes"].FillWeight = 20; }
                if (grid.Columns.Contains("sana"))  { grid.Columns["sana"].HeaderText  = "Qo'shilgan"; grid.Columns["sana"].FillWeight = 10; }
            }
            catch { }
        }

        private void EditSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            var row = grid.SelectedRows[0];
            int id = Convert.ToInt32(row.Cells["id"].Value);
            ShowEditDialog(id,
                row.Cells["name"].Value?.ToString() ?? "",
                row.Cells["phone"].Value?.ToString() ?? "",
                row.Cells["notes"].Value?.ToString() ?? "",
                row.Cells["login"].Value?.ToString() ?? "",
                "");
        }

        private void DeleteSelected()
        {
            if (grid.SelectedRows.Count == 0) return;
            int id = Convert.ToInt32(grid.SelectedRows[0].Cells["id"].Value);
            string name = grid.SelectedRows[0].Cells["name"].Value?.ToString() ?? "";
            if (MessageBox.Show($"'{name}' ni o'chirishni tasdiqlaysizmi?", "O'chirish",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                var db = new dbconnect(); db.OpenCon();
                // Central_id ni olamiz — agar synced bo'lsa, centraldan ham o'chiramiz
                int? centralId = null;
                using (var chk = new SqlCommand("SELECT central_id FROM customer WHERE id=@id", db.GetCon()))
                {
                    chk.Parameters.AddWithValue("@id", id);
                    object v = chk.ExecuteScalar();
                    if (v != null && v != DBNull.Value) centralId = Convert.ToInt32(v);
                }
                new SqlCommand("DELETE FROM customer WHERE id=@id", db.GetCon())
                    { Parameters = { new SqlParameter("@id", id) } }.ExecuteNonQuery();
                db.CloseCon();

                // Centraldan ham o'chirish (agar sync bo'lgan bo'lsa)
                if (centralId.HasValue && Session.IsOnline
                    && Session.TenantId > 0)
                {
                    try
                    {
                        using (var central = dbconnect.OpenCentralForSync(
                            Session.TenantId))
                        using (var cmd = new SqlCommand("DELETE FROM customer WHERE id=@id", central))
                        {
                            cmd.Parameters.AddWithValue("@id", centralId.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch { }
                }

                LoadData(txtSearch.Text);
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void ShowEditDialog(int id, string name, string phone, string notes, string login, string password)
        {
            bool isNew = id == 0;
            Form dlg = new Form
            {
                Text = isNew ? "Yangi mijoz" : "Mijozni tahrirlash",
                Width = 380, Height = 420,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };

            void Lbl(string t, int y) => dlg.Controls.Add(new Label { Text = t, Location = new Point(14, y), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            TextBox Txt(string val, int y, bool pw = false)
            {
                var t = new TextBox { Text = val, Location = new Point(14, y), Width = 330, Height = 30, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
                if (pw) t.PasswordChar = '●';
                dlg.Controls.Add(t);
                return t;
            }

            Lbl("Ism *", 14);                                                       var txtName  = Txt(name,  32);
            Lbl("Telefon", 72);                                                     var txtPhone = Txt(phone, 90);
            Lbl("Izoh",   130);                                                     var txtNotes = Txt(notes, 148);
            Lbl("Login",  188);                                                     var txtLogin = Txt(login, 206);
            Lbl(isNew ? "Parol" : "Parol (bo'sh qoldiring — o'zgarmaydi)", 246);   var txtPwd   = Txt("",    264, pw: true);

            var btnOk = new Button
            {
                Text = isNew ? "Qo'shish" : "Saqlash",
                Location = new Point(14, 310), Width = 330, Height = 42,
                FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Ism kiritilishi shart!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                try
                {
                    var db = new dbconnect(); db.OpenCon();
                    int savedId = id;
                    string loginVal = txtLogin.Text.Trim();
                    string pwPlain  = txtPwd.Text.Trim();
                    string pwHashed = string.IsNullOrEmpty(pwPlain) ? null : Session.HashPin(pwPlain);

                    if (isNew)
                    {
                        var insCmd = new SqlCommand(
                            "INSERT INTO customer(name,phone,notes,login,password) OUTPUT INSERTED.id VALUES(@n,@p,@nt,@lg,@pw)",
                            db.GetCon());
                        insCmd.Parameters.AddWithValue("@n",  txtName.Text.Trim());
                        insCmd.Parameters.AddWithValue("@p",  string.IsNullOrEmpty(txtPhone.Text.Trim()) ? (object)DBNull.Value : txtPhone.Text.Trim());
                        insCmd.Parameters.AddWithValue("@nt", string.IsNullOrEmpty(txtNotes.Text.Trim()) ? (object)DBNull.Value : txtNotes.Text.Trim());
                        insCmd.Parameters.AddWithValue("@lg", string.IsNullOrEmpty(loginVal) ? (object)DBNull.Value : loginVal);
                        insCmd.Parameters.AddWithValue("@pw", pwHashed == null ? (object)DBNull.Value : pwHashed);
                        savedId = (int)insCmd.ExecuteScalar();
                    }
                    else
                    {
                        string pwSql = pwHashed != null ? ", password=@pw" : "";
                        var updCmd = new SqlCommand(
                            $"UPDATE customer SET name=@n, phone=@p, notes=@nt, login=@lg{pwSql}, is_synced=0 WHERE id=@id",
                            db.GetCon());
                        updCmd.Parameters.AddWithValue("@n",  txtName.Text.Trim());
                        updCmd.Parameters.AddWithValue("@p",  string.IsNullOrEmpty(txtPhone.Text.Trim()) ? (object)DBNull.Value : txtPhone.Text.Trim());
                        updCmd.Parameters.AddWithValue("@nt", string.IsNullOrEmpty(txtNotes.Text.Trim()) ? (object)DBNull.Value : txtNotes.Text.Trim());
                        updCmd.Parameters.AddWithValue("@lg", string.IsNullOrEmpty(loginVal) ? (object)DBNull.Value : loginVal);
                        if (pwHashed != null) updCmd.Parameters.AddWithValue("@pw", pwHashed);
                        updCmd.Parameters.AddWithValue("@id", id);
                        updCmd.ExecuteNonQuery();
                    }
                    db.CloseCon();
                    SyncQueueHelper.Add(SyncQueueHelper.Customers, savedId, isNew ? "Insert" : "Update");
                    dlg.DialogResult = DialogResult.OK;
                    LoadData(txtSearch.Text);
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };
            dlg.Controls.Add(btnOk);
            dlg.ShowDialog(this.FindForm());
        }

    }
}
