using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using WindowsFormsApp1;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.food
{
    public partial class AddFood : UserControl
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain   = Color.FromArgb(248, 248, 250);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted= Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);

        private static readonly string[] Units =
            { "dona", "portsiya", "kg", "gramm", "mg", "litr", "ml", "idish" };

        // List area
        private Panel           foodsScrollPanel;
        private FlowLayoutPanel catFlow;
        private TextBox         txtSearch;
        private Button          btnFab;

        // Edit panel
        private Panel      editPanel;
        private Label      lblFormTitle;
        private Button     btnSave;
        private Button     btnDeleteFood;
        private PictureBox picPreview;
        private Label      lblPhotoHint;
        private TextBox    txtName;
        private ComboBox   cmbCategory;
        private TextBox    txtOriginalPrice;
        private TextBox    txtSellingPrice;
        private TextBox    txtCount;
        private TextBox    txtDescription;
        private Panel      toggleUnlimited;
        private Button[]   unitBtns = new Button[Units.Length];

        // State
        private string selectedUnit      = "dona";
        private bool   isUnlimited       = false;
        private int    editingFoodId     = 0;
        private int    filterCategoryId  = 0;
        private string selectedPhotoFile = "";
        private string photoDir;

        public AddFood()
        {
            InitializeComponent();
            photoDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photo");
            Directory.CreateDirectory(photoDir);
            BuildUI();
            LoadCategoryFilter();
            this.Load += (s, e) => BeginInvoke((Action)LoadFoods);
        }

        // ── BUILD UI ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.BackColor = BgMain;

            // HEADER
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "Taomlar",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(24, 18)
            });

            Button btnReorderFoods = new Button
            {
                Text = "⇅  Tartib", Width = 100, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand,
                Visible = Session.IsAdmin
            };
            btnReorderFoods.FlatAppearance.BorderSize = 1;
            btnReorderFoods.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnReorderFoods.Click += (s, e) =>
            {
                string catLabel = "Barchasi";
                foreach (Control c in catFlow.Controls)
                    if (c is Button b && (int)b.Tag == filterCategoryId) { catLabel = b.Text; break; }

                string extraWhere = filterCategoryId > 0 ? $"food_category_id = {filterCategoryId}" : "";
                var items = ReorderForm.LoadItems("food", "name", extraWhere);
                if (items.Count == 0) { MessageBox.Show("Bu kategoriyada taom yo'q!"); return; }

                using (var frm = new ReorderForm($"Taomlar tartibi — {catLabel}", items))
                {
                    frm.ShowDialog();
                    if (frm.Saved) { ReorderForm.SaveOrder("food", frm.OrderedIds); LoadFoods(); }
                }
            };
            header.Controls.Add(btnReorderFoods);
            header.Resize += (s, e) => btnReorderFoods.Location = new Point(header.Width - 116, 15);

            // EDIT PANEL (right fly-out)
            editPanel = new Panel { Width = 440, Dock = DockStyle.Right, BackColor = CardBg, Visible = false };
            editPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, editPanel.Height);
            BuildEditPanel();

            // LIST WRAP
            Panel listWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };

            // Search bar
            Panel searchBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = CardBg };
            searchBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, searchBar.Height - 1, searchBar.Width, searchBar.Height - 1);

            TextBox localSearch = new TextBox
            {
                BorderStyle = BorderStyle.None, BackColor = BgMain,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 10),
                Text = "Taom qidirish...", Location = new Point(44, 16)
            };
            localSearch.GotFocus  += (s, e) => { if (localSearch.Text == "Taom qidirish...") { localSearch.Text = ""; localSearch.ForeColor = TextDark; } };
            localSearch.LostFocus += (s, e) => { if (string.IsNullOrEmpty(localSearch.Text)) { localSearch.Text = "Taom qidirish..."; localSearch.ForeColor = TextMuted; } };
            localSearch.TextChanged += (s, e) => LoadFoods();
            txtSearch = localSearch;
            searchBar.Controls.Add(localSearch);
            searchBar.Resize += (s, e) => localSearch.Width = searchBar.Width - 60;

            Label lblSearchIcon = new Label
            {
                Text = "🔍", Font = new Font("Segoe UI", 12),
                ForeColor = TextMuted, AutoSize = true, Location = new Point(16, 14)
            };
            searchBar.Controls.Add(lblSearchIcon);

            // Category filter bar
            Panel catBarWrap = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = CardBg };
            catBarWrap.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, catBarWrap.Height - 1, catBarWrap.Width, catBarWrap.Height - 1);
            catFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, AutoScroll = false,
                Padding = new Padding(12, 8, 12, 0)
            };
            catBarWrap.Controls.Add(catFlow);

            // Foods scroll
            foodsScrollPanel = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = BgMain, Padding = new Padding(20, 12, 20, 80)
            };
            foodsScrollPanel.Resize += (s, e) => ResizeFoodCards();

            // listWrap: Fill first, Top controls LAST ✓
            listWrap.Controls.Add(foodsScrollPanel);
            listWrap.Controls.Add(catBarWrap);
            listWrap.Controls.Add(searchBar);

            // FAB (added FIRST = index 0 = highest z-order = on top ✓)
            btnFab = new Button { Width = 52, Height = 52, BackColor = Success, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnFab.FlatAppearance.BorderSize = 0;
            btnFab.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(btnFab.ClientRectangle, 26))
                    e.Graphics.FillPath(new SolidBrush(Success), path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI", 24, FontStyle.Bold))
                    e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, btnFab.Width, btnFab.Height), sf);
            };
            btnFab.Click += (s, e) => ShowEditPanel(0);
            this.Controls.Add(btnFab);
            this.Resize += (s, e) => UpdateFabPosition();
            editPanel.VisibleChanged += (s, e) => UpdateFabPosition();

            // Main: Fill first, Right second, Top LAST ✓
            this.Controls.Add(listWrap);
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
            // Bottom button row
            Panel btnRow = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = CardBg };
            btnRow.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, btnRow.Width, 0);

            Button btnCancel = new Button
            {
                Text = "Yopish", Location = new Point(16, 14), Width = 150, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = CardBg, ForeColor = TextDark,
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = Border;
            btnCancel.Click += (s, e) => editPanel.Visible = false;
            btnRow.Controls.Add(btnCancel);

            btnSave = new Button
            {
                Height = 48, FlatStyle = FlatStyle.Flat, BackColor = Success,
                ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveFood;
            btnRow.Controls.Add(btnSave);
            btnRow.Resize += (s, e) =>
            {
                btnSave.Location = new Point(btnCancel.Right + 8, 14);
                btnSave.Width = btnRow.Width - btnCancel.Right - 24;
            };

            // Scrollable content
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = CardBg };

            // × close
            Button btnClose = new Button
            {
                Text = "×", Width = 30, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent, ForeColor = TextMuted,
                Font = new Font("Segoe UI", 14, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => editPanel.Visible = false;
            scroll.Controls.Add(btnClose);
            scroll.Resize += (s, e) => btnClose.Location = new Point(scroll.ClientSize.Width - 40, 14);

            int y = 16;

            lblFormTitle = new Label
            {
                Text = "Yangi taom", Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark, Location = new Point(24, y), AutoSize = true
            };
            scroll.Controls.Add(lblFormTitle);
            y += 52;

            // Photo picker
            scroll.Controls.Add(MakeSmallLabel("Mahsulot rasmi", new Point(24, y)));
            y += 26;

            Panel photoPicker = new Panel { Location = new Point(24, y), Height = 90, BackColor = BgMain, Cursor = Cursors.Hand };
            photoPicker.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(photoPicker.ClientRectangle, 10))
                    e.Graphics.DrawPath(new Pen(Border, 1.5f), path);
            };
            scroll.Controls.Add(photoPicker);
            scroll.Resize += (s, e) => photoPicker.Width = scroll.ClientSize.Width - 48;
            photoPicker.Width = 392;

            picPreview = new PictureBox
            {
                Width = 70, Height = 70, Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
            };
            photoPicker.Controls.Add(picPreview);

            lblPhotoHint = new Label
            {
                Text = "Rasmni tanlash uchun bosing\n(jpg, png, gif)",
                Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                Location = new Point(90, 24), AutoSize = true
            };
            photoPicker.Controls.Add(lblPhotoHint);

            photoPicker.Click += PickPhoto;
            picPreview.Click  += PickPhoto;
            lblPhotoHint.Click += PickPhoto;
            y += 102;

            // Mahsulot nomi
            y = AddFormField(scroll, "Mahsulot nomi", "Taom nomini kiriting", y, out txtName);

            // Kategoriya
            scroll.Controls.Add(MakeSmallLabel("Mahsulot kategoriyasi", new Point(24, y)));
            y += 26;
            cmbCategory = new ComboBox
            {
                Location = new Point(24, y), Height = 36,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10), FlatStyle = FlatStyle.Flat, BackColor = BgMain
            };
            scroll.Controls.Add(cmbCategory);
            scroll.Resize += (s, e) => cmbCategory.Width = scroll.ClientSize.Width - 48;
            cmbCategory.Width = 392;
            y += 46;

            // Narxlar — two side-by-side
            scroll.Controls.Add(MakeSmallLabel("Eski narxi (tan narxi)", new Point(24, y)));
            y += 26;
            Panel narxRow = new Panel { Location = new Point(24, y), Height = 52, BackColor = Color.Transparent };
            scroll.Controls.Add(narxRow);
            scroll.Resize += (s, e) => narxRow.Width = scroll.ClientSize.Width - 48;
            narxRow.Width = 392;

            Panel b1 = MakeInputBox("0", out txtOriginalPrice);
            Panel b2 = MakeInputBox("0", out txtSellingPrice);

            Label lblSell = new Label
            {
                Text = "Sotuv narxi", Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted, AutoSize = true
            };
            narxRow.Controls.Add(b2);
            narxRow.Controls.Add(b1);
            scroll.Controls.Add(lblSell); // in scroll not narxRow — avoids clip at Y=-18
            narxRow.Resize += (s, e) =>
            {
                int half = narxRow.Width / 2 - 4;
                b1.Width = half; b1.Location = new Point(0, 0);
                b2.Width = half; b2.Location = new Point(half + 8, 0);
                lblSell.Location = new Point(narxRow.Left + half + 8, narxRow.Top - 26);
            };
            b1.Width = b2.Width = 190;
            b1.Location = new Point(0, 0);
            b2.Location = new Point(198, 0);
            lblSell.Location = new Point(narxRow.Left + 198, narxRow.Top - 26);
            y += 62;

            // O'lchov birligi
            scroll.Controls.Add(MakeSmallLabel("O'lchov birligi", new Point(24, y)));
            y += 26;
            FlowLayoutPanel unitFlow = new FlowLayoutPanel
            {
                Location = new Point(24, y), Height = 76,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true, BackColor = Color.Transparent
            };
            scroll.Controls.Add(unitFlow);
            scroll.Resize += (s, e) => unitFlow.Width = scroll.ClientSize.Width - 48;
            unitFlow.Width = 392;

            for (int i = 0; i < Units.Length; i++)
            {
                int idx = i;
                Button ub = new Button
                {
                    Text = Units[i], Height = 30, Margin = new Padding(0, 0, 6, 6),
                    FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9),
                    BackColor = BgMain, ForeColor = TextDark, Cursor = Cursors.Hand
                };
                using (Graphics g = this.CreateGraphics())
                    ub.Width = (int)g.MeasureString(Units[i], ub.Font).Width + 20;
                ub.FlatAppearance.BorderSize = 1;
                ub.FlatAppearance.BorderColor = Border;
                ub.Click += (s, e) => { selectedUnit = Units[idx]; UpdateUnitBtns(); };
                unitBtns[i] = ub;
                unitFlow.Controls.Add(ub);
            }
            UpdateUnitBtns();
            y += 88;

            // Miqdori + Cheklanmagan toggle
            scroll.Controls.Add(MakeSmallLabel("Miqdori", new Point(24, y)));
            y += 26;
            Panel countRow = new Panel { Location = new Point(24, y), Height = 52, BackColor = Color.Transparent };
            scroll.Controls.Add(countRow);
            scroll.Resize += (s, e) => { countRow.Width = scroll.ClientSize.Width - 48; LayoutCountRow(countRow); };
            countRow.Width = 392;

            Panel countBox = MakeInputBox("0", out txtCount);
            countBox.Width = 170;
            countRow.Controls.Add(countBox);

            Panel togWrap = new Panel { Height = 52, BackColor = Color.Transparent };
            countRow.Controls.Add(togWrap);

            Label lblUnlim = new Label
            {
                Text = "Cheklanmagan rejim",
                Font = new Font("Segoe UI", 9), ForeColor = TextDark,
                Location = new Point(0, 18), AutoSize = true
            };
            togWrap.Controls.Add(lblUnlim);

            toggleUnlimited = BuildToggle();
            togWrap.Controls.Add(toggleUnlimited);

            togWrap.Resize += (s, e) =>
            {
                togWrap.Location = new Point(180, 0);
                togWrap.Width = countRow.Width - 180;
                toggleUnlimited.Location = new Point(togWrap.Width - 50, 14);
            };
            togWrap.Location = new Point(180, 0);
            togWrap.Width = 208;
            toggleUnlimited.Location = new Point(160, 14);
            y += 62;

            // Tavsif
            scroll.Controls.Add(MakeSmallLabel("Mahsulot tavsifi", new Point(24, y)));
            y += 26;
            Panel descBox = new Panel { Location = new Point(24, y), Height = 90, BackColor = BgMain };
            descBox.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(descBox.ClientRectangle, 8))
                    e.Graphics.DrawPath(new Pen(Border, 1.5f), path);
            };
            TextBox localDesc = new TextBox
            {
                Location = new Point(12, 10), Multiline = true, Height = 70,
                BorderStyle = BorderStyle.None, BackColor = BgMain,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 10),
                Text = "Taom haqida ma'lumot...", ScrollBars = ScrollBars.Vertical
            };
            localDesc.GotFocus  += (s, e) => { if (localDesc.Text.StartsWith("Taom haqida")) { localDesc.Text = ""; localDesc.ForeColor = TextDark; } };
            localDesc.LostFocus += (s, e) => { if (string.IsNullOrEmpty(localDesc.Text)) { localDesc.Text = "Taom haqida ma'lumot..."; localDesc.ForeColor = TextMuted; } };
            descBox.Resize += (s, e) => localDesc.Width = descBox.Width - 24;
            localDesc.Width = 368;
            descBox.Controls.Add(localDesc);
            txtDescription = localDesc;
            scroll.Controls.Add(descBox);
            scroll.Resize += (s, e) => descBox.Width = scroll.ClientSize.Width - 48;
            descBox.Width = 392;
            y += 102;

            // Delete button
            btnDeleteFood = new Button
            {
                Text = "Taomni o'chirish", Location = new Point(24, y), Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(254, 242, 242), ForeColor = Danger,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Visible = false
            };
            btnDeleteFood.FlatAppearance.BorderSize = 0;
            btnDeleteFood.Click += DeleteFood;
            scroll.Controls.Add(btnDeleteFood);
            scroll.Resize += (s, e) => btnDeleteFood.Width = scroll.ClientSize.Width - 48;
            btnDeleteFood.Width = 392;

            // editPanel: scroll (Fill) first, btnRow (Bottom) last ✓
            editPanel.Controls.Add(scroll);
            editPanel.Controls.Add(btnRow);
        }

        private void LayoutCountRow(Panel countRow)
        {
            if (txtCount?.Parent == null) return;
            Panel countBox = txtCount.Parent as Panel;
            if (countBox != null) countBox.Width = Math.Min(170, countRow.Width / 2);
        }

        private Panel BuildToggle()
        {
            Panel t = new Panel { Width = 44, Height = 24, BackColor = Color.Transparent, Cursor = Cursors.Hand };
            t.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color track = isUnlimited ? Success : Color.FromArgb(200, 200, 210);
                using (var path = RoundRect(new Rectangle(0, 0, 43, 23), 12))
                    e.Graphics.FillPath(new SolidBrush(track), path);
                int cx = isUnlimited ? 22 : 2;
                e.Graphics.FillEllipse(Brushes.White, cx, 2, 19, 19);
            };
            t.Click += (s, e) =>
            {
                isUnlimited = !isUnlimited;
                txtCount.Enabled = !isUnlimited;
                txtCount.Text = isUnlimited ? "∞" : "0";
                txtCount.ForeColor = TextMuted;
                t.Invalidate();
            };
            return t;
        }

        private void UpdateUnitBtns()
        {
            for (int i = 0; i < Units.Length; i++)
            {
                bool sel = Units[i] == selectedUnit;
                unitBtns[i].BackColor = sel ? GoldBg : BgMain;
                unitBtns[i].ForeColor = sel ? Gold : TextDark;
                unitBtns[i].FlatAppearance.BorderColor = sel ? Gold : Border;
            }
        }

        private void PickPhoto(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Rasm fayllari|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
                ofd.Title  = "Mahsulot rasmini tanlang";
                if (ofd.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string ext     = Path.GetExtension(ofd.FileName).ToLower();
                    string newName = Guid.NewGuid().ToString("N") + ext;
                    string dest    = Path.Combine(photoDir, newName);
                    File.Copy(ofd.FileName, dest, true);
                    selectedPhotoFile = newName;
                    using (Stream st = File.OpenRead(dest))
                        picPreview.Image = Image.FromStream(st);
                    lblPhotoHint.Text = Path.GetFileName(ofd.FileName);
                }
                catch (Exception ex) { MessageBox.Show("Rasm yuklashda xatolik: " + ex.Message); }
            }
        }

        // ── CATEGORY FILTER ───────────────────────────────────────────────────
        private sealed class CatItem
        {
            public int    Id   { get; }
            public string Name { get; }
            public CatItem(int id, string name) { Id = id; Name = name; }
            public override string ToString() => Name;
        }

        private void LoadCategoryFilter()
        {
            try
            {
                catFlow.Controls.Clear();
                catFlow.Controls.Add(MakeCatFilterBtn("Barchasi", 0));

                cmbCategory.Items.Clear();

                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    "SELECT id, name FROM food_category ORDER BY ISNULL(sort_order,9999), name", db.GetCon()))
                    da.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    MessageBox.Show("food_category jadvali bo'sh!\nAvval 'Kategoriyalar' bo'limidan kategoriya qo'shing.",
                        "Ma'lumot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                foreach (DataRow row in dt.Rows)
                {
                    int    id   = Convert.ToInt32(row["id"]);
                    string name = row["name"].ToString();
                    catFlow.Controls.Add(MakeCatFilterBtn(name, id));
                    cmbCategory.Items.Add(new CatItem(id, name));
                }

                if (cmbCategory.Items.Count > 0)
                    cmbCategory.SelectedIndex = 0;

                UpdateCatFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kategoriyalarni yuklashda xatolik: " + ex.Message);
            }
        }

        private Button MakeCatFilterBtn(string text, int catId)
        {
            var font = new Font("Segoe UI", 9);
            Button b = new Button
            {
                Tag = catId, Text = text, Height = 28,
                Width = TextRenderer.MeasureText(text, font).Width + 24,
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.Flat, Font = font, Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.Click += (s, e) => { filterCategoryId = catId; UpdateCatFilter(); LoadFoods(); };
            return b;
        }

        private void UpdateCatFilter()
        {
            foreach (Control c in catFlow.Controls)
            {
                if (!(c is Button b)) continue;
                bool sel = (int)b.Tag == filterCategoryId;
                b.BackColor = sel ? GoldBg : CardBg;
                b.ForeColor = sel ? Gold : TextDark;
                b.FlatAppearance.BorderColor = sel ? Gold : Border;
            }
        }

        // ── LOAD FOODS ────────────────────────────────────────────────────────
        private void LoadFoods()
        {
            foodsScrollPanel.SuspendLayout();
            foodsScrollPanel.Controls.Clear();
            try
            {
                string search = (txtSearch != null && txtSearch.Text != "Taom qidirish...")
                    ? txtSearch.Text.Trim() : "";

                string sql = @"
                    SELECT f.id, f.name, f.selling_price, f.original_price,
                           f.count, f.photo, f.unit, f.is_unlimited,
                           fc.name AS cat_name, f.food_category_id
                    FROM food f
                    JOIN food_category fc ON fc.id = f.food_category_id
                    WHERE (@cat = 0 OR f.food_category_id = @cat)
                      AND (@q = '' OR f.name LIKE '%' + @q + '%')
                    ORDER BY ISNULL(fc.sort_order,9999), fc.name, ISNULL(f.sort_order,9999), f.name";

                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(sql, db.GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@cat", filterCategoryId);
                    da.SelectCommand.Parameters.AddWithValue("@q",   search);
                    da.Fill(dt);
                }

                int y = 0;
                string lastCat = "";
                foreach (DataRow row in dt.Rows)
                {
                    string catName = row["cat_name"].ToString();
                    if (catName != lastCat)
                    {
                        lastCat = catName;
                        Label lbl = new Label
                        {
                            Text = catName,
                            Font = new Font("Segoe UI", 9, FontStyle.Bold),
                            ForeColor = TextMuted,
                            Location = new Point(0, y), Height = 22, AutoSize = false
                        };
                        foodsScrollPanel.Controls.Add(lbl);
                        lbl.Width = Math.Max(100, foodsScrollPanel.ClientSize.Width - 40);
                        y += 26;
                    }
                    Panel card = CreateFoodCard(row);
                    card.Location = new Point(0, y);
                    card.Width = Math.Max(100, foodsScrollPanel.ClientSize.Width - 40);
                    foodsScrollPanel.Controls.Add(card);
                    y += card.Height + 8;
                }

                if (dt.Rows.Count == 0)
                    foodsScrollPanel.Controls.Add(new Label
                    {
                        Text = "Hali taom qo'shilmagan. (+) tugmasini bosing.",
                        Font = new Font("Segoe UI", 12), ForeColor = TextMuted,
                        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                    });
            }
            catch (Exception ex) { MessageBox.Show("Taomlarni yuklashda xatolik: " + ex.Message); }
            finally { foodsScrollPanel.ResumeLayout(); }
        }

        private Panel CreateFoodCard(DataRow row)
        {
            int     id       = Convert.ToInt32(row["id"]);
            string  name     = row["name"].ToString();
            decimal sellPrice= Convert.ToDecimal(row["selling_price"]);
            string  unit     = row["unit"]      == DBNull.Value ? "dona" : row["unit"].ToString();
            int     count    = row["count"]     == DBNull.Value ? 0 : Convert.ToInt32(row["count"]);
            bool    unlim    = row["is_unlimited"] != DBNull.Value && Convert.ToBoolean(row["is_unlimited"]);
            string  photo    = row["photo"].ToString();

            Panel card = new Panel { Height = 68, BackColor = CardBg, Cursor = Cursors.Hand };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border), path);
                }
            };

            // Thumbnail
            Panel thumb = new Panel { Width = 48, Height = 48, Location = new Point(10, 10), BackColor = Color.FromArgb(240, 240, 245) };
            thumb.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(thumb.ClientRectangle, 8))
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(240, 240, 245)), path);
            };
            string photoPath = Path.Combine(photoDir, photo);
            if (!string.IsNullOrEmpty(photo) && File.Exists(photoPath))
            {
                try
                {
                    PictureBox pb = new PictureBox
                    {
                        Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent
                    };
                    using (Stream st = File.OpenRead(photoPath))
                        pb.Image = Image.FromStream(st);
                    thumb.Controls.Add(pb);
                }
                catch { }
            }
            card.Controls.Add(thumb);

            card.Controls.Add(new Label
            {
                Text = name, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, Location = new Point(68, 8), AutoSize = true
            });
            card.Controls.Add(new Label
            {
                Text = $"{(unlim ? "∞" : count.ToString())} {unit}",
                Font = new Font("Segoe UI", 8), ForeColor = TextMuted,
                Location = new Point(68, 32), AutoSize = true
            });

            Label lblPrice = new Label
            {
                Text = sellPrice.ToString("N0") + " so'm",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            card.Controls.Add(lblPrice);

            Button btnEdit = new Button
            {
                Text = "✏", Width = 30, Height = 30,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 11), Cursor = Cursors.Hand
            };
            btnEdit.FlatAppearance.BorderSize = 0;
            btnEdit.Click += (s, e) => ShowEditPanel(id);
            card.Controls.Add(btnEdit);

            card.Resize += (s, e) =>
            {
                lblPrice.Location = new Point(card.Width - lblPrice.Width - 50, 24);
                btnEdit.Location  = new Point(card.Width - 40, 19);
            };

            EventHandler onClick = (s, e) => ShowEditPanel(id);
            card.Click  += onClick;
            thumb.Click += onClick;

            Color normalBg = CardBg, hoverBg = Color.FromArgb(250, 250, 252);
            card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
            card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };

            return card;
        }

        // ── SHOW EDIT PANEL ───────────────────────────────────────────────────
        private void ShowEditPanel(int foodId)
        {
            editingFoodId = foodId;
            bool isEdit = foodId > 0;
            lblFormTitle.Text    = isEdit ? "Taomni tahrirlash" : "Yangi taom";
            btnSave.Text         = isEdit ? "Saqlash" : "Qo'shish";
            btnDeleteFood.Visible = isEdit;

            // Reset
            selectedPhotoFile = "";
            picPreview.Image  = null;
            lblPhotoHint.Text = "Rasmni tanlash uchun bosing\n(jpg, png, gif)";
            SetField(txtName,          "Taom nomini kiriting", "");
            SetField(txtOriginalPrice, "0", "");
            SetField(txtSellingPrice,  "0", "");
            SetField(txtCount,         "0", "");
            txtCount.Enabled = true;
            isUnlimited = false;
            toggleUnlimited.Invalidate();
            selectedUnit = "dona";
            UpdateUnitBtns();
            txtDescription.Text     = "Taom haqida ma'lumot...";
            txtDescription.ForeColor = TextMuted;

            if (isEdit)
            {
                try
                {
                    dbconnect db = new dbconnect();
                    DataTable dt = new DataTable();
                    using (SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM food WHERE id=@id", db.GetCon()))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@id", foodId);
                        da.Fill(dt);
                    }
                    if (dt.Rows.Count == 0) return;
                    DataRow r = dt.Rows[0];

                    SetField(txtName,          "Taom nomini kiriting", r["name"].ToString());
                    SetField(txtOriginalPrice, "0", r["original_price"].ToString());
                    SetField(txtSellingPrice,  "0", r["selling_price"].ToString());

                    if (r["unit"] != DBNull.Value) selectedUnit = r["unit"].ToString();
                    UpdateUnitBtns();

                    bool unlim = r["is_unlimited"] != DBNull.Value && Convert.ToBoolean(r["is_unlimited"]);
                    isUnlimited = unlim;
                    toggleUnlimited.Invalidate();
                    if (unlim) { txtCount.Text = "∞"; txtCount.ForeColor = TextMuted; txtCount.Enabled = false; }
                    else SetField(txtCount, "0", r["count"].ToString());

                    string desc = r["description"] == DBNull.Value ? "" : r["description"].ToString();
                    if (!string.IsNullOrEmpty(desc)) { txtDescription.Text = desc; txtDescription.ForeColor = TextDark; }

                    string photo = r["photo"].ToString();
                    if (!string.IsNullOrEmpty(photo))
                    {
                        string pp = Path.Combine(photoDir, photo);
                        if (File.Exists(pp))
                        {
                            selectedPhotoFile = photo;
                            try { using (Stream st = File.OpenRead(pp)) picPreview.Image = Image.FromStream(st); lblPhotoHint.Text = photo; }
                            catch { }
                        }
                    }

                    if (cmbCategory.Items.Count > 0 && r["food_category_id"] != DBNull.Value)
                    {
                        int catId = Convert.ToInt32(r["food_category_id"]);
                        for (int i = 0; i < cmbCategory.Items.Count; i++)
                        {
                            if (((CatItem)cmbCategory.Items[i]).Id == catId)
                            { cmbCategory.SelectedIndex = i; break; }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            }

            editPanel.Visible = true;
            txtName.Focus();
        }

        private void SetField(TextBox tb, string placeholder, string value)
        {
            if (string.IsNullOrEmpty(value)) { tb.Text = placeholder; tb.ForeColor = TextMuted; }
            else                             { tb.Text = value;       tb.ForeColor = TextDark;  }
        }

        // ── SAVE / DELETE ─────────────────────────────────────────────────────
        private void SaveFood(object sender, EventArgs e)
        {
            string name    = GetVal(txtName,          "Taom nomini kiriting");
            string origStr = GetVal(txtOriginalPrice, "0");
            string sellStr = GetVal(txtSellingPrice,  "0");
            string desc    = GetVal(txtDescription,   "Taom haqida ma'lumot...");

            if (string.IsNullOrEmpty(name))                           { MessageBox.Show("Taom nomini kiriting!");          return; }
            if (!decimal.TryParse(origStr, out decimal orig))         { MessageBox.Show("Tan narxini to'g'ri kiriting!");  return; }
            if (!decimal.TryParse(sellStr, out decimal sell) || sell <= 0) { MessageBox.Show("Sotuv narxini kiriting!"); return; }
            if (cmbCategory.SelectedItem == null)                    { MessageBox.Show("Kategoriya tanlang!");            return; }

            int cnt = 0;
            if (!isUnlimited)
            {
                string countStr = GetVal(txtCount, "0");
                if (!int.TryParse(countStr, out cnt) || cnt < 0) { MessageBox.Show("Miqdorni to'g'ri kiriting!"); return; }
            }

            int catId = ((CatItem)cmbCategory.SelectedItem).Id;
            object photoParam = string.IsNullOrEmpty(selectedPhotoFile) ? (object)DBNull.Value : selectedPhotoFile;
            object descParam  = string.IsNullOrEmpty(desc)              ? (object)DBNull.Value : desc;

            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                if (editingFoodId == 0)
                {
                    using (SqlCommand cmd = new SqlCommand(
                        @"INSERT INTO food(food_category_id,name,count,original_price,selling_price,
                          photo,unit,description,is_unlimited)
                          VALUES(@c,@n,@cnt,@op,@sp,@ph,@u,@d,@iu)", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@c",   catId);
                        cmd.Parameters.AddWithValue("@n",   name);
                        cmd.Parameters.AddWithValue("@cnt", cnt);
                        cmd.Parameters.AddWithValue("@op",  orig);
                        cmd.Parameters.AddWithValue("@sp",  sell);
                        cmd.Parameters.AddWithValue("@ph",  photoParam);
                        cmd.Parameters.AddWithValue("@u",   selectedUnit);
                        cmd.Parameters.AddWithValue("@d",   descParam);
                        cmd.Parameters.AddWithValue("@iu",  isUnlimited ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SqlCommand cmd = new SqlCommand(
                        @"UPDATE food SET food_category_id=@c,name=@n,count=@cnt,
                          original_price=@op,selling_price=@sp,photo=@ph,
                          unit=@u,description=@d,is_unlimited=@iu WHERE id=@id", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@c",   catId);
                        cmd.Parameters.AddWithValue("@n",   name);
                        cmd.Parameters.AddWithValue("@cnt", cnt);
                        cmd.Parameters.AddWithValue("@op",  orig);
                        cmd.Parameters.AddWithValue("@sp",  sell);
                        cmd.Parameters.AddWithValue("@ph",  photoParam);
                        cmd.Parameters.AddWithValue("@u",   selectedUnit);
                        cmd.Parameters.AddWithValue("@d",   descParam);
                        cmd.Parameters.AddWithValue("@iu",  isUnlimited ? 1 : 0);
                        cmd.Parameters.AddWithValue("@id",  editingFoodId);
                        cmd.ExecuteNonQuery();
                    }
                }
                db.CloseCon();
                editPanel.Visible = false;
                LoadFoods();
                if (Session.IsOnline && Session.TenantId > 0)
                    System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void DeleteFood(object sender, EventArgs e)
        {
            if (MessageBox.Show("Bu taomni o'chirasizmi?", "O'chirish",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                // Check if food is referenced in any order
                int refs = 0;
                using (SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM order_food WHERE food_id=@id", db.GetCon()))
                { chk.Parameters.AddWithValue("@id", editingFoodId); refs = (int)chk.ExecuteScalar(); }
                if (refs > 0)
                {
                    db.CloseCon();
                    MessageBox.Show($"Bu taom {refs} ta buyurtmada ishlatilgan. O'chirish mumkin emas.", "Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                using (SqlCommand cmd = new SqlCommand("DELETE FROM food WHERE id=@id", db.GetCon()))
                { cmd.Parameters.AddWithValue("@id", editingFoodId); cmd.ExecuteNonQuery(); }
                db.CloseCon();
                editPanel.Visible = false;
                LoadFoods();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private void ResizeFoodCards()
        {
            int y = 0;
            foreach (Control c in foodsScrollPanel.Controls)
            {
                c.Width = Math.Max(100, foodsScrollPanel.ClientSize.Width - 40);
                c.Top   = y;
                y      += c.Height + (c is Panel ? 8 : 4);
            }
        }

        private int AddFormField(Panel parent, string label, string placeholder, int y, out TextBox tb)
        {
            parent.Controls.Add(MakeSmallLabel(label, new Point(24, y)));
            y += 26;
            Panel box = MakeInputBox(placeholder, out tb);
            box.Location = new Point(24, y);
            parent.Controls.Add(box);
            parent.Resize += (s, e) => box.Width = parent.ClientSize.Width - 48;
            box.Width = 392;
            return y + 62;
        }

        private Panel MakeInputBox(string placeholder, out TextBox tb)
        {
            Panel box = new Panel { Height = 52, BackColor = BgMain };
            box.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(box.ClientRectangle, 8))
                    e.Graphics.DrawPath(new Pen(Border, 1.5f), path);
            };
            TextBox local = new TextBox
            {
                Location = new Point(14, 14), BorderStyle = BorderStyle.None,
                BackColor = BgMain, ForeColor = TextMuted,
                Font = new Font("Segoe UI", 10), Text = placeholder
            };
            local.GotFocus  += (s, e) => { if (local.Text == placeholder) { local.Text = ""; local.ForeColor = TextDark; } box.Invalidate(); };
            local.LostFocus += (s, e) => { if (string.IsNullOrEmpty(local.Text)) { local.Text = placeholder; local.ForeColor = TextMuted; } box.Invalidate(); };
            box.Resize += (s, e) => local.Width = box.Width - 28;
            local.Width = 364;
            box.Controls.Add(local);
            tb = local;
            return box;
        }

        private Label MakeSmallLabel(string text, Point loc)
        {
            return new Label
            {
                Text = text, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted, Location = loc, AutoSize = true
            };
        }

        private string GetVal(TextBox tb, string placeholder)
        {
            string v = tb.Text.Trim();
            return v == placeholder ? "" : v;
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X,                  r.Y,                   radius * 2, radius * 2, 180, 90);
            p.AddArc(r.Right - radius * 2, r.Y,                   radius * 2, radius * 2, 270, 90);
            p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2,   0, 90);
            p.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2,  90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
