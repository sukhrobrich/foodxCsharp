using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using WindowsFormsApp1;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.food
{
    public partial class AddFoodCategory : UserControl
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg    = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain    = Color.FromArgb(248, 248, 250);
        private static readonly Color CardBg    = Color.White;
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color AvatarBg  = Color.FromArgb(240, 240, 245);

        private Panel    categoriesListPanel;
        private TextBox  txtSearch;
        private TextBox  txtName;
        private ComboBox cmbPrinter;
        private Label    lblFormTitle;
        private Panel    editPanel;
        private Button   btnFab;
        private int      editingId = 0;

        public AddFoodCategory()
        {
            InitializeComponent();
            BuildUI();
            this.Load += (s, e) => this.BeginInvoke(new Action(() => { ReorderForm.EnsureOrderColumn("food_category"); LoadCategories(); }));
        }

        // ── UI CONSTRUCTION ────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.BackColor = BgMain;

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            Label lblTitle = new Label
            {
                Text = "Kategoriyalar",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(24, 18)
            };
            header.Controls.Add(lblTitle);

            // Search box
            Panel searchWrap = new Panel { Height = 36, Width = 280, BackColor = Color.Transparent };
            Panel searchBg = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };
            searchBg.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(searchBg.ClientRectangle, 18))
                {
                    e.Graphics.FillPath(new SolidBrush(BgMain), path);
                    e.Graphics.DrawPath(new Pen(Border, 1.5f), path);
                }
            };
            Label searchIcon = new Label
            {
                Text = "⌕",
                Font = new Font("Segoe UI", 14),
                ForeColor = TextMuted,
                Location = new Point(8, 3),
                AutoSize = true
            };
            txtSearch = new TextBox
            {
                Location = new Point(32, 7),
                Width = 226,
                Height = 22,
                BorderStyle = BorderStyle.None,
                BackColor = BgMain,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 10),
                Text = "Qidirish"
            };
            txtSearch.GotFocus  += (s, e) => { if (txtSearch.Text == "Qidirish") { txtSearch.Text = ""; txtSearch.ForeColor = TextDark; } };
            txtSearch.LostFocus += (s, e) => { if (string.IsNullOrEmpty(txtSearch.Text)) { txtSearch.Text = "Qidirish"; txtSearch.ForeColor = TextMuted; } };
            txtSearch.TextChanged += (s, e) => LoadCategories(GetSearchText());
            searchBg.Controls.Add(searchIcon);
            searchBg.Controls.Add(txtSearch);
            searchWrap.Controls.Add(searchBg);
            header.Controls.Add(searchWrap);

            Button btnReorder = new Button
            {
                Text = "⇅  Tartib", Width = 100, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand,
                Visible = Session.IsAdmin
            };
            btnReorder.FlatAppearance.BorderSize = 1;
            btnReorder.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnReorder.Click += (s, e) =>
            {
                var items = ReorderForm.LoadItems("food_category");
                using (var frm = new ReorderForm("Kategoriyalar tartibi", items))
                {
                    frm.ShowDialog();
                    if (frm.Saved) { ReorderForm.SaveOrder("food_category", frm.OrderedIds); LoadCategories(GetSearchText()); }
                }
            };
            header.Controls.Add(btnReorder);
            header.Resize += (s, e) =>
            {
                searchWrap.Location = new Point(header.Width - 300, 14);
                btnReorder.Location = new Point(header.Width - 300 - 108, 15);
            };

            // === SCROLL LIST ===
            categoriesListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgMain,
                Padding = new Padding(20, 12, 20, 12)
            };
            categoriesListPanel.Resize += (s, e) => ResizeCards();

            // === RIGHT EDIT PANEL ===
            editPanel = new Panel { Width = UIScale.EditPanelXs, Dock = DockStyle.Right, BackColor = CardBg, Visible = false };
            editPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, editPanel.Height);
            BuildEditPanel();

            // === FAB (+) Button — added first so it gets highest Z-order (front) ===
            btnFab = new Button
            {
                Width = 52, Height = 52,
                BackColor = Color.FromArgb(22, 163, 74),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                Text = "+",
                Cursor = Cursors.Hand
            };
            btnFab.FlatAppearance.BorderSize = 0;
            btnFab.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(btnFab.ClientRectangle, 26))
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(22, 163, 74)), path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI", 24, FontStyle.Bold))
                    e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, btnFab.Width, btnFab.Height), sf);
            };
            btnFab.Click += (s, e) => ShowEditPanel(0, "", "");
            this.Controls.Add(btnFab);

            this.Resize += (s, e) => UpdateFabPosition();
            editPanel.VisibleChanged += (s, e) => UpdateFabPosition();

            // Docked controls — Fill first, Right second, Top LAST (= topmost) ✓
            this.Controls.Add(categoriesListPanel);
            this.Controls.Add(editPanel);
            this.Controls.Add(header);
        }

        private void UpdateFabPosition()
        {
            int rightOffset = (editPanel.Visible ? editPanel.Width : 0) + 20;
            btnFab.Location = new Point(this.Width - rightOffset - btnFab.Width, this.Height - btnFab.Height - 20);
        }

        private void BuildEditPanel()
        {
            // Close button
            Button btnClose = new Button
            {
                Text = "×", Width = 30, Height = 30,
                Location = new Point(editPanel.Width - 44, 18),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => editPanel.Visible = false;
            editPanel.Controls.Add(btnClose);
            editPanel.Resize += (s, e) => btnClose.Location = new Point(editPanel.Width - 44, 18);

            int y = 24;
            lblFormTitle = new Label
            {
                Text = "Yangi Kategoriya",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            editPanel.Controls.Add(lblFormTitle);
            y += 44;

            Panel line = new Panel { Left = 24, Top = y, Width = 312, Height = 2, BackColor = Gold };
            editPanel.Controls.Add(line);
            y += 16;

            // Name field
            editPanel.Controls.Add(new Label
            {
                Text = "Kategoriya nomi",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 22;

            Panel nameBox = new Panel { Location = new Point(24, y), Width = 312, Height = 40, BackColor = BgMain };
            nameBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(nameBox.ClientRectangle, 6))
                {
                    e.Graphics.FillPath(new SolidBrush(nameBox.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border, 1.5f), path);
                }
            };
            txtName = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.None,
                BackColor = BgMain,
                ForeColor = TextDark,
                Padding = new Padding(8, 0, 8, 0)
            };
            txtName.GotFocus  += (s, e) => { nameBox.BackColor = GoldBg; nameBox.Invalidate(); };
            txtName.LostFocus += (s, e) => { nameBox.BackColor = BgMain; nameBox.Invalidate(); };
            nameBox.Controls.Add(txtName);
            editPanel.Controls.Add(nameBox);
            y += 52;

            // Printer field
            editPanel.Controls.Add(new Label
            {
                Text = "Printer (oshxona printer)",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 22;

            cmbPrinter = new ComboBox
            {
                Location = new Point(24, y),
                Width = 312,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = BgMain
            };
            cmbPrinter.Items.Add("— Printer yo'q —");
            foreach (string pn in PrinterSettings.InstalledPrinters)
                cmbPrinter.Items.Add(pn);
            cmbPrinter.SelectedIndex = 0;
            editPanel.Controls.Add(cmbPrinter);
            y += 44;

            Label lblHint = new Label
            {
                Text = "Oshxona printeri bu kategoriyaga biriktiriladi",
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                Width = 312,
                Height = 16,
                AutoSize = false
            };
            editPanel.Controls.Add(lblHint);
            y += 28;

            Button btnSave = new Button
            {
                Text = "Saqlash",
                Location = new Point(24, y),
                Width = 312, Height = 42,
                FlatStyle = FlatStyle.Flat,
                BackColor = Gold,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveCategory;
            editPanel.Controls.Add(btnSave);
        }

        private void ShowEditPanel(int id, string name, string printer)
        {
            editingId = id;
            lblFormTitle.Text = id == 0 ? "Yangi Kategoriya" : "Kategoriyani tahrirlash";
            txtName.Text = name;
            cmbPrinter.SelectedIndex = 0;
            for (int i = 1; i < cmbPrinter.Items.Count; i++)
            {
                if (cmbPrinter.Items[i].ToString().Equals(printer, StringComparison.OrdinalIgnoreCase))
                { cmbPrinter.SelectedIndex = i; break; }
            }
            editPanel.Visible = true;
            txtName.Focus();
        }

        // ── DATA ───────────────────────────────────────────────────────────────
        private void LoadCategories(string filter = "")
        {
            categoriesListPanel.SuspendLayout();
            categoriesListPanel.Controls.Clear();
            try
            {
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    @"SELECT fc.id, fc.name, ISNULL(fc.printer_name,'') AS printer,
                             COUNT(f.id) AS product_count
                      FROM food_category fc
                      LEFT JOIN food f ON f.food_category_id = fc.id
                      GROUP BY fc.id, fc.name, fc.printer_name, fc.sort_order
                      ORDER BY ISNULL(fc.sort_order,9999), fc.name", db.GetCon()))
                    da.Fill(dt);

                int cardY = 0;
                foreach (DataRow row in dt.Rows)
                {
                    string name = row["name"].ToString();
                    if (!string.IsNullOrEmpty(filter) &&
                        name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Panel card = CreateCategoryCard(
                        Convert.ToInt32(row["id"]), name,
                        row["printer"].ToString(),
                        Convert.ToInt32(row["product_count"]));
                    card.Location = new Point(0, cardY);
                    card.Width = Math.Max(100, categoriesListPanel.ClientSize.Width - 4);
                    categoriesListPanel.Controls.Add(card);
                    cardY += card.Height + 8;
                }

                if (categoriesListPanel.Controls.Count == 0)
                    categoriesListPanel.Controls.Add(new Label
                    {
                        Text = string.IsNullOrEmpty(filter)
                               ? "Hali kategoriya qo'shilmagan"
                               : $"\"{filter}\" topilmadi",
                        Font = new Font("Segoe UI", 12),
                        ForeColor = TextMuted,
                        Location = new Point(0, 40),
                        AutoSize = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kategoriyalar yuklashda xatolik: " + ex.Message +
                    "\n\nDB migratsiya bajarilganmi? (printer_name ustuni qo'shilganmi?)", "Xatolik");
            }
            finally { categoriesListPanel.ResumeLayout(); }
        }

        private Panel CreateCategoryCard(int id, string name, string printer, int productCount)
        {
            Panel card = new Panel { Height = 72, BackColor = CardBg };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    e.Graphics.DrawPath(new Pen(Border, 1f), path);
                }
            };

            // Avatar (rounded square with initials)
            string initials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            Panel avatar = new Panel { Width = 44, Height = 44, Location = new Point(16, 14), BackColor = Color.Transparent };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(new Rectangle(0, 0, 43, 43), 10))
                    e.Graphics.FillPath(new SolidBrush(AvatarBg), path);
                using (var f = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(initials, f, new SolidBrush(TextDark), new RectangleF(0, 0, 44, 44), sf);
            };
            card.Controls.Add(avatar);

            // Name
            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(72, 13),
                AutoSize = true
            };
            card.Controls.Add(lblName);

            // Sub-info: product count + printer
            bool hasPrinter = !string.IsNullOrEmpty(printer);
            Label lblSub = new Label
            {
                Text = $"Mahsulotlar soni: {productCount}  •  " +
                       (hasPrinter ? printer : "Printer biriktirilmagan"),
                Font      = new Font("Segoe UI", 8),
                ForeColor = hasPrinter ? Gold : TextMuted,
                Location  = new Point(72, 38),
                AutoSize  = true
            };
            card.Controls.Add(lblSub);

            // Action buttons
            Button btnEdit  = MakeIconBtn("✏", TextMuted);
            Button btnDel   = MakeIconBtn("🗑", Danger);
            Button btnPrint = MakeIconBtn("🖨", hasPrinter ? Gold : TextMuted);

            btnEdit.Click  += (s, e) => ShowEditPanel(id, name, printer);
            btnPrint.Click += (s, e) => ShowEditPanel(id, name, printer);
            btnDel.Click   += (s, e) =>
            {
                if (MessageBox.Show($"'{name}' kategoriyasini o'chirishni tasdiqlaysizmi?",
                    "O'chirish", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    int foodCnt = 0;
                    using (SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM food WHERE food_category_id=@id", db.GetCon()))
                    { chk.Parameters.AddWithValue("@id", id); foodCnt = (int)chk.ExecuteScalar(); }
                    if (foodCnt > 0)
                    {
                        db.CloseCon();
                        MessageBox.Show($"Bu kategoriyada {foodCnt} ta taom bor. Avval taomlarni o'chiring yoki boshqa kategoriyaga o'tkazing.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    int? centralCatId = null;
                    using (var cid = new SqlCommand("SELECT central_id FROM food_category WHERE id=@id", db.GetCon()))
                    {
                        cid.Parameters.AddWithValue("@id", id);
                        object v = cid.ExecuteScalar();
                        if (v != null && v != DBNull.Value) centralCatId = Convert.ToInt32(v);
                    }
                    SqlCommand cmd = new SqlCommand("DELETE FROM food_category WHERE id=@id", db.GetCon());
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery(); db.CloseCon();
                    if (centralCatId.HasValue && Session.IsOnline && Session.TenantId > 0)
                        SyncQueueHelper.Add("FoodCategoryDelete", centralCatId.Value, "Delete");
                    LoadCategories(GetSearchText());
                }
                catch (Exception ex) { MessageBox.Show("O'chirishda xatolik: " + ex.Message); }
            };

            card.Controls.Add(btnEdit);
            card.Controls.Add(btnDel);
            card.Controls.Add(btnPrint);

            Action positionBtns = () =>
            {
                int rx = card.Width - 16;
                btnPrint.Location = new Point(rx - 36, (card.Height - 32) / 2);
                btnDel.Location   = new Point(rx - 76, (card.Height - 32) / 2);
                btnEdit.Location  = new Point(rx - 116, (card.Height - 32) / 2);
            };
            card.Resize += (s, e) => positionBtns();
            positionBtns();

            return card;
        }

        private Button MakeIconBtn(string icon, Color fg)
        {
            Button b = new Button
            {
                Text = icon,
                Width = 32, Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = fg,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (s, e) => b.BackColor = Color.FromArgb(243, 244, 246);
            b.MouseLeave += (s, e) => b.BackColor = Color.Transparent;
            return b;
        }

        private string GetSearchText() =>
            txtSearch.Text == "Qidirish" ? "" : txtSearch.Text.Trim();

        private void ResizeCards()
        {
            int y = 0;
            foreach (Control c in categoriesListPanel.Controls)
            {
                if (!(c is Panel)) continue;
                c.Width = Math.Max(100, categoriesListPanel.ClientSize.Width - 4);
                c.Top   = y;
                y      += c.Height + 8;
            }
        }

        private void SaveCategory(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Kategoriya nomini kiriting!", "Diqqat",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string printer = cmbPrinter.SelectedIndex <= 0 ? null : cmbPrinter.SelectedItem.ToString();
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                if (editingId == 0)
                {
                    SqlCommand cmd = new SqlCommand(
                        "INSERT INTO food_category(name,printer_name) VALUES(@n,@p)", db.GetCon());
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@p", (object)printer ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    SqlCommand cmd = new SqlCommand(
                        "UPDATE food_category SET name=@n, printer_name=@p WHERE id=@id", db.GetCon());
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@p", (object)printer ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", editingId);
                    cmd.ExecuteNonQuery();
                }
                db.CloseCon();
                editPanel.Visible = false;
                LoadCategories(GetSearchText());
                MessageBox.Show("Saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (Session.IsOnline && Session.TenantId > 0)
                    System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X,               r.Y,               radius * 2, radius * 2, 180, 90);
            p.AddArc(r.Right - radius * 2, r.Y,            radius * 2, radius * 2, 270, 90);
            p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            p.AddArc(r.X,               r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
