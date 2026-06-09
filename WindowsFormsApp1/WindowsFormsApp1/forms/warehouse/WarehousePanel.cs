using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.warehouse
{
    public class WarehousePanel : UserControl
    {
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard   = Color.White;
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color Warning  = Color.FromArgb(234, 88, 12);
        private static readonly Color Blue     = Color.FromArgb(59, 130, 246);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted= Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);

        private Panel   _content;
        private Button[] _tabBtns;
        private int     _activeTab = -1;

        public WarehousePanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = BgPage;
            EnsureTables();
            BuildUI();
        }

        // ── DB MIGRATION ─────────────────────────────────────────────────────
        private void EnsureTables()
        {
            try
            {
                var db = new dbconnect();
                db.OpenCon();
                Exec(db, @"IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ingredient')
                    CREATE TABLE ingredient(id INT IDENTITY PRIMARY KEY,name NVARCHAR(100) NOT NULL,
                    unit NVARCHAR(20) NOT NULL DEFAULT 'kg',quantity DECIMAL(10,3) DEFAULT 0,
                    price_per_unit DECIMAL(18,2) DEFAULT 0,min_quantity DECIMAL(10,3) DEFAULT 0)");

                Exec(db, @"IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='recipe_ingredient')
                    CREATE TABLE recipe_ingredient(id INT IDENTITY PRIMARY KEY,food_id INT NOT NULL,
                    ingredient_id INT NOT NULL,quantity_per_portion DECIMAL(10,4) NOT NULL)");

                Exec(db, @"IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ingredient_purchase')
                    CREATE TABLE ingredient_purchase(id INT IDENTITY PRIMARY KEY,ingredient_id INT NOT NULL,
                    quantity DECIMAL(10,3) NOT NULL,price_per_unit DECIMAL(18,2) NOT NULL,
                    total_price DECIMAL(18,2) NOT NULL,purchased_at DATETIME DEFAULT GETDATE(),
                    notes NVARCHAR(200) NULL)");
                Exec(db, @"IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='food_purchase')
                    CREATE TABLE food_purchase(id INT IDENTITY PRIMARY KEY,food_id INT NOT NULL,
                    quantity INT NOT NULL DEFAULT 1,price_per_unit DECIMAL(18,2) NOT NULL DEFAULT 0,
                    total_price DECIMAL(18,2) NOT NULL DEFAULT 0,purchased_at DATETIME DEFAULT GETDATE(),
                    notes NVARCHAR(200) NULL)");
                db.CloseCon();
            }
            catch { }
        }

        private static void Exec(dbconnect db, string sql)
        {
            using (var c = new SqlCommand(sql, db.GetCon())) c.ExecuteNonQuery();
        }

        // ── SHELL ─────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            Panel tabBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgCard };
            tabBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);

            string[] labels = { "Masalliqlar", "Retseptlar", "Masalliq xaridi", "Mahsulot xaridi" };
            int[] widths     = { 118, 108, 138, 142 };
            _tabBtns = new Button[4];
            int tx = 20;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                Button b = new Button
                {
                    Text = labels[i], Location = new Point(tx, 12),
                    Width = widths[i], Height = 32,
                    FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Border;
                b.Click += (s, e) => SwitchTab(idx);
                _tabBtns[i] = b;
                tabBar.Controls.Add(b);
                tx += widths[i] + 8;
            }
            this.Controls.Add(tabBar);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            this.Controls.Add(_content);
            _content.BringToFront();

            this.Load += (s, e) => SwitchTab(0);
        }

        private void SwitchTab(int tab)
        {
            if (_activeTab == tab) return;
            for (int i = 0; i < _tabBtns.Length; i++)
            {
                bool a = i == tab;
                _tabBtns[i].BackColor = a ? Gold : BgCard;
                _tabBtns[i].ForeColor = a ? Color.White : TextDark;
                _tabBtns[i].Font = new Font("Segoe UI", 10, a ? FontStyle.Bold : FontStyle.Regular);
                _tabBtns[i].FlatAppearance.BorderColor = a ? Gold : Border;
            }
            _activeTab = tab;
            _content.Controls.Clear();
            UserControl uc = null;
            switch (tab)
            {
                case 0: uc = BuildMasalliqlarTab();     break;
                case 1: uc = BuildRetseptlarTab();      break;
                case 2: uc = BuildXaridlarTab();        break;
                case 3: uc = BuildMahsulotXaridiTab();  break;
            }
            if (uc != null) { uc.Dock = DockStyle.Fill; _content.Controls.Add(uc); }
        }

        // ── MASALLIQLAR ───────────────────────────────────────────────────────
        private UserControl BuildMasalliqlarTab()
        {
            var uc = new UserControl { BackColor = BgPage };

            // Header
            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            hdr.Controls.Add(new Label { Text = "Masalliqlar", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16) });
            Button btnAdd = MakeBtn("+ Qo'shish", Success, 120);
            btnAdd.FlatAppearance.BorderSize = 0;
            hdr.Controls.Add(btnAdd);
            hdr.Resize += (s, e) => btnAdd.Location = new Point(hdr.Width - 140, 12);

            // Stats
            Panel stats = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = BgPage };
            Label lblCnt = null, lblVal = null, lblLow = null;
            Panel cCnt = StatCard("Jami masalliqlar", "0",       Gold,    out lblCnt);
            Panel cVal = StatCard("Ombor qiymati",    "0 so'm",  Success, out lblVal);
            Panel cLow = StatCard("⚠ Kam qolganlar",  "0 ta",    Danger,  out lblLow);
            stats.Controls.AddRange(new Control[] { cCnt, cVal, cLow });
            stats.Resize += (s, e) =>
            {
                int w = Math.Max((stats.Width - 56) / 3, 120);
                cCnt.SetBounds(16,      12, w, 60);
                cVal.SetBounds(24 + w,  12, w, 60);
                cLow.SetBounds(32 + w*2,12, w, 60);
            };

            // Search
            Panel srch = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            srch.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0, srch.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, srch.Height - 1, srch.Width, srch.Height - 1);
            };
            TextBox txtSrch = new TextBox { Location = new Point(16, 12), Width = 260, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle, BackColor = BgPage };
            SetHint(txtSrch, "Qidirish...");
            srch.Controls.Add(txtSrch);

            // Col header
            Panel colHdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(243, 244, 246) };
            colHdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, colHdr.Height - 1, colHdr.Width, colHdr.Height - 1);

            // Rows
            Panel rows = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };

            uc.Controls.Add(rows);
            uc.Controls.Add(colHdr);
            uc.Controls.Add(srch);
            uc.Controls.Add(stats);
            uc.Controls.Add(hdr);

            float[] pcts = { .28f, .14f, .17f, .17f, .13f, .11f };
            string[] cols = { "Nomi", "Miqdor", "Narx/birlik", "Jami qiymat", "Holat", "Amallar" };

            // Sarlavhalarni qayta quramiz — panel o'lchami o'zgarganda
            colHdr.Resize += (s, e) => { if (colHdr.Width > 0) BuildColHdr(colHdr, cols, pcts); };

            Action load = null;
            load = () =>
            {
                if (colHdr.Width > 0) BuildColHdr(colHdr, cols, pcts);
                rows.SuspendLayout();
                rows.Controls.Clear();
                string q = txtSrch.Text.Trim();
                try
                {
                    var db = new dbconnect();
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT id,name,unit,quantity,price_per_unit,min_quantity,quantity*price_per_unit AS tv FROM ingredient WHERE (@s='' OR name LIKE '%'+@s+'%') ORDER BY name",
                        db.GetCon()))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@s", q);
                        da.Fill(dt);
                    }

                    decimal totalVal = 0; int lowCnt = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        totalVal += Convert.ToDecimal(r["tv"]);
                        if (Convert.ToDecimal(r["quantity"]) <= Convert.ToDecimal(r["min_quantity"]) && Convert.ToDecimal(r["min_quantity"]) > 0) lowCnt++;
                    }
                    if (lblCnt != null) lblCnt.Text = dt.Rows.Count.ToString();
                    if (lblVal != null) lblVal.Text  = totalVal.ToString("N0") + " so'm";
                    if (lblLow != null) lblLow.Text  = lowCnt + " ta";

                    BuildColHdr(colHdr, cols, pcts);

                    int y = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        int     id   = Convert.ToInt32(r["id"]);
                        string  nm   = r["name"].ToString();
                        string  un   = r["unit"].ToString();
                        decimal qty  = Convert.ToDecimal(r["quantity"]);
                        decimal ppu  = Convert.ToDecimal(r["price_per_unit"]);
                        decimal tv   = Convert.ToDecimal(r["tv"]);
                        decimal minQ = Convert.ToDecimal(r["min_quantity"]);
                        bool isEmpty = qty <= 0;
                        bool isLow   = !isEmpty && minQ > 0 && qty <= minQ;

                        string _nm = nm; string _un = un; decimal _qty = qty, _ppu = ppu, _tv = tv;
                        bool _empty = isEmpty, _low = isLow; int _id = id;

                        Panel row = new Panel { BackColor = y % 2 == 0 ? BgCard : Color.FromArgb(250, 250, 252), Height = 52 };
                        row.Paint += (s2, e2) => e2.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);

                        Action<int> build = (w) =>
                        {
                            row.Controls.Clear();

                            // Avatar
                            Panel av = new Panel { Width = 34, Height = 34, Location = new Point(16, 9) };
                            string ini = _nm.Length > 0 ? _nm[0].ToString().ToUpper() : "?";
                            av.Paint += (s3, e3) =>
                            {
                                e3.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                                e3.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(240, 244, 255)), 0, 0, 33, 33);
                                using (var f = new Font("Segoe UI", 12, FontStyle.Bold))
                                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                    e3.Graphics.DrawString(ini, f, new SolidBrush(Gold), new RectangleF(0, 0, 33, 33), sf);
                            };
                            row.Controls.Add(av);

                            // Name
                            int x0 = 58; int nw = Gw(w, pcts, 0) - 42;
                            row.Controls.Add(RLbl(_nm, x0, nw, TextDark, FontStyle.Bold, row.Height));

                            // Qty
                            Color qc = _empty ? Danger : (_low ? Warning : TextDark);
                            FontStyle qfs = _empty || _low ? FontStyle.Bold : FontStyle.Regular;
                            row.Controls.Add(RLbl(Fn(_qty) + " " + _un, Gx(w, pcts, 1), Gw(w, pcts, 1), qc, qfs, row.Height));

                            // Price/unit
                            row.Controls.Add(RLbl(_ppu.ToString("N0") + " so'm", Gx(w, pcts, 2), Gw(w, pcts, 2), TextMuted, FontStyle.Regular, row.Height));

                            // Total value
                            row.Controls.Add(RLbl(_tv.ToString("N0") + " so'm", Gx(w, pcts, 3), Gw(w, pcts, 3), Gold, FontStyle.Bold, row.Height));

                            // Status badge
                            Color sc = _empty ? Danger : (_low ? Warning : Success);
                            string st = _empty ? "Tugagan" : (_low ? "⚠ Kam" : "✓ Normal");
                            Panel badge = new Panel { Width = 72, Height = 22, Location = new Point(Gx(w, pcts, 4), (row.Height - 22) / 2) };
                            badge.Paint += (s3, e3) =>
                            {
                                e3.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                                using (var p = RR(badge.ClientRectangle, 4))
                                    e3.Graphics.FillPath(new SolidBrush(Color.FromArgb(28, sc.R, sc.G, sc.B)), p);
                                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                    e3.Graphics.DrawString(st, new Font("Segoe UI", 8, FontStyle.Bold), new SolidBrush(sc), badge.ClientRectangle, sf);
                            };
                            row.Controls.Add(badge);

                            // Edit
                            int ax = Gx(w, pcts, 5);
                            Button be = IBtn("✏", Blue); be.Location = new Point(ax, (row.Height - 28) / 2);
                            be.Click += (s3, e3) => { using (var f = new IngredientForm(_id)) if (f.ShowDialog() == DialogResult.OK) load(); };
                            row.Controls.Add(be);

                            // Quick buy
                            Button bb = IBtn("🛒", Gold); bb.Location = new Point(ax + 32, (row.Height - 28) / 2);
                            bb.Click += (s3, e3) => { using (var f = new PurchaseForm(_id)) if (f.ShowDialog() == DialogResult.OK) load(); };
                            row.Controls.Add(bb);

                            // Delete
                            Button bd = IBtn("✕", Danger); bd.Location = new Point(ax + 64, (row.Height - 28) / 2);
                            bd.Click += (s3, e3) =>
                            {
                                if (MessageBox.Show($"'{_nm}'ni o'chirishni tasdiqlaysizmi?", "O'chirish", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                                try
                                {
                                    var db2 = new dbconnect(); db2.OpenCon();
                                    // central_id ni oldindan olamiz — SyncQueue uchun
                                    int? centralId = null;
                                    using (var chk = new SqlCommand("SELECT central_id FROM ingredient WHERE id=@id", db2.GetCon()))
                                    {
                                        chk.Parameters.AddWithValue("@id", _id);
                                        object v = chk.ExecuteScalar();
                                        if (v != null && v != DBNull.Value) centralId = Convert.ToInt32(v);
                                    }
                                    using (var cmd = new SqlCommand("DELETE FROM ingredient WHERE id=@id", db2.GetCon()))
                                    { cmd.Parameters.AddWithValue("@id", _id); cmd.ExecuteNonQuery(); }
                                    db2.CloseCon();
                                    // Central DB dan ham o'chirish uchun SyncQueue ga yozamiz
                                    if (centralId.HasValue)
                                        SyncQueueHelper.Add(SyncQueueHelper.IngredientDelete, centralId.Value, "Delete");
                                    load();
                                }
                                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                            };
                            row.Controls.Add(bd);
                        };

                        row.SetBounds(0, y, Math.Max(rows.ClientSize.Width, 400), 52);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        rows.Controls.Add(row);
                        rows.Resize += (s2, e2) => { if (rows.ClientSize.Width > 0) { row.Width = rows.ClientSize.Width; build(row.Width); } };
                        build(row.Width > 0 ? row.Width : 800);
                        y += 52;
                    }

                    if (dt.Rows.Count == 0)
                        rows.Controls.Add(new Label { Text = "Masalliq topilmadi. '+ Qo'shish' tugmasi orqali qo'shing.", Font = new Font("Segoe UI", 11), ForeColor = TextMuted, AutoSize = true, Location = new Point(32, 32) });
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { rows.ResumeLayout(); }
            };

            btnAdd.Click   += (s, e) => { using (var f = new IngredientForm(0)) if (f.ShowDialog() == DialogResult.OK) load(); };
            txtSrch.TextChanged += (s, e) => load();
            uc.Load += (s, e) => BeginInvoke((Action)load);
            return uc;
        }

        // ── RETSEPTLAR ────────────────────────────────────────────────────────
        private UserControl BuildRetseptlarTab()
        {
            var uc = new UserControl { BackColor = BgPage };

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            hdr.Controls.Add(new Label { Text = "Retseptlar", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16) });
            hdr.Controls.Add(new Label { Text = "Taom ustiga bosib, 1 porsiyaga kerak masalliqlarni belgilang", Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 40) });

            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true, Padding = new Padding(16) };
            FlowLayoutPanel flp = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, BackColor = BgPage };
            scroll.Controls.Add(flp);

            uc.Controls.Add(scroll);
            uc.Controls.Add(hdr);

            Action loadFoods = null;
            loadFoods = () =>
            {
                flp.SuspendLayout();
                flp.Controls.Clear();
                try
                {
                    var db = new dbconnect();
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT f.id,f.name,(SELECT COUNT(*) FROM recipe_ingredient r WHERE r.food_id=f.id) AS rc FROM food f ORDER BY f.name",
                        db.GetCon())) da.Fill(dt);

                    foreach (DataRow r in dt.Rows)
                    {
                        int fid = Convert.ToInt32(r["id"]);
                        string fname = r["name"].ToString();
                        int rc = Convert.ToInt32(r["rc"]);
                        string _fn = fname; int _fid = fid; int _rc = rc;

                        Panel card = new Panel { Width = 210, Height = 130, Margin = new Padding(8), BackColor = BgCard, Cursor = Cursors.Hand };
                        card.Paint += (s2, e2) =>
                        {
                            e2.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            using (var p = RR(card.ClientRectangle, 10))
                            { e2.Graphics.FillPath(new SolidBrush(BgCard), p); e2.Graphics.DrawPath(new Pen(Border), p); }
                            e2.Graphics.FillRectangle(new SolidBrush(_rc > 0 ? Success : Gold), 0, 0, card.Width, 4);
                        };
                        card.Controls.Add(new Label { Text = _fn, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextDark, Width = 190, Height = 22, Location = new Point(10, 16), AutoSize = false });
                        card.Controls.Add(new Label { Text = _rc > 0 ? $"{_rc} ta masalliq belgilangan" : "Retsept yo'q", Font = new Font("Segoe UI", 8), ForeColor = _rc > 0 ? Success : TextMuted, AutoSize = true, Location = new Point(10, 44) });

                        Button btn = MakeBtn(_rc > 0 ? "Retseptni tahrirlash" : "+ Retsept qo'shish", _rc > 0 ? Success : Gold, 190);
                        btn.Location = new Point(10, 88);
                        EventHandler open = (s2, e2) => ShowRecipeOverlay(uc, _fid, _fn, loadFoods);
                        card.Click += open; btn.Click += open;
                        card.MouseEnter += (s2, e2) => { card.BackColor = Color.FromArgb(250, 250, 252); card.Refresh(); };
                        card.MouseLeave += (s2, e2) => { card.BackColor = BgCard; card.Refresh(); };
                        card.Controls.Add(btn);
                        flp.Controls.Add(card);
                    }
                    if (dt.Rows.Count == 0)
                        flp.Controls.Add(new Label { Text = "Taomlar yo'q. 'Taomlar' bo'limiga o'ting.", Font = new Font("Segoe UI", 11), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(20) });
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { flp.ResumeLayout(); }
            };

            uc.Load += (s, e) => loadFoods();
            return uc;
        }

        private void ShowRecipeOverlay(UserControl parent, int foodId, string foodName, Action refresh)
        {
            Panel ov = new Panel { BackColor = BgPage, Location = Point.Empty, Size = parent.ClientSize };
            EventHandler sync = (s, e) => { if (!ov.IsDisposed) ov.Size = parent.ClientSize; };
            parent.Resize += sync;
            ov.Disposed += (s, e) => parent.Resize -= sync;

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            Button back = new Button { Text = "←", Width = 38, Height = 34, Location = new Point(16, 13), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextDark, Font = new Font("Segoe UI", 13), Cursor = Cursors.Hand };
            back.FlatAppearance.BorderSize = 1; back.FlatAppearance.BorderColor = Border;
            back.Click += (s, e) => { parent.Controls.Remove(ov); ov.Dispose(); refresh?.Invoke(); };
            hdr.Controls.Add(back);
            hdr.Controls.Add(new Label { Text = "Retsept — " + foodName, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(66, 16) });
            Button btnAdd = MakeBtn("+ Masalliq qo'shish", Success, 170);
            hdr.Controls.Add(btnAdd);
            hdr.Resize += (s, e) => btnAdd.Location = new Point(hdr.Width - 190, 13);

            // Portions info bar
            Panel info = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(240, 253, 244) };
            info.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, info.Height - 1, info.Width, info.Height - 1);
            Label lblPort = new Label { Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Success, AutoSize = true };
            info.Controls.Add(lblPort);
            info.Resize += (s, e) => lblPort.Location = new Point(24, (info.Height - lblPort.PreferredHeight) / 2);

            Panel colHdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(243, 244, 246) };
            colHdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, colHdr.Height - 1, colHdr.Width, colHdr.Height - 1);

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };
            ov.Controls.Add(content);
            ov.Controls.Add(colHdr);
            ov.Controls.Add(info);
            ov.Controls.Add(hdr);

            float[] pcts = { .28f, .14f, .12f, .18f, .14f, .14f };
            string[] recipeCols = { "Masalliq", "1 porsiyaga", "Birlik", "Zaxirada", "Holat", "Amallar" };

            colHdr.Resize += (s, e) => { if (colHdr.Width > 0) BuildColHdr(colHdr, recipeCols, pcts); };

            Action loadRecipe = null;
            loadRecipe = () =>
            {
                if (colHdr.Width > 0) BuildColHdr(colHdr, recipeCols, pcts);
                content.SuspendLayout();
                content.Controls.Clear();
                try
                {
                    var db = new dbconnect();
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT ri.id,ri.quantity_per_portion,i.name AS iname,i.unit,i.quantity AS stock FROM recipe_ingredient ri JOIN ingredient i ON i.id=ri.ingredient_id WHERE ri.food_id=@fid ORDER BY i.name",
                        db.GetCon()))
                    { da.SelectCommand.Parameters.AddWithValue("@fid", foodId); da.Fill(dt); }

                    // Portions
                    decimal maxPort = dt.Rows.Count > 0 ? decimal.MaxValue : 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        decimal qpp = Convert.ToDecimal(r["quantity_per_portion"]);
                        decimal stk = Convert.ToDecimal(r["stock"]);
                        if (qpp > 0) maxPort = Math.Min(maxPort, Math.Floor(stk / qpp));
                    }
                    if (maxPort == decimal.MaxValue) maxPort = 0;
                    lblPort.Text = dt.Rows.Count > 0
                        ? $"Hozirgi zaxira bilan {maxPort} ta porsiya tayyorlash mumkin"
                        : "Retsept belgilanmagan";
                    lblPort.ForeColor = maxPort > 5 ? Success : (maxPort > 0 ? Warning : (dt.Rows.Count > 0 ? Danger : TextMuted));

                    // BuildColHdr already called at top of loadRecipe

                    int y = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        int riId = Convert.ToInt32(r["id"]);
                        string iname = r["iname"].ToString();
                        string unit  = r["unit"].ToString();
                        decimal qpp  = Convert.ToDecimal(r["quantity_per_portion"]);
                        decimal stk  = Convert.ToDecimal(r["stock"]);
                        bool ok = stk >= qpp;
                        string _iname = iname; string _unit = unit; decimal _qpp = qpp, _stk = stk; bool _ok = ok; int _rid = riId;

                        Panel row = new Panel { BackColor = y % 2 == 0 ? BgCard : Color.FromArgb(250, 250, 252), Height = 50 };
                        row.Paint += (s2, e2) => e2.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);

                        Action<int> build = (w) =>
                        {
                            row.Controls.Clear();
                            row.Controls.Add(RLbl(_iname, Gx(w, pcts, 0) + 16, Gw(w, pcts, 0), TextDark, FontStyle.Bold, row.Height));
                            row.Controls.Add(RLbl(Fn(_qpp), Gx(w, pcts, 1), Gw(w, pcts, 1), Gold, FontStyle.Bold, row.Height));
                            row.Controls.Add(RLbl(_unit,    Gx(w, pcts, 2), Gw(w, pcts, 2), TextMuted, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(Fn(_stk) + " " + _unit, Gx(w, pcts, 3), Gw(w, pcts, 3), _ok ? TextDark : Danger, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(_ok ? "✓ Yetarli" : "✗ Kam", Gx(w, pcts, 4), Gw(w, pcts, 4), _ok ? Success : Danger, FontStyle.Bold, row.Height));

                            int ax = Gx(w, pcts, 5);
                            // Edit button
                            Button edit = IBtn("✏", Blue); edit.Location = new Point(ax + 4, (row.Height - 28) / 2);
                            edit.Click += (s3, e3) => ShowQppEditOverlay(row, _rid, _iname, _unit, _qpp, loadRecipe);
                            row.Controls.Add(edit);

                            // Delete button
                            Button del = IBtn("✕", Danger); del.Location = new Point(ax + 36, (row.Height - 28) / 2);
                            del.Click += (s3, e3) =>
                            {
                                if (MessageBox.Show($"'{_iname}'ni retseptdan olib tashlaysizmi?", "O'chirish", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                                try
                                {
                                    var db2 = new dbconnect(); db2.OpenCon();
                                    using (var cmd = new SqlCommand("DELETE FROM recipe_ingredient WHERE id=@id", db2.GetCon()))
                                    { cmd.Parameters.AddWithValue("@id", _rid); cmd.ExecuteNonQuery(); }
                                    db2.CloseCon(); loadRecipe();
                                }
                                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                            };
                            row.Controls.Add(del);
                        };
                        row.SetBounds(0, y, Math.Max(content.ClientSize.Width, 400), 50);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row);
                        content.Resize += (s2, e2) => { if (content.ClientSize.Width > 0) { row.Width = content.ClientSize.Width; build(row.Width); } };
                        build(row.Width > 0 ? row.Width : 800);
                        y += 50;
                    }
                    if (dt.Rows.Count == 0)
                        content.Controls.Add(new Label { Text = "Hali masalliq qo'shilmagan. '+ Masalliq qo'shish' tugmasi orqali boshlang.", Font = new Font("Segoe UI", 10), ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 24) });
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { content.ResumeLayout(); }
            };

            btnAdd.Click += (s, e) => { using (var f = new RecipeIngredientForm(foodId)) if (f.ShowDialog() == DialogResult.OK) loadRecipe(); };

            parent.Controls.Add(ov);
            ov.BringToFront();
            BeginInvoke((Action)loadRecipe);
        }

        // ── XARIDLAR ─────────────────────────────────────────────────────────
        private UserControl BuildXaridlarTab()
        {
            var uc = new UserControl { BackColor = BgPage };

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            hdr.Controls.Add(new Label { Text = "Xaridlar tarixi", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16) });
            Button btnAdd = MakeBtn("+ Xarid qo'shish", Gold, 148);
            hdr.Controls.Add(btnAdd);
            hdr.Resize += (s, e) => btnAdd.Location = new Point(hdr.Width - 168, 12);

            Panel stats = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = BgPage };
            Label lblToday = null, lblMonth = null;
            Panel cT = StatCard("Bugungi xaridlar", "0 so'm", Gold,  out lblToday);
            Panel cM = StatCard("Bu oylik jami",    "0 so'm", Blue,  out lblMonth);
            stats.Controls.AddRange(new Control[] { cT, cM });
            stats.Resize += (s, e) =>
            {
                int w = Math.Max((stats.Width - 48) / 3, 120);
                cT.SetBounds(16,     12, w, 60);
                cM.SetBounds(24 + w, 12, w, 60);
            };

            Panel filter = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = BgCard };
            filter.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0, filter.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, filter.Height - 1, filter.Width, filter.Height - 1);
            };
            filter.Controls.Add(new Label { Text = "Dan:", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted, Location = new Point(16, 17) });
            var dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = DateTime.Today.AddDays(-30), Font = new Font("Segoe UI", 9), Location = new Point(48, 14) };
            filter.Controls.Add(new Label { Text = "Gacha:", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted, Location = new Point(170, 17) });
            var dtTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Value = DateTime.Today, Font = new Font("Segoe UI", 9), Location = new Point(214, 14) };
            filter.Controls.Add(dtFrom); filter.Controls.Add(dtTo);

            Panel colHdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(243, 244, 246) };
            colHdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, colHdr.Height - 1, colHdr.Width, colHdr.Height - 1);

            Panel rows = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };

            uc.Controls.Add(rows);
            uc.Controls.Add(colHdr);
            uc.Controls.Add(filter);
            uc.Controls.Add(stats);
            uc.Controls.Add(hdr);

            float[] pcts = { .22f, .12f, .16f, .16f, .18f, .16f };
            string[] xCols = { "Masalliq", "Miqdor", "Narx/birlik", "Jami", "Sana", "Izoh" };

            // Rebuild headers whenever column header panel is resized
            colHdr.Resize += (s, e) => { if (colHdr.Width > 0) BuildColHdr(colHdr, xCols, pcts); };

            Action load = null;
            load = () =>
            {
                rows.SuspendLayout();
                rows.Controls.Clear();
                if (colHdr.Width > 0) BuildColHdr(colHdr, xCols, pcts);
                try
                {
                    var db = new dbconnect();
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(
                        "SELECT p.id,i.name AS iname,i.unit,p.quantity,p.price_per_unit,p.total_price,p.purchased_at,ISNULL(p.notes,'') AS notes FROM ingredient_purchase p JOIN ingredient i ON i.id=p.ingredient_id WHERE CAST(p.purchased_at AS DATE) BETWEEN @d1 AND @d2 ORDER BY p.purchased_at DESC",
                        db.GetCon()))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@d1", dtFrom.Value.Date);
                        da.SelectCommand.Parameters.AddWithValue("@d2", dtTo.Value.Date);
                        da.Fill(dt);
                    }

                    decimal todaySum = 0, monthSum = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        decimal tp = Convert.ToDecimal(r["total_price"]);
                        DateTime pd = Convert.ToDateTime(r["purchased_at"]);
                        if (pd.Date == DateTime.Today) todaySum += tp;
                        if (pd.Year == DateTime.Today.Year && pd.Month == DateTime.Today.Month) monthSum += tp;
                    }
                    if (lblToday != null) lblToday.Text = todaySum.ToString("N0") + " so'm";
                    if (lblMonth != null) lblMonth.Text  = monthSum.ToString("N0") + " so'm";

                    int y = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        string iname = r["iname"].ToString(); string unit = r["unit"].ToString();
                        decimal qty  = Convert.ToDecimal(r["quantity"]);
                        decimal ppu  = Convert.ToDecimal(r["price_per_unit"]);
                        decimal tp   = Convert.ToDecimal(r["total_price"]);
                        DateTime dt2 = Convert.ToDateTime(r["purchased_at"]);
                        string notes = r["notes"].ToString();
                        string _i = iname, _u = unit, _n = notes; decimal _q = qty, _p = ppu, _t = tp; DateTime _d = dt2;

                        Panel row = new Panel { BackColor = y % 2 == 0 ? BgCard : Color.FromArgb(250, 250, 252), Height = 50 };
                        row.Paint += (s2, e2) => e2.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);

                        Action<int> build = (w) =>
                        {
                            row.Controls.Clear();
                            // Width trimmed by 16 so name label ends exactly where next column starts (no overlap)
                            row.Controls.Add(RLbl(_i,   Gx(w,pcts,0)+16, Gw(w,pcts,0)-16, TextDark, FontStyle.Bold, row.Height));
                            row.Controls.Add(RLbl(Fn(_q)+" "+_u, Gx(w,pcts,1), Gw(w,pcts,1), TextDark, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(_p.ToString("N0")+" so'm", Gx(w,pcts,2), Gw(w,pcts,2), TextMuted, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(_t.ToString("N0")+" so'm", Gx(w,pcts,3), Gw(w,pcts,3), Gold, FontStyle.Bold, row.Height));
                            row.Controls.Add(RLbl(_d.ToString("dd.MM.yy HH:mm"), Gx(w,pcts,4), Gw(w,pcts,4), TextMuted, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(_n, Gx(w,pcts,5), Gw(w,pcts,5), TextMuted, FontStyle.Regular, row.Height));
                        };
                        row.SetBounds(0, y, Math.Max(rows.ClientSize.Width, 400), 50);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        rows.Controls.Add(row);
                        rows.Resize += (s2, e2) => { if (rows.ClientSize.Width > 0) { row.Width = rows.ClientSize.Width; build(row.Width); } };
                        build(row.Width > 0 ? row.Width : 800);
                        y += 50;
                    }
                    if (dt.Rows.Count == 0)
                        rows.Controls.Add(new Label { Text = "Bu sana oralig'ida xaridlar yo'q.", Font = new Font("Segoe UI", 11), ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 24) });
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { rows.ResumeLayout(); }
            };

            btnAdd.Click        += (s, e) => { using (var f = new PurchaseForm(0)) if (f.ShowDialog() == DialogResult.OK) load(); };
            dtFrom.ValueChanged += (s, e) => load();
            dtTo.ValueChanged   += (s, e) => load();
            // BeginInvoke defers until layout is complete so colHdr.Width is already set
            uc.Load += (s, e) => BeginInvoke((Action)load);
            return uc;
        }

        // ── MAHSULOT XARIDI ───────────────────────────────────────────────────
        private UserControl BuildMahsulotXaridiTab()
        {
            var uc = new UserControl { BackColor = BgPage };

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            hdr.Controls.Add(new Label { Text = "Mahsulot xaridi", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(24, 16) });
            hdr.Controls.Add(new Label { Text = "Xarid qo'shilganda taomning miqdori avtomatik oshadi", Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 40) });

            // Stats
            Panel stats = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = BgPage };
            Label lblCnt = null, lblLow = null, lblToday = null;
            Panel cCnt   = StatCard("Jami mahsulotlar",   "0",      Gold,    out lblCnt);
            Panel cLow   = StatCard("⚠ Kam qolganlar",    "0 ta",   Danger,  out lblLow);
            Panel cToday = StatCard("Bugungi xaridlar",   "0 ta",   Blue,    out lblToday);
            stats.Controls.AddRange(new Control[] { cCnt, cLow, cToday });
            stats.Resize += (s, e) =>
            {
                int w = Math.Max((stats.Width - 56) / 3, 120);
                cCnt.SetBounds(16,       12, w, 60);
                cLow.SetBounds(24 + w,   12, w, 60);
                cToday.SetBounds(32+w*2, 12, w, 60);
            };

            // Search
            Panel srch = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            srch.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(Border), 0, 0, srch.Width, 0); e.Graphics.DrawLine(new Pen(Border), 0, srch.Height - 1, srch.Width, srch.Height - 1); };
            TextBox txtSrch = new TextBox { Location = new Point(16, 12), Width = 280, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle, BackColor = BgPage };
            SetHint(txtSrch, "Mahsulot qidirish...");
            srch.Controls.Add(txtSrch);

            // Col header
            Panel colHdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(243, 244, 246) };
            colHdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, colHdr.Height - 1, colHdr.Width, colHdr.Height - 1);

            Panel rows = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };

            uc.Controls.Add(rows);
            uc.Controls.Add(colHdr);
            uc.Controls.Add(srch);
            uc.Controls.Add(stats);
            uc.Controls.Add(hdr);

            float[] pcts = { .30f, .18f, .14f, .14f, .13f, .11f };
            string[] cols = { "Mahsulot nomi", "Kategoriya", "Stok miqdori", "Sotish narxi", "Holat", "Amallar" };
            colHdr.Resize += (s, e) => { if (colHdr.Width > 0) BuildColHdr(colHdr, cols, pcts); };

            Action load = null;
            load = () =>
            {
                if (colHdr.Width > 0) BuildColHdr(colHdr, cols, pcts);
                rows.SuspendLayout();
                rows.Controls.Clear();
                string q = txtSrch.Text.Trim();
                try
                {
                    var db = new dbconnect();
                    var dt = new DataTable();
                    using (var da = new SqlDataAdapter(@"
                        SELECT f.id, f.name, ISNULL(fc.name,'—') AS cat,
                               ISNULL(f.[count],0) AS cnt,
                               f.selling_price,
                               ISNULL(f.is_unlimited,0) AS unlimited
                        FROM food f
                        LEFT JOIN food_category fc ON fc.id=f.food_category_id
                        WHERE (@s='' OR f.name LIKE '%'+@s+'%')
                        ORDER BY fc.name, f.name", db.GetCon()))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@s", q);
                        da.Fill(dt);
                    }

                    int totalCnt = 0, lowCnt = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        totalCnt++;
                        bool unlim = Convert.ToBoolean(r["unlimited"]);
                        int  stok  = Convert.ToInt32(r["cnt"]);
                        if (!unlim && stok < 5) lowCnt++;
                    }
                    if (lblCnt   != null) lblCnt.Text   = totalCnt + " ta";
                    if (lblLow   != null) lblLow.Text   = lowCnt   + " ta";

                    // Today purchases count
                    int todayCnt = 0;
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM food_purchase WHERE CAST(purchased_at AS DATE)=CAST(GETDATE() AS DATE)", db.GetCon()))
                            todayCnt = (int)cmd.ExecuteScalar();
                    } catch { }
                    if (lblToday != null) lblToday.Text = todayCnt + " ta";

                    int y = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        int    fid   = Convert.ToInt32(r["id"]);
                        string fname = r["name"].ToString();
                        string fcat  = r["cat"].ToString();
                        int    stok  = Convert.ToInt32(r["cnt"]);
                        decimal narx = Convert.ToDecimal(r["selling_price"]);
                        bool unlim   = Convert.ToBoolean(r["unlimited"]);

                        string _fn = fname, _fc = fcat; int _stok = stok; decimal _narx = narx; bool _ul = unlim;
                        string holat   = unlim ? "Cheksiz" : stok <= 0 ? "✗ Tugagan" : stok < 5 ? "⚠ Kam" : stok < 20 ? "O'rtacha" : "Yetarli";
                        Color holatClr = unlim ? Blue : stok <= 0 ? Danger : stok < 5 ? Danger : stok < 20 ? Warning : Success;

                        Panel row = new Panel { BackColor = y % 2 == 0 ? BgCard : Color.FromArgb(250, 250, 252), Height = 50 };
                        row.Paint += (s2, e2) => e2.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);

                        string _h = holat; Color _hc = holatClr;
                        Action<int> buildRow = (w) =>
                        {
                            // Remove only static labels (not overlay panels)
                            for (int ci = row.Controls.Count - 1; ci >= 0; ci--)
                                if (row.Controls[ci] is Label) row.Controls.RemoveAt(ci);
                            row.Controls.Add(RLbl(_fn, Gx(w, pcts, 0)+16, Gw(w, pcts, 0)-16, TextDark,  FontStyle.Bold,    row.Height));
                            row.Controls.Add(RLbl(_fc, Gx(w, pcts, 1),    Gw(w, pcts, 1),    TextMuted, FontStyle.Regular,  row.Height));
                            row.Controls.Add(RLbl(_ul ? "∞" : _stok+" dona", Gx(w, pcts, 2), Gw(w, pcts, 2), _ul ? Blue : (_stok<5?Danger:TextDark), FontStyle.Bold, row.Height));
                            row.Controls.Add(RLbl(_narx.ToString("N0")+" so'm", Gx(w, pcts, 3), Gw(w, pcts, 3), TextMuted, FontStyle.Regular, row.Height));
                            row.Controls.Add(RLbl(_h, Gx(w, pcts, 4), Gw(w, pcts, 4), _hc, FontStyle.Bold, row.Height));
                        };

                        // "➕ Xarid" button
                        Button btnBuy = MakeBtn("➕ Xarid", Success, 90);
                        btnBuy.Height = 32; btnBuy.Font = new Font("Segoe UI", 8, FontStyle.Bold);
                        row.Controls.Add(btnBuy);

                        int _fid = fid; string _fname = fname;
                        btnBuy.Click += (s2, e2) => ShowFoodPurchaseOverlay(row, _fid, _fname, () => load());

                        row.SetBounds(0, y, Math.Max(rows.ClientSize.Width, 500), 50);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        rows.Controls.Add(row);
                        rows.Resize += (s2, e2) =>
                        {
                            if (rows.ClientSize.Width > 0)
                            {
                                row.Width = rows.ClientSize.Width;
                                buildRow(row.Width);
                                // reposition button
                                int bx = Gx(row.Width, pcts, 5) + 4;
                                btnBuy.Location = new Point(bx, (row.Height - btnBuy.Height) / 2);
                            }
                        };
                        buildRow(row.Width > 0 ? row.Width : 800);
                        // initial button position
                        int ibx = Gx(row.Width > 0 ? row.Width : 800, pcts, 5) + 4;
                        btnBuy.Location = new Point(ibx, (row.Height - btnBuy.Height) / 2);

                        y += 50;
                    }
                    if (dt.Rows.Count == 0)
                        rows.Controls.Add(new Label { Text = "Mahsulotlar topilmadi.", Font = new Font("Segoe UI", 11), ForeColor = TextMuted, AutoSize = true, Location = new Point(24, 24) });

                    db.CloseCon();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { rows.ResumeLayout(); }
            };

            txtSrch.TextChanged += (s, e) => load();
            uc.Load += (s, e) => BeginInvoke((Action)load);
            return uc;
        }

        private static void ShowFoodPurchaseOverlay(Panel row, int foodId, string foodName, Action reload)
        {
            // Remove existing overlay
            for (int i = row.Controls.Count - 1; i >= 0; i--)
                if (row.Controls[i] is Panel) { row.Controls.RemoveAt(i); break; }

            var strip = new Panel { BackColor = Color.FromArgb(240, 253, 244), Location = Point.Empty, Size = row.Size };
            strip.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Color.FromArgb(16, 185, 129)), 0, 0, strip.Width - 1, strip.Height - 1);

            strip.Controls.Add(new Label { Text = foodName + ":", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(22, 163, 74), AutoSize = true, Location = new Point(12, (strip.Height - 16) / 2) });

            strip.Controls.Add(new Label { Text = "Miqdor:", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(107, 114, 128), AutoSize = true, Location = new Point(180, 6) });
            var numQty = new NumericUpDown { Location = new Point(180, 22), Width = 80, Minimum = 1, Maximum = 9999, Value = 1, DecimalPlaces = 0, Font = new Font("Segoe UI", 9), BackColor = Color.White };
            strip.Controls.Add(numQty);

            strip.Controls.Add(new Label { Text = "Narx (so'm):", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(107, 114, 128), AutoSize = true, Location = new Point(272, 6) });
            var txtPrice = new TextBox { Location = new Point(272, 22), Width = 100, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle, Text = "0" };
            strip.Controls.Add(txtPrice);

            strip.Controls.Add(new Label { Text = "Izoh:", Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(107, 114, 128), AutoSize = true, Location = new Point(384, 6) });
            var txtNote = new TextBox { Location = new Point(384, 22), Width = 140, Font = new Font("Segoe UI", 9), BorderStyle = BorderStyle.FixedSingle };
            strip.Controls.Add(txtNote);

            Button btnSave = MakeBtn("Saqlash", Color.FromArgb(22, 163, 74), 80); btnSave.Height = 28; btnSave.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            btnSave.Location = new Point(536, (strip.Height - 28) / 2);
            strip.Controls.Add(btnSave);

            Button btnX = MakeBtn("✕", Color.FromArgb(107, 114, 128), 36); btnX.Height = 28;
            btnX.Location = new Point(622, (strip.Height - 28) / 2);
            strip.Controls.Add(btnX);

            Action close = () => { row.Controls.Remove(strip); strip.Dispose(); };

            btnX.Click += (s, e) => close();

            btnSave.Click += (s, e) =>
            {
                int qty = (int)numQty.Value;
                decimal.TryParse(txtPrice.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal price);
                decimal total = qty * price;
                try
                {
                    var db = new dbconnect(); db.OpenCon();
                    int newPurchaseId;
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO food_purchase(food_id,quantity,price_per_unit,total_price,notes,purchased_at)
                        OUTPUT INSERTED.id
                        VALUES(@fid,@qty,@ppu,@total,@note,GETDATE())", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@fid",   foodId);
                        cmd.Parameters.AddWithValue("@qty",   qty);
                        cmd.Parameters.AddWithValue("@ppu",   price);
                        cmd.Parameters.AddWithValue("@total", total);
                        cmd.Parameters.AddWithValue("@note",  string.IsNullOrEmpty(txtNote.Text) ? (object)DBNull.Value : txtNote.Text.Trim());
                        newPurchaseId = (int)cmd.ExecuteScalar();
                    }
                    using (var cmd = new SqlCommand(
                        "UPDATE food SET [count]=ISNULL([count],0)+@qty WHERE id=@fid", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@qty", qty);
                        cmd.Parameters.AddWithValue("@fid", foodId);
                        cmd.ExecuteNonQuery();
                    }
                    db.CloseCon();
                    // Darhol sync trigger
                    SyncQueueHelper.Add(SyncQueueHelper.FoodPurchases, newPurchaseId, "Insert");
                    System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
                    close();
                    reload?.Invoke();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };

            row.Controls.Add(strip);
            strip.BringToFront();
            numQty.Focus();
        }

        // ── SHARED HELPERS ────────────────────────────────────────────────────
        private static Panel StatCard(string title, string val, Color accent, out Label lbl)
        {
            Panel card = new Panel { BackColor = BgCard };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Border)) e.Graphics.DrawRectangle(p, 0, 0, card.Width-1, card.Height-1);
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, 4, card.Height);
            };
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 8), ForeColor = TextMuted, AutoSize = true, Location = new Point(12, 6) });
            Label lv = new Label { Text = val, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = accent, AutoSize = true, Location = new Point(12, 24) };
            card.Controls.Add(lv);
            lbl = lv;
            return card;
        }

        private static void BuildColHdr(Panel panel, string[] cols, float[] pcts)
        {
            panel.Controls.Clear();
            int x = 16, w = panel.Width > 0 ? panel.Width : 800;
            for (int i = 0; i < cols.Length; i++)
            {
                int cw = (int)(w * pcts[i]);
                panel.Controls.Add(new Label { Text = cols[i], Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = TextMuted, Location = new Point(x, 0), Width = cw, Height = panel.Height > 0 ? panel.Height : 40, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false });
                x += cw;
            }
        }

        private static int Gx(int w, float[] p, int i) { int x = 16; for (int j = 0; j < i; j++) x += (int)(w * p[j]); return x; }
        private static int Gw(int w, float[] p, int i) => (int)(w * p[i]);

        private static Label RLbl(string t, int x, int w, Color fc, FontStyle fs, int h) =>
            new Label { Text = t, Font = new Font("Segoe UI", 10, fs), ForeColor = fc, Location = new Point(x, 0), Width = w, Height = h, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false };

        private static Button MakeBtn(string text, Color bg, int w)
        {
            Button b = new Button { Text = text, Width = w, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private static Button IBtn(string icon, Color color)
        {
            Button b = new Button { Text = icon, Width = 28, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(18, color.R, color.G, color.B), ForeColor = color, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (s, e) => b.BackColor = Color.FromArgb(38, color.R, color.G, color.B);
            b.MouseLeave += (s, e) => b.BackColor = Color.FromArgb(18, color.R, color.G, color.B);
            return b;
        }

        private static string Fn(decimal d) => d == Math.Floor(d) ? ((long)d).ToString() : d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);
        private static void SetHint(TextBox tb, string h) => tb.HandleCreated += (s, e) => SendMessage(tb.Handle, 0x1501, (IntPtr)1, h);

        private static void ShowQppEditOverlay(Panel row, int riId, string ingredientName, string unit, decimal currentQpp, Action reload)
        {
            var strip = new Panel
            {
                BackColor = Color.FromArgb(255, 248, 230),
                Location  = new Point(0, 0),
                Size      = row.Size
            };

            strip.Controls.Add(new Label
            {
                Text      = ingredientName + "  —  1 porsiyaga:",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize  = true,
                Location  = new Point(16, (strip.Height - 18) / 2)
            });

            // TextBox — accepts both "0.2" and "0,2"
            var txt = new TextBox
            {
                Text        = currentQpp.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.'),
                Width       = 100,
                Height      = 28,
                Font        = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.White,
                Location    = new Point(300, (strip.Height - 28) / 2)
            };
            strip.Controls.Add(txt);

            strip.Controls.Add(new Label
            {
                Text      = unit,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Gold,
                AutoSize  = true,
                Location  = new Point(408, (strip.Height - 18) / 2)
            });

            Button btnOk = MakeBtn("Saqlash", Success, 80); btnOk.Height = 28;
            btnOk.Location = new Point(450, (strip.Height - 28) / 2);
            strip.Controls.Add(btnOk);

            Button btnX = MakeBtn("Bekor", TextMuted, 64); btnX.Height = 28;
            btnX.FlatAppearance.BorderSize = 0;
            btnX.Location = new Point(536, (strip.Height - 28) / 2);
            strip.Controls.Add(btnX);

            Action close = () => { row.Controls.Remove(strip); strip.Dispose(); };

            Action save = () =>
            {
                string raw = txt.Text.Trim().Replace(',', '.');
                decimal newQpp;
                if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out newQpp) || newQpp <= 0)
                {
                    txt.BackColor = Color.FromArgb(254, 226, 226);
                    MessageBox.Show("Noto'g'ri qiymat. Masalan: 0.2 yoki 0,2", "Xatolik");
                    txt.BackColor = Color.White;
                    txt.Focus(); txt.SelectAll();
                    return;
                }
                try
                {
                    var db = new dbconnect(); db.OpenCon();
                    using (var cmd = new SqlCommand("UPDATE recipe_ingredient SET quantity_per_portion=@q WHERE id=@id", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@q", newQpp);
                        cmd.Parameters.AddWithValue("@id", riId);
                        cmd.ExecuteNonQuery();
                    }
                    db.CloseCon();
                    close(); reload?.Invoke();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };

            btnOk.Click += (s, e) => save();
            btnX.Click  += (s, e) => close();
            txt.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)  { e.SuppressKeyPress = true; save(); }
                if (e.KeyCode == Keys.Escape) close();
            };

            row.Controls.Add(strip);
            strip.BringToFront();
            txt.Focus(); txt.SelectAll();
        }

        private static GraphicsPath RR(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, rad*2, rad*2, 180, 90);
            p.AddArc(r.Right-rad*2, r.Y, rad*2, rad*2, 270, 90);
            p.AddArc(r.Right-rad*2, r.Bottom-rad*2, rad*2, rad*2, 0, 90);
            p.AddArc(r.X, r.Bottom-rad*2, rad*2, rad*2, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
