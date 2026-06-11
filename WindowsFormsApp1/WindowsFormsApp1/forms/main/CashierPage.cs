using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.main
{
    public class CashierPage : Form
    {
        // ── Ranglar ──────────────────────────────────────────────────────────
        static readonly Color C_Bg        = Color.FromArgb(245, 246, 250);
        static readonly Color C_White     = Color.White;
        static readonly Color C_Dark      = Color.FromArgb(17, 24, 39);
        static readonly Color C_Muted     = Color.FromArgb(107, 114, 128);
        static readonly Color C_Border    = Color.FromArgb(229, 231, 235);
        static readonly Color C_Primary   = Color.FromArgb(59, 130, 246);   // ko'k
        static readonly Color C_PrimaryBg = Color.FromArgb(239, 246, 255);
        static readonly Color C_Green     = Color.FromArgb(16, 185, 129);
        static readonly Color C_GreenBg   = Color.FromArgb(236, 253, 245);
        static readonly Color C_Red       = Color.FromArgb(239, 68, 68);
        static readonly Color C_RedBg     = Color.FromArgb(254, 242, 242);
        static readonly Color C_Amber     = Color.FromArgb(245, 158, 11);
        static readonly Color C_AmberBg   = Color.FromArgb(255, 251, 235);
        static readonly Color C_Purple    = Color.FromArgb(139, 92, 246);
        static readonly Color C_PurpleBg  = Color.FromArgb(245, 243, 255);

        // ── State ────────────────────────────────────────────────────────────
        Panel  _pageArea;          // content
        int    _activeTab   = -1;  // 0=Joylar, 1=Buyurtmalar
        Timer  _refreshTimer;
        int    _refreshSec  = 5;
        int    _refreshLeft = 5;
        Label  _lblClock;

        // Joylar state
        string _activeZone  = "";   // "" = barchasi
        Panel  _tableGrid;
        Label  _lblEmpty, _lblBusy;
        List<(int id, string name)> _zones = new List<(int, string)>();
        Panel  _zoneTabs;

        // Buyurtmalar state
        string _ordFilter   = "NO";
        Panel  _orderList;
        Panel  _orderDetail;
        string _searchText  = "";

        // ════════════════════════════════════════════════════════════════════
        public CashierPage()
        {
            InitLayout();
            Load += (s, e) => SwitchTab(0);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ASOSIY LAYOUT
        // ════════════════════════════════════════════════════════════════════
        void InitLayout()
        {
            WindowState     = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            BackColor       = C_Bg;
            Text            = "FoodX — Kassir";
            Font            = new Font("Segoe UI", 9);

            // ── Top nav bar ──────────────────────────────────────────────────
            Panel nav = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 58,
                BackColor = C_White
            };
            nav.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_Border), 0, 57, nav.Width, 57);
            Controls.Add(nav);

            // Logo
            Label logo = new Label
            {
                Text      = "FoodX",
                Font      = new Font("Segoe UI", 17, FontStyle.Bold),
                ForeColor = C_Primary,
                AutoSize  = true,
                Location  = new Point(24, 12)
            };
            nav.Controls.Add(logo);

            // Tab butonlar
            Button btnJoylar = NavTab("⬛  Joylar", 0);
            Button btnOrders = NavTab("☰  Buyurtmalar", 1);
            btnJoylar.Location = new Point(150, 10);
            btnOrders.Location = new Point(270, 10);
            nav.Controls.Add(btnJoylar);
            nav.Controls.Add(btnOrders);

            // Soat (refresh countdown)
            _lblClock = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Muted,
                AutoSize  = true,
                Location  = new Point(0, 20)
            };
            nav.Controls.Add(_lblClock);
            nav.Resize += (s, e) => _lblClock.Location = new Point(nav.Width - 310, 20);

            // Foydalanuvchi va chiqish
            Panel userPanel = new Panel
            {
                Width     = 280,
                Height    = 40,
                Location  = new Point(0, 9),
                BackColor = Color.Transparent
            };
            nav.Resize += (s, e) => userPanel.Location = new Point(nav.Width - 290, 9);

            // Avatar
            Panel av = new Panel { Width = 36, Height = 36, Location = new Point(0, 2), BackColor = Color.Transparent };
            av.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(C_PrimaryBg))
                    e.Graphics.FillEllipse(br, 0, 0, 35, 35);
                using (var pen = new Pen(C_Primary, 1.5f))
                    e.Graphics.DrawEllipse(pen, 0, 0, 35, 35);
                string ini = Session.UserName?.Length >= 1
                    ? (Session.UserName.Length >= 2
                        ? Session.UserName.Substring(0, 2).ToUpper()
                        : Session.UserName.Substring(0, 1).ToUpper())
                    : "?";
                using (var f  = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(ini, f, new SolidBrush(C_Primary), new RectangleF(0, 0, 35, 35), sf);
            };
            userPanel.Controls.Add(av);

            userPanel.Controls.Add(new Label
            {
                Text      = Session.UserName ?? "",
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = C_Dark,
                AutoSize  = true,
                Location  = new Point(44, 4)
            });
            userPanel.Controls.Add(new Label
            {
                Text      = "Kassir",
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Primary,
                AutoSize  = true,
                Location  = new Point(44, 22)
            });

            // Chiqish tugmasi
            Button btnExit = new Button
            {
                Text      = "⏻",
                Width     = 36, Height = 36,
                Location  = new Point(240, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = C_Bg,
                ForeColor = C_Muted,
                Font      = new Font("Segoe UI", 13),
                Cursor    = Cursors.Hand
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.MouseEnter += (s, e) => { btnExit.BackColor = C_RedBg; btnExit.ForeColor = C_Red; };
            btnExit.MouseLeave += (s, e) => { btnExit.BackColor = C_Bg;    btnExit.ForeColor = C_Muted; };
            btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show("Chiqishni tasdiqlaysizmi?", "Chiqish",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Session.Clear();
                    Hide();
                    new Form1().Show();
                }
            };
            userPanel.Controls.Add(btnExit);
            nav.Controls.Add(userPanel);

            // ── Content ──────────────────────────────────────────────────────
            _pageArea = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg };
            Controls.Add(_pageArea);
            _pageArea.BringToFront();

            // ── Refresh timer ─────────────────────────────────────────────────
            _refreshTimer = new Timer { Interval = 1000 };
            _refreshTimer.Tick += RefreshTick;
            _refreshTimer.Start();
        }

        Button NavTab(string text, int idx)
        {
            Button b = new Button
            {
                Text      = text,
                Width     = 110, Height = 38,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9),
                Cursor    = Cursors.Hand,
                Tag       = idx
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => SwitchTab((int)b.Tag);
            return b;
        }

        void ApplyNavStyle()
        {
            foreach (Control c in Controls)
            {
                if (c is Panel nav && nav.Dock == DockStyle.Top)
                {
                    foreach (Control nc in nav.Controls)
                    {
                        if (nc is Button b && b.Tag is int idx)
                        {
                            bool on = idx == _activeTab;
                            b.BackColor = on ? C_PrimaryBg : Color.Transparent;
                            b.ForeColor = on ? C_Primary   : C_Muted;
                            b.Font      = new Font("Segoe UI", 9, on ? FontStyle.Bold : FontStyle.Regular);
                        }
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  NAVIGATSIYA
        // ════════════════════════════════════════════════════════════════════
        void SwitchTab(int tab)
        {
            _activeTab = tab;
            ApplyNavStyle();
            _pageArea.Controls.Clear();

            if (tab == 0)
            {
                _refreshSec  = 5;
                _refreshLeft = 5;
                BuildJoylarView();
            }
            else
            {
                _refreshSec  = 15;
                _refreshLeft = 15;
                BuildBuyurtmalarView();
            }
        }

        void RefreshTick(object sender, EventArgs e)
        {
            _refreshLeft--;
            _lblClock.Text = $"↺ {_refreshLeft}s";

            if (_refreshLeft <= 0)
            {
                _refreshLeft = _refreshSec;
                if (_activeTab == 0) RefreshTables();
                else RefreshOrderList();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TAB 0 — JOYLAR
        // ════════════════════════════════════════════════════════════════════
        void BuildJoylarView()
        {
            // ── Statistika satri ─────────────────────────────────────────────
            _lblEmpty = null; _lblBusy = null; _lblOchiq = null; _lblSum = null;
            Label lblE, lblB, lblO, lblS;
            Panel sc1 = StatChip("Bo'sh",      "—", C_Green,  out lblE);
            Panel sc2 = StatChip("Band",       "—", C_Red,    out lblB);
            Panel sc3 = StatChip("Ochiq zakaz","—", C_Amber,  out lblO);
            Panel sc4 = StatChip("Jami summa", "—", C_Purple, out lblS);
            _lblEmpty = lblE; _lblBusy = lblB; _lblOchiq = lblO; _lblSum = lblS;

            Panel stats = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = C_White };
            stats.Paint  += (s, e) => e.Graphics.DrawLine(new Pen(C_Border), 0, 83, stats.Width, 83);
            stats.Resize += (s, e) => LayoutStatChips(stats, sc1, sc2, sc3, sc4);
            stats.Controls.AddRange(new Control[] { sc1, sc2, sc3, sc4 });

            // ── Zone tabs ────────────────────────────────────────────────────
            Panel zoneBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = C_White };
            zoneBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(C_Border), 0, 0,  zoneBar.Width, 0);
                e.Graphics.DrawLine(new Pen(C_Border), 0, 47, zoneBar.Width, 47);
            };
            _zoneTabs = zoneBar;

            // ── Table grid (scroll) ──────────────────────────────────────────
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg, AutoScroll = true };

            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock          = DockStyle.Top,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                WrapContents  = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor     = C_Bg,
                Padding       = new Padding(18, 14, 18, 18)
            };
            scroll.Controls.Add(flp);

            // WrapContents ishlashi uchun MaximumSize cheklash KERAK (WinForms klassik yechim)
            Action fixWidth = () =>
            {
                int w = scroll.ClientSize.Width;
                if (w > 10) flp.MaximumSize = new Size(w, 0);
            };
            scroll.Resize += (s, e) => fixWidth();

            // Controls.Add tartib: WinForms da Dock=Top — OXIRGI qo'shilgan ENG TEPAGA chiqadi
            _pageArea.Controls.Add(scroll);   // Fill — birinchi qo'sh (eng pastga)
            _pageArea.Controls.Add(zoneBar);  // Top — ikkinchi (stats ostida bo'ladi)
            _pageArea.Controls.Add(stats);    // Top — oxirgi (eng tepada bo'ladi)

            _tableGrid = flp;

            // BeginInvoke: layout tugagandan keyin ishga tush — scroll o'lchami to'g'ri bo'ladi
            BeginInvoke(new Action(() =>
            {
                fixWidth();
                LoadZones(() => { BuildZoneTabs(); RefreshTables(); });
            }));
        }

        void LoadZones(Action done)
        {
            _zones.Clear();
            try
            {
                string sql = "SELECT id, name FROM place_out ORDER BY ISNULL(sort_order,9999), name";
                DataTable dt = new DataTable();
                using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                    da.Fill(dt);
                foreach (DataRow r in dt.Rows)
                    _zones.Add((Convert.ToInt32(r["id"]), r["name"].ToString()));
            }
            catch { }
            done?.Invoke();
        }

        void BuildZoneTabs()
        {
            _zoneTabs.Controls.Clear();
            int x = 16;

            ZoneTabBtn("Barchasi", "", ref x);
            foreach (var z in _zones)
            {
                string zName = z.name;
                ZoneTabBtn(z.name, zName, ref x);
            }
        }

        void ZoneTabBtn(string label, string zoneFilter, ref int x)
        {
            bool on = _activeZone == zoneFilter;
            Button b = new Button
            {
                Text      = label,
                Location  = new Point(x, 8),
                Height    = 30,
                AutoSize  = false,
                Width     = TextRenderer.MeasureText(label, new Font("Segoe UI", 9, FontStyle.Bold)).Width + 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = on ? C_Primary   : C_Bg,
                ForeColor = on ? Color.White  : C_Muted,
                Font      = new Font("Segoe UI", 9, on ? FontStyle.Bold : FontStyle.Regular),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize  = 1;
            b.FlatAppearance.BorderColor = on ? C_Primary : C_Border;
            b.Click += (s, e) =>
            {
                _activeZone  = zoneFilter;
                _refreshLeft = 1;
                BuildZoneTabs();
                RefreshTables();
            };
            _zoneTabs.Controls.Add(b);
            x += b.Width + 6;
        }

        void RefreshTables()
        {
            if (_tableGrid == null) return;

            // MaximumSize ni scroll panelidan olish (WrapContents uchun)
            var scrollPnl = _tableGrid.Parent as Panel;
            if (scrollPnl != null && scrollPnl.ClientSize.Width > 10)
                _tableGrid.MaximumSize = new Size(scrollPnl.ClientSize.Width, 0);

            _tableGrid.SuspendLayout();
            _tableGrid.Controls.Clear();
            try
            {
                string zoneWhere = string.IsNullOrEmpty(_activeZone)
                    ? "" : $"AND po.name='{_activeZone.Replace("'", "''")}'";

                string sql = $@"
                    SELECT po.name AS zone, po.price, po.price_type,
                           pi.id AS tid, pi.room_name, pi.empty,
                           (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS cnt,
                           (SELECT ISNULL(SUM(total),0) FROM [order] WHERE place_id=pi.id AND paid='NO') AS ord_total
                    FROM place_out po
                    JOIN place_category pc ON pc.id = po.place_category_id
                    JOIN place_in pi ON pi.place_out_id = po.id
                    WHERE 1=1 {zoneWhere}
                    ORDER BY ISNULL(po.sort_order,9999), po.name,
                        TRY_CAST(SUBSTRING(pi.room_name,1,
                            PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT),
                        pi.room_name";

                DataTable dt = new DataTable();
                using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                    da.Fill(dt);

                int bosh = 0, band = 0, ochiq = 0;
                decimal totalSum = 0;
                string curZone = null;

                foreach (DataRow r in dt.Rows)
                {
                    string zone    = r["zone"].ToString();
                    int    tid     = Convert.ToInt32(r["tid"]);
                    string rname   = r["room_name"].ToString();
                    int    cnt     = Convert.ToInt32(r["cnt"]);
                    bool   empty   = r["empty"].ToString().ToUpper() == "YES" && cnt == 0;
                    decimal oTotal = Convert.ToDecimal(r["ord_total"]);

                    if (empty) bosh++; else { band++; if (cnt > 0) { ochiq++; totalSum += oTotal; } }

                    if (zone != curZone)
                    {
                        curZone = zone;
                        _tableGrid.Controls.Add(ZoneLabel(zone));
                    }
                    _tableGrid.Controls.Add(TableTile(tid, rname, zone, empty, cnt, oTotal));
                }

                UpdateStatsLabels(bosh, band, ochiq, totalSum);

                if (dt.Rows.Count == 0)
                    _tableGrid.Controls.Add(EmptyLabel("Hech qanday stol topilmadi"));
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { _tableGrid.ResumeLayout(); }
        }

        // Stats labellari uchun reference saqlash
        Label _lblOchiq, _lblSum;

        void UpdateStatsLabels(int bosh, int band, int ochiq, decimal sum)
        {
            if (_lblEmpty != null) _lblEmpty.Text = bosh.ToString();
            if (_lblBusy  != null) _lblBusy.Text  = band.ToString();
            if (_lblOchiq != null) _lblOchiq.Text  = ochiq.ToString();
            if (_lblSum   != null) _lblSum.Text    = sum.ToString("N0") + " so'm";
        }

        Panel TableTile(int tableId, string name, string zone, bool empty, int cnt, decimal ordTotal)
        {
            Color accent = empty ? C_Green : (cnt > 0 ? C_Amber : C_Red);
            Color bg     = empty ? C_GreenBg : (cnt > 0 ? C_AmberBg : C_RedBg);

            Panel tile = new Panel
            {
                Width     = 190, Height = 170,
                Margin    = new Padding(6),
                BackColor = C_White,
                Cursor    = Cursors.Hand
            };

            // Gölge va border
            tile.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Yuqori rang chizig'i (5px)
                using (var br = new SolidBrush(accent))
                    g.FillRectangle(br, 0, 0, tile.Width, 5);

                // Border
                using (var pen = new Pen(Color.FromArgb(40, accent), 1))
                    g.DrawRectangle(pen, 0, 0, tile.Width - 1, tile.Height - 1);
            };

            // Status badge (top-right)
            Panel badge = new Panel
            {
                Width     = 74, Height = 22,
                Location  = new Point(tile.Width - 80, 12),
                BackColor = bg
            };
            badge.Paint += (s, e) =>
            {
                string txt = empty ? "Bo'sh" : (cnt > 0 ? "Aktiv" : "Band");
                using (var f  = new Font("Segoe UI", 8, FontStyle.Bold))
                using (var br = new SolidBrush(accent))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(txt, f, br, new RectangleF(0, 0, badge.Width, badge.Height), sf);
                using (var pen = new Pen(Color.FromArgb(60, accent)))
                    e.Graphics.DrawRectangle(pen, 0, 0, badge.Width - 1, badge.Height - 1);
            };
            tile.Controls.Add(badge);

            // Stol nomi — KATTA
            Label lblName = new Label
            {
                Text      = name,
                Font      = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = C_Dark,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 18),
                Width     = tile.Width, Height = 52
            };
            tile.Controls.Add(lblName);

            // Zona nomi
            Label lblZone = new Label
            {
                Text      = zone,
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Muted,
                TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 68),
                Width     = tile.Width, Height = 18
            };
            tile.Controls.Add(lblZone);

            // Agar aktiv buyurtma bo'lsa — summa
            if (cnt > 0)
            {
                tile.Controls.Add(new Label
                {
                    Text      = ordTotal.ToString("N0") + " so'm",
                    Font      = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = C_Amber,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location  = new Point(0, 86),
                    Width     = tile.Width, Height = 26
                });
                tile.Controls.Add(new Label
                {
                    Text      = cnt + " ta aktiv buyurtma",
                    Font      = new Font("Segoe UI", 8),
                    ForeColor = C_Muted,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location  = new Point(0, 110),
                    Width     = tile.Width, Height = 18
                });
            }

            // Tugma
            Button btn = new Button
            {
                Text      = empty ? "+ Yangi zakaz" : "Ko'rish",
                Location  = new Point(12, tile.Height - 44),
                Width     = tile.Width - 24, Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            EventHandler openOrder = (s, e) => OpenTableOrder(tableId);
            tile.Click  += openOrder;
            lblName.Click += openOrder;
            lblZone.Click += openOrder;
            btn.Click   += openOrder;
            tile.Controls.Add(btn);

            return tile;
        }

        void OpenTableOrder(int tableId)
        {
            try
            {
                int existId = 0;
                var con = new dbconnect();
                con.OpenCon();
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 id FROM [order] WHERE place_id=@p AND paid='NO'", con.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@p", tableId);
                    object r = cmd.ExecuteScalar();
                    if (r != null) existId = Convert.ToInt32(r);
                }
                con.CloseCon();
                new AddOrder(tableId, existId, Session.Login).ShowDialog();
                _refreshLeft = 1;
                RefreshTables();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        Panel ZoneLabel(string zone)
        {
            Panel p = new Panel
            {
                Height    = 44,
                Width     = Math.Max(_tableGrid.Width > 36 ? _tableGrid.Width - 36 : 800, 200),
                Margin    = new Padding(0, 14, 0, 2),
                BackColor = Color.Transparent
            };
            _tableGrid.Resize += (s, e) =>
                p.Width = Math.Max(_tableGrid.Width > 36 ? _tableGrid.Width - 36 : 800, 200);

            p.Controls.Add(new Label
            {
                Text      = zone,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = C_Dark,
                AutoSize  = true,
                Location  = new Point(2, 4)
            });
            Panel line = new Panel { Height = 1, Dock = DockStyle.Bottom, BackColor = C_Border };
            p.Controls.Add(line);
            return p;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TAB 1 — BUYURTMALAR (split: list + detail)
        // ════════════════════════════════════════════════════════════════════
        void BuildBuyurtmalarView()
        {
            // ── Top bar (filter + search) ────────────────────────────────────
            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = C_White };
            topBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(C_Border), 0, 55, topBar.Width, 55);
            };
            _pageArea.Controls.Add(topBar);

            // Filter tugmalar
            string[] fLbl = { "Ochiq", "Yopilgan", "Barchasi" };
            string[] fVal = { "NO",    "YES",       "ALL" };
            Button[] fBtns = new Button[3];
            int fx = 16;
            for (int i = 0; i < 3; i++)
            {
                Button fb = FilterBtn(fLbl[i], fx);
                fBtns[i] = fb;
                topBar.Controls.Add(fb);
                fx += fb.Width + 6;
            }

            // Qidiruv
            Panel searchBox = new Panel
            {
                Width     = 220, Height = 32,
                Location  = new Point(fx + 12, 12),
                BackColor = C_Bg
            };
            searchBox.Paint += (s, e) =>
                e.Graphics.DrawRectangle(new Pen(C_Border), 0, 0, searchBox.Width - 1, searchBox.Height - 1);

            Label iconSrch = new Label
            {
                Text = "🔍", AutoSize = true,
                Location = new Point(6, 6), ForeColor = C_Muted,
                Font = new Font("Segoe UI", 9)
            };
            searchBox.Controls.Add(iconSrch);

            TextBox txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 9),
                Width       = 178, Height = 20,
                Location    = new Point(28, 6),
                BackColor   = C_Bg,
                ForeColor   = C_Dark
            };
            txtSearch.TextChanged += (s, e) => { _searchText = txtSearch.Text; _refreshLeft = 1; RefreshOrderList(); };
            searchBox.Controls.Add(txtSearch);
            topBar.Controls.Add(searchBox);
            topBar.Resize += (s, e) => searchBox.Location = new Point(topBar.Width - 240, 12);

            // ── Split panel ──────────────────────────────────────────────────
            Panel split = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg };
            _pageArea.Controls.Add(split);

            // Order list (chap)
            Panel listPanel = new Panel
            {
                Width     = 380,
                Dock      = DockStyle.Left,
                BackColor = C_White
            };
            listPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_Border), listPanel.Width - 1, 0, listPanel.Width - 1, listPanel.Height);

            Panel listScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = C_White };
            FlowLayoutPanel flpList = new FlowLayoutPanel
            {
                Location      = new Point(0, 0),
                Width         = 378,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                WrapContents  = false,
                FlowDirection = FlowDirection.TopDown,
                BackColor     = C_White,
                Padding       = new Padding(0)
            };
            listScroll.Controls.Add(flpList);
            listScroll.SizeChanged += (s, e) => { if (listScroll.ClientSize.Width > 0) flpList.Width = listScroll.ClientSize.Width; };
            listPanel.Controls.Add(listScroll);

            // List header
            Panel listHdr = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = C_Bg };
            listHdr.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_Border), 0, 45, listHdr.Width, 45);
            listHdr.Controls.Add(new Label
            {
                Text      = "Buyurtmalar",
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = C_Dark,
                AutoSize  = true,
                Location  = new Point(16, 14)
            });
            listPanel.Controls.Add(listHdr);
            split.Controls.Add(listPanel);
            _orderList = flpList;

            // Detail panel (o'ng)
            Panel detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg };
            split.Controls.Add(detailPanel);
            _orderDetail = detailPanel;

            // Bo'sh holat
            ShowDetailEmpty();

            // Filter stilini qo'llash
            Action reStyleFilter = () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    bool on = fVal[i] == _ordFilter;
                    fBtns[i].BackColor = on ? C_Primary   : C_Bg;
                    fBtns[i].ForeColor = on ? Color.White  : C_Muted;
                    fBtns[i].Font      = new Font("Segoe UI", 9, on ? FontStyle.Bold : FontStyle.Regular);
                    fBtns[i].FlatAppearance.BorderColor = on ? C_Primary : C_Border;
                }
            };

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                fBtns[i].Click += (s, e) =>
                {
                    _ordFilter = fVal[idx];
                    reStyleFilter();
                    _refreshLeft = 1;
                    RefreshOrderList();
                };
            }

            reStyleFilter();
            RefreshOrderList();
        }

        Button FilterBtn(string text, int x)
        {
            Button b = new Button
            {
                Text      = text,
                Location  = new Point(x, 12),
                Height    = 32,
                Width     = TextRenderer.MeasureText(text, new Font("Segoe UI", 9, FontStyle.Bold)).Width + 24,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_Bg,
                ForeColor = C_Muted,
                Font      = new Font("Segoe UI", 9),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize  = 1;
            b.FlatAppearance.BorderColor = C_Border;
            return b;
        }

        void RefreshOrderList()
        {
            if (_orderList == null) return;
            _orderList.SuspendLayout();
            _orderList.Controls.Clear();
            try
            {
                string where = _ordFilter == "ALL" ? "" : $"AND o.paid='{_ordFilter}'";
                string srch  = string.IsNullOrWhiteSpace(_searchText) ? ""
                    : $"AND (ISNULL(pi.room_name,'') LIKE '%{_searchText.Replace("'", "''")}%' OR ISNULL(u.name,'') LIKE '%{_searchText.Replace("'", "''")}%')";

                string sql = $@"
                    SELECT o.id, o.total, o.created_at, o.paid,
                           ISNULL(pi.room_name,'—') AS room_name,
                           ISNULL(po.name,'—')      AS zone,
                           (SELECT COUNT(*) FROM order_food WHERE order_id=o.id) AS items,
                           ISNULL(u.name,'')        AS waiter,
                           ISNULL(o.is_customer_order,0) AS is_cust,
                           ISNULL(o.customer_name,'')    AS cust_name
                    FROM [order] o
                    LEFT JOIN place_in  pi ON pi.id = o.place_id
                    LEFT JOIN place_out po ON po.id = pi.place_out_id
                    LEFT JOIN [user]     u ON u.id  = o.user_id
                    WHERE 1=1 {where} {srch}
                    ORDER BY o.paid ASC, o.created_at DESC";

                DataTable dt = new DataTable();
                using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                    da.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    _orderList.Controls.Add(EmptyListItem("Buyurtma topilmadi"));
                }
                else
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        int     oid    = Convert.ToInt32(r["id"]);
                        decimal total  = Convert.ToDecimal(r["total"]);
                        DateTime creat = Convert.ToDateTime(r["created_at"]);
                        bool    paid   = r["paid"].ToString() == "YES";
                        bool    isCust = Convert.ToInt32(r["is_cust"]) == 1;
                        string  place  = isCust
                            ? (r["cust_name"].ToString() != "" ? r["cust_name"].ToString() : "Mijoz")
                            : (r["zone"] + " — " + r["room_name"]);
                        int    items   = Convert.ToInt32(r["items"]);
                        string waiter  = r["waiter"].ToString();

                        _orderList.Controls.Add(OrderListItem(oid, total, creat, paid, place, items, waiter, isCust));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { _orderList.ResumeLayout(); }
        }

        Panel OrderListItem(int oid, decimal total, DateTime created, bool paid,
            string place, int items, string waiter, bool isCust)
        {
            Color accent = paid ? C_Green : (isCust ? C_Purple : C_Primary);
            Color bg     = paid ? C_GreenBg : (isCust ? C_PurpleBg : C_PrimaryBg);

            int rowW = _orderList != null && _orderList.Width > 40 ? _orderList.Width : 378;
            Panel row = new Panel
            {
                Width     = rowW,
                Height    = 78,
                Margin    = new Padding(0),
                BackColor = C_White,
                Cursor    = Cursors.Hand
            };
            // Parent width o'zgarganda row ham kengaysin
            if (_orderList != null)
                _orderList.SizeChanged += (s, e) => { if (_orderList.Width > 40) row.Width = _orderList.Width; };
            row.Paint += (s, e) =>
            {
                // Sol rang chizig'i
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, 0, 0, 4, row.Height);
                // Alt bg on hover yapilmaydi — divider
                e.Graphics.DrawLine(new Pen(C_Border), 0, row.Height - 1, row.Width, row.Height - 1);
            };

            // Status dot
            Panel dot = new Panel { Width = 8, Height = 8, Location = new Point(14, 14), BackColor = Color.Transparent };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(accent), 0, 0, 7, 7);
            };
            row.Controls.Add(dot);

            Label lblName = new Label
            {
                Text      = "#" + oid + "  " + place,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = C_Dark,
                Location  = new Point(28, 10),
                Width     = 260, Height = 18,
                Cursor    = Cursors.Hand
            };
            row.Controls.Add(lblName);

            Label lblMeta = new Label
            {
                Text      = $"{items} taom  •  {created:HH:mm}  •  {(string.IsNullOrEmpty(waiter) ? "" : waiter)}",
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Muted,
                Location  = new Point(28, 30),
                Width     = 260, Height = 16,
                Cursor    = Cursors.Hand
            };
            row.Controls.Add(lblMeta);

            Label lblAmt = new Label
            {
                Text      = total.ToString("N0") + " so'm",
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = accent,
                Location  = new Point(28, 48),
                Width     = 200, Height = 22,
                Cursor    = Cursors.Hand
            };
            row.Controls.Add(lblAmt);

            // Tahrirlash
            if (!paid)
            {
                Button btnEdit = new Button
                {
                    Text      = "→",
                    Width     = 30, Height = 30,
                    Location  = new Point(row.Width - 38, 24),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = C_Primary,
                    Font      = new Font("Segoe UI", 12, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                row.SizeChanged += (s, e) => btnEdit.Location = new Point(row.Width - 38, 24);
                btnEdit.Click += (s, e) => OpenOrderDetail(oid);
                row.Controls.Add(btnEdit);
            }

            EventHandler openDet = (s, e) => OpenOrderDetail(oid);
            row.Click += openDet;
            lblName.Click += openDet;
            lblMeta.Click += openDet;
            lblAmt.Click  += openDet;

            return row;
        }

        void OpenOrderDetail(int orderId)
        {
            if (_orderDetail == null) return;
            _orderDetail.Controls.Clear();

            // Sag panel: buyurtma tafsilotlari
            try
            {
                DataTable dt = new DataTable();
                string sql = @"
                    SELECT o.id, o.total, o.created_at, o.paid, o.note,
                           ISNULL(pi.room_name,'—') AS room_name,
                           ISNULL(po.name,'—')      AS zone,
                           ISNULL(u.name,'')        AS waiter,
                           ISNULL(o.is_customer_order,0) AS is_cust,
                           ISNULL(o.customer_name,'')    AS cust_name,
                           ISNULL(o.delivery_phone,'')   AS dph,
                           ISNULL(o.is_delivery,0)       AS isdel
                    FROM [order] o
                    LEFT JOIN place_in  pi ON pi.id = o.place_id
                    LEFT JOIN place_out po ON po.id = pi.place_out_id
                    LEFT JOIN [user]     u ON u.id  = o.user_id
                    WHERE o.id=@oid";
                using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(dt);
                }
                if (dt.Rows.Count == 0) { ShowDetailEmpty(); return; }

                DataRow ro  = dt.Rows[0];
                bool    paid = ro["paid"].ToString() == "YES";
                bool    isCust = Convert.ToInt32(ro["is_cust"]) == 1;
                string  place  = isCust
                    ? (ro["cust_name"].ToString() != "" ? ro["cust_name"].ToString() : "Mijoz buyurtmasi")
                    : (ro["zone"] + " — " + ro["room_name"]);
                decimal total  = Convert.ToDecimal(ro["total"]);
                string  waiter = ro["waiter"].ToString();
                DateTime creat = Convert.ToDateTime(ro["created_at"]);
                Color   accent = paid ? C_Green : (isCust ? C_Purple : C_Primary);

                // Taomlar
                DataTable foods = new DataTable();
                string fsql = @"SELECT f.name, of.quantity, of.price, ISNULL(of.note,'') AS note
                                FROM order_food of JOIN food f ON f.id=of.food_id
                                WHERE of.order_id=@oid ORDER BY of.id";
                using (var da = new SqlDataAdapter(fsql, new dbconnect().GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(foods);
                }

                // ── Detail UI ─────────────────────────────────────────────────
                Panel det = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg, AutoScroll = true, Padding = new Padding(24, 20, 24, 20) };
                _orderDetail.Controls.Add(det);

                // Sarlavha
                Panel hdrPan = new Panel
                {
                    Dock      = DockStyle.Top,
                    Height    = 70,
                    BackColor = C_White,
                    Margin    = new Padding(0, 0, 0, 12)
                };
                hdrPan.Paint += (s, e) =>
                {
                    using (var br = new SolidBrush(accent))
                        e.Graphics.FillRectangle(br, 0, 0, hdrPan.Width, 5);
                    e.Graphics.DrawLine(new Pen(C_Border), 0, 69, hdrPan.Width, 69);
                };
                hdrPan.Controls.Add(new Label
                {
                    Text      = $"#{orderId} — {place}",
                    Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = C_Dark,
                    Location  = new Point(18, 14),
                    AutoSize  = true
                });
                hdrPan.Controls.Add(new Label
                {
                    Text      = $"{creat:dd.MM.yyyy  HH:mm}  •  {waiter}  •  {(paid ? "Yopilgan ✓" : "Ochiq")}",
                    Font      = new Font("Segoe UI", 8),
                    ForeColor = paid ? C_Green : C_Muted,
                    Location  = new Point(18, 42),
                    AutoSize  = true
                });

                if (!paid)
                {
                    Button btnOpen = new Button
                    {
                        Text      = "Tahrirlash →",
                        AutoSize  = true, Height = 30,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = accent, ForeColor = Color.White,
                        Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                        Cursor    = Cursors.Hand
                    };
                    btnOpen.FlatAppearance.BorderSize = 0;
                    btnOpen.Location = new Point(0, 18);
                    btnOpen.Click += (s, e) =>
                    {
                        new AddOrder(0, orderId, Session.Login).ShowDialog();
                        _refreshLeft = 1;
                        RefreshOrderList();
                        ShowDetailEmpty();
                    };
                    hdrPan.Resize += (s, e2) => btnOpen.Location = new Point(hdrPan.Width - btnOpen.Width - 18, 20);
                    hdrPan.Controls.Add(btnOpen);
                }
                det.Controls.Add(hdrPan);

                // Taomlar ro'yxati
                Panel foodsCard = new Panel { Dock = DockStyle.Top, BackColor = C_White, Padding = new Padding(18, 12, 18, 12) };
                foodsCard.Paint += (s, e) =>
                    e.Graphics.DrawLine(new Pen(C_Border), 0, foodsCard.Height - 1, foodsCard.Width, foodsCard.Height - 1);

                int cardH = 32 + foods.Rows.Count * 36;
                foodsCard.Height = cardH;

                foodsCard.Controls.Add(new Label
                {
                    Text      = "Taomlar",
                    Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = C_Muted,
                    Location  = new Point(18, 10),
                    AutoSize  = true
                });

                int fy = 34;
                foreach (DataRow fr in foods.Rows)
                {
                    string fn  = fr["name"].ToString();
                    int    qty = Convert.ToInt32(fr["quantity"]);
                    decimal fp = Convert.ToDecimal(fr["price"]);
                    string fnote = fr["note"].ToString();

                    foodsCard.Controls.Add(new Label
                    {
                        Text      = $"× {qty}  {fn}{(fnote != "" ? "  (" + fnote + ")" : "")}",
                        Font      = new Font("Segoe UI", 9),
                        ForeColor = C_Dark,
                        Location  = new Point(18, fy),
                        AutoSize  = true
                    });
                    foodsCard.Controls.Add(new Label
                    {
                        Text      = (fp * qty).ToString("N0"),
                        Font      = new Font("Segoe UI", 9),
                        ForeColor = C_Muted,
                        AutoSize  = true,
                        Location  = new Point(0, fy)
                    });
                    foodsCard.Resize += (s, e) =>
                    { /* right-align handled individually below */ };

                    fy += 36;
                }

                // Right-align price labels
                foreach (Control fc in foodsCard.Controls)
                {
                    if (fc is Label fl && fl.Location.X == 0 && fl.Location.Y >= 34)
                    {
                        int lY = fl.Location.Y;
                        foodsCard.Resize += (s, e) => fl.Location = new Point(foodsCard.Width - fl.Width - 18, lY);
                    }
                }

                det.Controls.Add(foodsCard);

                // Jami
                Panel totalCard = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = C_White, Margin = new Padding(0, 4, 0, 0) };
                totalCard.Paint += (s, e) =>
                {
                    e.Graphics.DrawLine(new Pen(C_Border), 18, 0, totalCard.Width - 18, 0);
                    using (var f  = new Font("Segoe UI", 14, FontStyle.Bold))
                    using (var br = new SolidBrush(accent))
                    using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
                    {
                        e.Graphics.DrawString("JAMI", new Font("Segoe UI", 9, FontStyle.Bold),
                            new SolidBrush(C_Muted), new RectangleF(18, 0, 100, 56), sf);
                        string ts = total.ToString("N0") + " so'm";
                        SizeF sz = e.Graphics.MeasureString(ts, f);
                        e.Graphics.DrawString(ts, f, br,
                            new RectangleF(totalCard.Width - sz.Width - 18, 0, sz.Width + 4, 56), sf);
                    }
                };
                det.Controls.Add(totalCard);
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        void ShowDetailEmpty()
        {
            if (_orderDetail == null) return;
            _orderDetail.Controls.Clear();
            Panel center = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg };
            center.Controls.Add(new Label
            {
                Text      = "← Buyurtmani tanlang",
                Font      = new Font("Segoe UI", 13),
                ForeColor = C_Muted,
                AutoSize  = true,
                Location  = new Point(60, 160)
            });
            _orderDetail.Controls.Add(center);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPER METODLAR
        // ════════════════════════════════════════════════════════════════════

        Panel StatChip(string label, string initVal, Color accent, out Label valLbl)
        {
            Panel p = new Panel { BackColor = C_White };
            p.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(C_Border), p.Width - 1, 10, p.Width - 1, p.Height - 10);
            };
            p.Controls.Add(new Label
            {
                Text      = label,
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Muted,
                Location  = new Point(20, 12),
                AutoSize  = true
            });
            valLbl = new Label
            {
                Text      = initVal,
                Font      = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = accent,
                Location  = new Point(20, 30),
                Width     = 240, Height = 36
            };
            p.Controls.Add(valLbl);
            return p;
        }

        void LayoutStatChips(Panel stats, params Panel[] chips)
        {
            if (stats.Width < 10) return;
            int gap = 0;
            int w   = stats.Width / chips.Length;
            for (int i = 0; i < chips.Length; i++)
                chips[i].SetBounds(i * w + gap, 0, w - gap, stats.Height);
        }

        Label EmptyLabel(string text)
        {
            return new Label
            {
                Text      = text,
                AutoSize  = true,
                Margin    = new Padding(40),
                Font      = new Font("Segoe UI", 13),
                ForeColor = C_Muted
            };
        }

        Panel EmptyListItem(string text)
        {
            int w = _orderList != null && _orderList.Width > 40 ? _orderList.Width : 378;
            Panel p = new Panel { Width = w, Height = 60, BackColor = C_White };
            if (_orderList != null)
                _orderList.SizeChanged += (s, e) => { if (_orderList.Width > 40) p.Width = _orderList.Width; };
            p.Controls.Add(new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 10),
                ForeColor = C_Muted,
                AutoSize  = true,
                Location  = new Point(20, 20)
            });
            return p;
        }

        protected override void Dispose(bool disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            ClientSize = new Size(1400, 800);
            Name = "CashierPage";
            ResumeLayout(false);
        }
    }
}
