using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.payment
{
    public partial class PaymentPanel : UserControl
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain   = Color.FromArgb(248, 249, 250);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted= Color.FromArgb(107, 114, 128);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color Blue     = Color.FromArgb(59, 130, 246);

        private Panel _listPanel;
        private TextBox _tbNewName;

        public PaymentPanel()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.BackColor = BgMain;

            // ── HEADER ───────────────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "To'lov turlari",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16)
            });
            Label lblHint = new Label
            {
                Text = "Tartibni ↑↓ tugmalar bilan o'zgartiring. Birinchi tur default hisoblanadi.",
                Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true
            };
            header.Controls.Add(lblHint);
            header.Resize += (s, e) =>
                lblHint.Location = new Point(header.Width - lblHint.Width - 24, (header.Height - lblHint.Height) / 2);

            // ── ADD BAR ───────────────────────────────────────────────────────
            Panel addBar = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = CardBg };
            addBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, addBar.Height - 1, addBar.Width, addBar.Height - 1);

            addBar.Controls.Add(new Label
            {
                Text = "Yangi to'lov turi:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 20)
            });

            _tbNewName = new TextBox
            {
                Location = new Point(160, 16), Width = 280, Height = 30,
                Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle,
                ForeColor = TextMuted, Text = "Masalan: Naqd pul, Click, Uzum..."
            };
            _tbNewName.Enter += (s, e) =>
            {
                if (_tbNewName.ForeColor == TextMuted) { _tbNewName.Text = ""; _tbNewName.ForeColor = TextDark; }
            };
            _tbNewName.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_tbNewName.Text))
                { _tbNewName.Text = "Masalan: Naqd pul, Click, Uzum..."; _tbNewName.ForeColor = TextMuted; }
            };
            _tbNewName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) AddPayment(); };
            addBar.Controls.Add(_tbNewName);

            Button btnAdd = new Button
            {
                Text = "+ Qo'shish", Width = 110, Height = 32, Location = new Point(452, 14),
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (s, e) => AddPayment();
            addBar.Controls.Add(btnAdd);

            // ── LIST ─────────────────────────────────────────────────────────
            Panel scrollWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Padding = new Padding(24, 20, 24, 20), AutoScroll = true };
            _listPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, AutoScroll = true };
            scrollWrap.Controls.Add(_listPanel);

            this.Controls.Add(scrollWrap);
            this.Controls.Add(addBar);
            this.Controls.Add(header);
        }

        private void PaymentPanel_Load(object sender, EventArgs e)
        {
            EnsureColumn();
            LoadPayments();
        }

        private void EnsureColumn()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (SqlCommand cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns
                        WHERE object_id=OBJECT_ID('payment') AND name='sort_order')
                    ALTER TABLE payment ADD sort_order INT NULL", db.GetCon()))
                    cmd.ExecuteNonQuery();
                // Initialize sort_order for rows that have NULL
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE payment SET sort_order = id WHERE sort_order IS NULL", db.GetCon()))
                    cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        private void LoadPayments()
        {
            _listPanel.SuspendLayout();
            _listPanel.Controls.Clear();

            try
            {
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    "SELECT id, name, ISNULL(sort_order, id) AS sort_order FROM payment ORDER BY ISNULL(sort_order, id), id",
                    new dbconnect().GetCon()))
                    da.Fill(dt);

                int totalRows = dt.Rows.Count;
                int y = 0;

                for (int i = 0; i < totalRows; i++)
                {
                    DataRow row = dt.Rows[i];
                    int    pid  = Convert.ToInt32(row["id"]);
                    string name = row["name"].ToString();
                    int    sord = Convert.ToInt32(row["sort_order"]);
                    bool   isFirst = (i == 0);
                    bool   isLast  = (i == totalRows - 1);

                    // Next/prev sort_order for swap
                    int prevSord = isFirst ? -1 : Convert.ToInt32(dt.Rows[i - 1]["sort_order"]);
                    int prevId   = isFirst ? -1 : Convert.ToInt32(dt.Rows[i - 1]["id"]);
                    int nextSord = isLast  ? -1 : Convert.ToInt32(dt.Rows[i + 1]["sort_order"]);
                    int nextId   = isLast  ? -1 : Convert.ToInt32(dt.Rows[i + 1]["id"]);

                    _listPanel.Controls.Add(MakeRow(pid, name, sord, prevId, prevSord, nextId, nextSord, isFirst, isLast, i == 0, ref y));
                }

                if (dt.Rows.Count == 0)
                    _listPanel.Controls.Add(new Label
                    {
                        Text = "Hali hech qanday to'lov turi qo'shilmagan",
                        Font = new Font("Segoe UI", 12), ForeColor = TextMuted,
                        AutoSize = true, Location = new Point(0, 24)
                    });
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { _listPanel.ResumeLayout(); }
        }

        private Panel MakeRow(int pid, string name, int sord,
            int prevId, int prevSord, int nextId, int nextSord,
            bool isFirst, bool isLast, bool isDefault, ref int y)
        {
            Panel row = new Panel
            {
                Location = new Point(0, y),
                Height = 56, BackColor = CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Paint += (s, e) =>
            {
                Color stripe = isDefault ? Success : Gold;
                e.Graphics.FillRectangle(new SolidBrush(stripe), 0, 10, 4, 36);
                e.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);
            };

            // Order index badge
            Label lblNum = new Label
            {
                Text = (y / 56 + 1).ToString(),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = isDefault ? Success : TextMuted,
                Width = 30, Height = 56, Location = new Point(16, 0),
                TextAlign = ContentAlignment.MiddleCenter, AutoSize = false
            };
            row.Controls.Add(lblNum);

            // Default badge
            if (isDefault)
            {
                Label lbDef = new Label
                {
                    Text = "DEFAULT",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Color.White, BackColor = Success,
                    AutoSize = false, Width = 64, Height = 18,
                    Location = new Point(54, 9), TextAlign = ContentAlignment.MiddleCenter
                };
                row.Controls.Add(lbDef);
            }

            // Name label
            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = false, Height = 56,
                Location = new Point(isDefault ? 126 : 54, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            row.Controls.Add(lblName);

            // ↑ ↓ move buttons
            Button btnUp   = MakeBtn("↑", Color.FromArgb(99, 102, 241), 36, 30);
            Button btnDown = MakeBtn("↓", Color.FromArgb(99, 102, 241), 36, 30);
            Button btnEdit = MakeBtn("Tahrirlash", Blue, 110, 30);
            Button btnDel  = MakeBtn("O'chirish",  Danger, 100, 30);

            btnUp.Enabled   = !isFirst;
            btnDown.Enabled = !isLast;
            if (!isFirst) { btnUp.BackColor   = Color.FromArgb(99, 102, 241); btnUp.ForeColor   = Color.White; }
            else          { btnUp.BackColor   = Color.FromArgb(200, 200, 210); btnUp.ForeColor   = Color.FromArgb(160,160,175); }
            if (!isLast)  { btnDown.BackColor = Color.FromArgb(99, 102, 241); btnDown.ForeColor = Color.White; }
            else          { btnDown.BackColor = Color.FromArgb(200, 200, 210); btnDown.ForeColor = Color.FromArgb(160,160,175); }

            row.Controls.Add(btnUp);
            row.Controls.Add(btnDown);
            row.Controls.Add(btnEdit);
            row.Controls.Add(btnDel);

            Action positionBtns = () =>
            {
                int rx = row.Width - 24;
                btnDel.Location  = new Point(rx - btnDel.Width, 13);
                btnEdit.Location = new Point(btnDel.Left - btnEdit.Width - 6, 13);
                btnDown.Location = new Point(btnEdit.Left - btnDown.Width - 6, 13);
                btnUp.Location   = new Point(btnDown.Left - btnUp.Width - 4, 13);
                lblName.Width    = btnUp.Left - lblName.Left - 12;
            };
            positionBtns();
            row.Resize += (s, e) => positionBtns();
            _listPanel.Resize += (s, e) => { row.Width = _listPanel.Width; positionBtns(); };
            row.Width = _listPanel.Width > 0 ? _listPanel.Width : 800;

            // Move up: swap sort_order with previous row
            btnUp.Click += (s, e) =>
            {
                try
                {
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE payment SET sort_order=CASE id WHEN @a THEN @sb WHEN @b THEN @sa END WHERE id IN (@a,@b)",
                        db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@a", pid);
                        cmd.Parameters.AddWithValue("@sa", sord);
                        cmd.Parameters.AddWithValue("@b", prevId);
                        cmd.Parameters.AddWithValue("@sb", prevSord);
                        cmd.ExecuteNonQuery();
                    }
                    db.CloseCon();
                    LoadPayments();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };

            // Move down: swap sort_order with next row
            btnDown.Click += (s, e) =>
            {
                try
                {
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE payment SET sort_order=CASE id WHEN @a THEN @sb WHEN @b THEN @sa END WHERE id IN (@a,@b)",
                        db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@a", pid);
                        cmd.Parameters.AddWithValue("@sa", sord);
                        cmd.Parameters.AddWithValue("@b", nextId);
                        cmd.Parameters.AddWithValue("@sb", nextSord);
                        cmd.ExecuteNonQuery();
                    }
                    db.CloseCon();
                    LoadPayments();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };

            // Delete
            btnDel.Click += (s, e) =>
            {
                if (MessageBox.Show($"'{name}' ni o'chirishni tasdiqlaysizmi?", "O'chirish",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM payment WHERE id=@id", db.GetCon()))
                    { cmd.Parameters.AddWithValue("@id", pid); cmd.ExecuteNonQuery(); }
                    db.CloseCon();
                    LoadPayments();
                }
                catch (Exception ex) { MessageBox.Show("O'chirishda xatolik: " + ex.Message); }
            };

            // Inline edit
            btnEdit.Click += (s, e) =>
            {
                TextBox tbEdit = new TextBox
                {
                    Text = name, Font = new Font("Segoe UI", 11),
                    Location = new Point(lblName.Left, 14), Width = lblName.Width, Height = 28,
                    BorderStyle = BorderStyle.FixedSingle
                };
                Button btnSave   = MakeBtn("Saqlash", Success, 90, 30);
                Button btnCancel = MakeBtn("Bekor", Color.FromArgb(107, 114, 128), 74, 30);

                Action pos2 = () =>
                {
                    int rx2 = row.Width - 24;
                    btnCancel.Location = new Point(rx2 - btnCancel.Width, 13);
                    btnSave.Location   = new Point(btnCancel.Left - btnSave.Width - 6, 13);
                    tbEdit.Width       = btnSave.Left - tbEdit.Left - 12;
                };
                pos2();
                EventHandler rh = (rs, re) => pos2();
                row.Resize += rh;

                row.Controls.Remove(lblName);
                row.Controls.Remove(btnEdit);
                row.Controls.Remove(btnDel);
                row.Controls.Remove(btnUp);
                row.Controls.Remove(btnDown);
                row.Controls.Add(tbEdit);
                row.Controls.Add(btnSave);
                row.Controls.Add(btnCancel);
                tbEdit.Focus(); tbEdit.SelectAll();

                Action cancel = () =>
                {
                    row.Resize -= rh;
                    row.Controls.Remove(tbEdit);
                    row.Controls.Remove(btnSave);
                    row.Controls.Remove(btnCancel);
                    row.Controls.Add(lblName);
                    row.Controls.Add(btnEdit);
                    row.Controls.Add(btnDel);
                    row.Controls.Add(btnUp);
                    row.Controls.Add(btnDown);
                    positionBtns();
                };

                btnCancel.Click += (s2, e2) => cancel();
                tbEdit.KeyDown  += (s2, e2) => { if (e2.KeyCode == Keys.Escape) cancel(); };

                Action save = () =>
                {
                    string newName = tbEdit.Text.Trim();
                    if (string.IsNullOrEmpty(newName)) { MessageBox.Show("Nom bo'sh bo'lishi mumkin emas."); return; }
                    try
                    {
                        dbconnect db = new dbconnect();
                        db.OpenCon();
                        using (SqlCommand cmd = new SqlCommand("UPDATE payment SET name=@n WHERE id=@id", db.GetCon()))
                        { cmd.Parameters.AddWithValue("@n", newName); cmd.Parameters.AddWithValue("@id", pid); cmd.ExecuteNonQuery(); }
                        db.CloseCon();
                        LoadPayments();
                    }
                    catch (Exception ex) { MessageBox.Show("Saqlashda xatolik: " + ex.Message); }
                };
                btnSave.Click  += (s2, e2) => save();
                tbEdit.KeyDown += (s2, e2) => { if (e2.KeyCode == Keys.Enter) save(); };
            };

            y += 56;
            return row;
        }

        private void AddPayment()
        {
            string name = _tbNewName.ForeColor == TextMuted ? "" : _tbNewName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("To'lov turi nomini kiriting.", "Ogohlantirish",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _tbNewName.Focus(); return;
            }
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                // sort_order = current max + 1 so it goes to the end
                using (SqlCommand cmd = new SqlCommand(
                    "INSERT INTO payment(name, sort_order) SELECT @n, ISNULL(MAX(sort_order),0)+1 FROM payment",
                    db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.ExecuteNonQuery();
                }
                db.CloseCon();
                _tbNewName.Text = "Masalan: Naqd pul, Click, Uzum...";
                _tbNewName.ForeColor = TextMuted;
                LoadPayments();
            }
            catch (Exception ex) { MessageBox.Show("Qo'shishda xatolik: " + ex.Message); }
        }

        private static Button MakeBtn(string text, Color bg, int w, int h)
        {
            Button b = new Button
            {
                Text = text, Width = w, Height = h,
                FlatStyle = FlatStyle.Flat, BackColor = bg,
                ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
