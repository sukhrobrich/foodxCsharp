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

        // Joylar state
        string _activeZone  = "";   // "" = barchasi
        Panel  _tableGrid;
        Label  _lblEmpty, _lblBusy;
        List<(int id, string name)> _zones = new List<(int, string)>();
        Panel  _zoneTabs;
        Action _newOrderHandler;
        Action<SyncEngine.NewOrderInfo> _newOrderToastHandler;
        Action<StockAlertService.LowStockInfo> _lowStockToastHandler;

        // Tile diff: tableId → updater (qayta yaratmasdan faqat yangilash uchun)
        readonly Dictionary<int, Action<bool, int, decimal>> _tileUpdaters
            = new Dictionary<int, Action<bool, int, decimal>>();

        // Multimonoblok rejimida buyurtma download timer (SyncService ishlamaydi)
        Timer _multiDlTimer;

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


            // Foydalanuvchi va chiqish
            Panel userPanel = new Panel
            {
                Width     = 280,
                Height    = 40,
                Location  = new Point(0, 9),
                BackColor = Color.Transparent
            };
            nav.Resize += (s, e) => userPanel.Location = new Point(nav.Width - 295, 9);

            Label lblLicenseDays = new Label
            {
                Font     = new Font("Segoe UI", 8.5f),
                AutoSize = true,
                Location = new Point(0, 20)
            };
            nav.Controls.Add(lblLicenseDays);
            nav.Resize += (s, e) => lblLicenseDays.Location = new Point(nav.Width - 460, 20);

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
                    try { PrintService.SetSetting("last_logged_in_user", ""); } catch { }
                    Session.Clear();
                    if (WindowsFormsApp1.services.MultiMonoblokConfig.IsMultiMonoblokMode)
                        Close();
                    else
                    {
                        Hide();
                        new Form1().Show();
                    }
                }
            };
            userPanel.Controls.Add(btnExit);
            nav.Controls.Add(userPanel);

            // ── Content ──────────────────────────────────────────────────────
            _pageArea = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg };
            Controls.Add(_pageArea);
            _pageArea.BringToFront();

            // ── Refresh timer ─────────────────────────────────────────────────
            LicenseDaysDisplay.Apply(lblLicenseDays);
            _refreshTimer = new Timer { Interval = 1000 };
            _refreshTimer.Tick += RefreshTick;
            _refreshTimer.Tick += (s, e) => LicenseDaysDisplay.Apply(lblLicenseDays);
            _refreshTimer.Start();

            // ── Yangi buyurtma bildirishnomasi (toast + ovoz) ───────────────────
            // Tab qaysi bo'lishidan qat'iy nazar ishlaydi; admin sozlamadan o'chirib qo'yishi mumkin
            _newOrderToastHandler = info =>
            {
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && PrintService.GetSetting("new_order_notify", "1") == "1")
                            NewOrderToast.Show(info, this);
                    }));
                }
                catch { }
            };
            SyncEngine.NewOrderCreated += _newOrderToastHandler;

            // ── Kam qolgan ingredient bildirishnomasi (toast + ovoz) ────────────
            _lowStockToastHandler = info =>
            {
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && PrintService.GetSetting("low_stock_notify", "1") == "1")
                            NewOrderToast.Show("⚠ Kam qolgan mahsulot",
                                string.Format("{0}  •  {1:N1} {2} qoldi", info.Name, info.Quantity, info.Unit),
                                NewOrderToast.C_AccentWarning, this);
                    }));
                }
                catch { }
            };
            StockAlertService.LowStockDetected += _lowStockToastHandler;

            // ── Multimonoblok rejimida SyncService ishlamaydi — o'zimiz pollayamiz ──
            // Har 2 soniyada yangi buyurtmalarni centraldan yuklab NewOrderCreated/
            // NewOrdersArrived eventlarini otiramiz → toast + refresh ishlaydi.
            if (MultiMonoblokConfig.IsMultiMonoblokMode && Session.IsOnline)
            {
                _multiDlTimer = new Timer { Interval = 2000 };
                _multiDlTimer.Tick += (s, e) =>
                {
                    if (IsDisposed) { _multiDlTimer.Stop(); return; }
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { SyncEngine.DownloadOrdersFast(); } catch { }
                    });
                };
                _multiDlTimer.Start();
            }
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
            if (_newOrderHandler != null)
            {
                SyncEngine.NewOrdersArrived -= _newOrderHandler;
                _newOrderHandler = null;
            }
            _tileUpdaters.Clear();
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
                _refreshSec  = 30;
                _refreshLeft = 30;
                BuildBuyurtmalarView();
            }
        }

        void RefreshTick(object sender, EventArgs e)
        {
            _refreshLeft--;
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

            // Yangi buyurtma tushganda darhol yangilash (_refreshLeft ni kutmasdan)
            _newOrderHandler = () =>
            {
                if (_activeTab == 0 && !IsDisposed)
                    try { BeginInvoke(new Action(() => { _refreshLeft = 1; })); } catch { }
            };
            SyncEngine.NewOrdersArrived += _newOrderHandler;
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

            var scrollPnl = _tableGrid.Parent as Panel;
            if (scrollPnl != null && scrollPnl.ClientSize.Width > 10)
                _tableGrid.MaximumSize = new Size(scrollPnl.ClientSize.Width, 0);

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

                // Yangi ID to'plami
                var newIds = new HashSet<int>();
                foreach (DataRow r in dt.Rows) newIds.Add(Convert.ToInt32(r["tid"]));

                // Tuzilma o'zgarganmi? (stol qo'shildi yoki o'chirildi — kam bo'ladi)
                bool structChanged = _tileUpdaters.Count == 0
                    || newIds.Count != _tileUpdaters.Count
                    || !newIds.IsSubsetOf(_tileUpdaters.Keys);

                int bosh = 0, band = 0, ochiq = 0;
                decimal totalSum = 0;

                if (structChanged)
                {
                    // To'liq qayta qurilish — faqat stol ro'yxati o'zgarganda
                    _tileUpdaters.Clear();
                    _tableGrid.SuspendLayout();
                    _tableGrid.Controls.Clear();
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

                        if (zone != curZone) { curZone = zone; _tableGrid.Controls.Add(ZoneLabel(zone)); }

                        Action<bool, int, decimal> upd;
                        _tableGrid.Controls.Add(TableTile(tid, rname, zone, empty, cnt, oTotal, out upd));
                        _tileUpdaters[tid] = upd;
                    }
                    if (dt.Rows.Count == 0) _tableGrid.Controls.Add(EmptyLabel("Hech qanday stol topilmadi"));
                    _tableGrid.ResumeLayout();
                }
                else
                {
                    // Lipillashsiz yangilanish: faqat o'zgargan holat qiymatlarini update qiladi
                    foreach (DataRow r in dt.Rows)
                    {
                        int     tid    = Convert.ToInt32(r["tid"]);
                        int     cnt    = Convert.ToInt32(r["cnt"]);
                        bool    empty  = r["empty"].ToString().ToUpper() == "YES" && cnt == 0;
                        decimal oTotal = Convert.ToDecimal(r["ord_total"]);

                        if (empty) bosh++; else { band++; if (cnt > 0) { ochiq++; totalSum += oTotal; } }

                        Action<bool, int, decimal> upd;
                        if (_tileUpdaters.TryGetValue(tid, out upd)) upd(empty, cnt, oTotal);
                    }
                }

                UpdateStatsLabels(bosh, band, ochiq, totalSum);
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
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

        Panel TableTile(int tableId, string name, string zone, bool empty, int cnt, decimal ordTotal,
                        out Action<bool, int, decimal> updater)
        {
            // Mutable state — closurelar array ni ushlaydi, qiymatni emas → yangilanadi
            Color[] ac = { empty ? C_Green : (cnt > 0 ? C_Amber : C_Red) };
            Color[] bg = { empty ? C_GreenBg : (cnt > 0 ? C_AmberBg : C_RedBg) };
            bool[]  em = { empty };
            int[]   cn = { cnt };

            Panel tile = new Panel
            {
                Width     = 190, Height = 170,
                Margin    = new Padding(6),
                BackColor = C_White,
                Cursor    = Cursors.Hand
            };
            tile.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(ac[0]))
                    e.Graphics.FillRectangle(br, 0, 0, tile.Width, 5);
                using (var pen = new Pen(Color.FromArgb(40, ac[0]), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, tile.Width - 1, tile.Height - 1);
            };

            Panel badge = new Panel { Width = 74, Height = 22, Location = new Point(tile.Width - 80, 12), BackColor = bg[0] };
            badge.Paint += (s, e) =>
            {
                string txt = em[0] ? "Bo'sh" : (cn[0] > 0 ? "Aktiv" : "Band");
                using (var f  = new Font("Segoe UI", 8, FontStyle.Bold))
                using (var br = new SolidBrush(ac[0]))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(txt, f, br, new RectangleF(0, 0, badge.Width, badge.Height), sf);
                using (var pen = new Pen(Color.FromArgb(60, ac[0])))
                    e.Graphics.DrawRectangle(pen, 0, 0, badge.Width - 1, badge.Height - 1);
            };
            tile.Controls.Add(badge);

            Label lblName = new Label
            {
                Text = name, Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = C_Dark, TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 18), Width = tile.Width, Height = 52
            };
            tile.Controls.Add(lblName);

            Label lblZone = new Label
            {
                Text = zone, Font = new Font("Segoe UI", 8),
                ForeColor = C_Muted, TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 68), Width = tile.Width, Height = 18
            };
            tile.Controls.Add(lblZone);

            // Summa va soni — har doim qo'shiladi, Visible orqali ko'rsatiladi/yashiriladi
            Label lblSum = new Label
            {
                Text      = cnt > 0 ? ordTotal.ToString("N0") + " so'm" : "",
                Font      = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = C_Amber, TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 86), Width = tile.Width, Height = 26,
                Visible   = cnt > 0
            };
            tile.Controls.Add(lblSum);

            Label lblCnt = new Label
            {
                Text      = cnt > 0 ? cnt + " ta aktiv buyurtma" : "",
                Font      = new Font("Segoe UI", 8),
                ForeColor = C_Muted, TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(0, 110), Width = tile.Width, Height = 18,
                Visible   = cnt > 0
            };
            tile.Controls.Add(lblCnt);

            Button btn = new Button
            {
                Text      = empty ? "+ Yangi zakaz" : "Ko'rish",
                Location  = new Point(12, tile.Height - 44),
                Width     = tile.Width - 24, Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = ac[0], ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            EventHandler openOrder = (s, e) => OpenTableOrder(tableId);
            tile.Click   += openOrder;
            lblName.Click += openOrder;
            lblZone.Click += openOrder;
            btn.Click    += openOrder;
            tile.Controls.Add(btn);

            // Yangilash funksiyasi — panel qayta yaratilmaydi, faqat qiymatlar o'zgaradi
            updater = (newEmpty, newCnt, newTotal) =>
            {
                em[0] = newEmpty;
                cn[0] = newCnt;
                ac[0] = newEmpty ? C_Green : (newCnt > 0 ? C_Amber : C_Red);
                bg[0] = newEmpty ? C_GreenBg : (newCnt > 0 ? C_AmberBg : C_RedBg);

                badge.BackColor = bg[0];
                lblSum.Text     = newCnt > 0 ? newTotal.ToString("N0") + " so'm" : "";
                lblSum.Visible  = newCnt > 0;
                lblCnt.Text     = newCnt > 0 ? newCnt + " ta aktiv buyurtma" : "";
                lblCnt.Visible  = newCnt > 0;
                btn.Text        = newEmpty ? "+ Yangi zakaz" : "Ko'rish";
                btn.BackColor   = ac[0];

                tile.Invalidate();   // yuqori chiziq + border qayta chiziladi
                badge.Invalidate();  // badge matni qayta chiziladi
            };

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
            // Ko'rinmas refresh — list yangilanishda parvoz qilmasin
            var listScrollParent = _orderList.Parent;
            if (listScrollParent != null) listScrollParent.Visible = false;
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
                           ISNULL(o.customer_name,'') AS cust_name
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
                        string custName = r["cust_name"].ToString();
                        bool   isCust  = !string.IsNullOrEmpty(custName);
                        string place   = isCust
                            ? custName
                            : (r["zone"] + " — " + r["room_name"]);
                        int    items   = Convert.ToInt32(r["items"]);
                        string waiter  = r["waiter"].ToString();

                        _orderList.Controls.Add(OrderListItem(oid, total, creat, paid, place, items, waiter, isCust));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally
            {
                _orderList.ResumeLayout();
                if (listScrollParent != null) listScrollParent.Visible = true;
            }
        }

        Panel OrderListItem(int oid, decimal total, DateTime created, bool paid,
            string place, int items, string waiter, bool isCust)
        {
            Color accent    = paid ? C_Green : (isCust ? C_Purple : C_Primary);
            string titleTxt = $"#{oid}  {place}";
            string metaTxt  = $"{items} taom  •  {created:HH:mm}" +
                              (string.IsNullOrEmpty(waiter) ? "" : "  •  " + waiter);
            string amtTxt   = total.ToString("N0") + " so'm";

            int rowW = _orderList != null && _orderList.Width > 40 ? _orderList.Width : 378;
            Panel row = new Panel { Width = rowW, Height = 78, Margin = new Padding(0),
                                    BackColor = C_White, Cursor = Cursors.Hand };

            if (_orderList != null)
                _orderList.SizeChanged += (s, e) =>
                {
                    if (_orderList.Width > 40) { row.Width = _orderList.Width; row.Invalidate(); }
                };

            Font fTitle = new Font("Segoe UI", 9, FontStyle.Bold);
            Font fMeta  = new Font("Segoe UI", 8);
            Font fAmt   = new Font("Segoe UI", 11, FontStyle.Bold);

            row.Paint += (s, e) =>
            {
                var g = e.Graphics;
                // Sol rang chizig'i
                using (var br = new SolidBrush(accent))
                    g.FillRectangle(br, 0, 0, 4, row.Height);
                // Pastki ajratuvchi chiziq
                g.DrawLine(new Pen(C_Border), 0, row.Height - 1, row.Width, row.Height - 1);
                // Status doira
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(accent), 14, 13, 8, 8);
                g.SmoothingMode = SmoothingMode.Default;
                // Matnlar — TextRenderer har doim ishlaydi
                int textW = row.Width - 68;
                TextRenderer.DrawText(g, titleTxt, fTitle,
                    new Rectangle(28, 8, textW, 20), C_Dark,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, metaTxt, fMeta,
                    new Rectangle(28, 28, textW, 18), C_Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, amtTxt, fAmt,
                    new Rectangle(28, 48, textW, 22), accent,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            if (!paid)
            {
                Button btnEdit = new Button
                {
                    Text = "→", Width = 30, Height = 30,
                    Location  = new Point(row.Width - 38, 24),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent, ForeColor = C_Primary,
                    Font      = new Font("Segoe UI", 12, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btnEdit.FlatAppearance.BorderSize = 0;
                row.SizeChanged += (s, e) => btnEdit.Location = new Point(row.Width - 38, 24);
                btnEdit.Click += (s, e) => OpenOrderDetail(oid);
                row.Controls.Add(btnEdit);
            }

            row.Click += (s, e) => OpenOrderDetail(oid);
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
                    SELECT o.id, o.total, o.created_at, o.paid,
                           ISNULL(pi.room_name,'—') AS room_name,
                           ISNULL(po.name,'—')      AS zone,
                           ISNULL(u.name,'')        AS waiter,
                           ISNULL(o.customer_name,'') AS cust_name
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
                bool    paid    = ro["paid"].ToString() == "YES";
                string  custNm  = ro["cust_name"].ToString();
                bool    isCust  = !string.IsNullOrEmpty(custNm);
                string  place   = isCust
                    ? custNm
                    : (ro["zone"] + " — " + ro["room_name"]);
                decimal total  = Convert.ToDecimal(ro["total"]);
                string  waiter = ro["waiter"].ToString();
                DateTime creat = Convert.ToDateTime(ro["created_at"]);
                Color   accent = paid ? C_Green : (isCust ? C_Purple : C_Primary);

                // Taomlar
                DataTable foods = new DataTable();
                string fsql = @"SELECT f.name, ofd.quantity, f.selling_price AS price
                                FROM order_food ofd
                                LEFT JOIN food f ON f.id = ofd.food_id
                                               OR (f.central_id = ofd.food_id
                                                   AND NOT EXISTS(SELECT 1 FROM food WHERE id = ofd.food_id))
                                WHERE ofd.order_id=@oid AND f.id IS NOT NULL ORDER BY ofd.id";
                using (var da = new SqlDataAdapter(fsql, new dbconnect().GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@oid", orderId);
                    da.Fill(foods);
                }

                // ── Detail UI ─────────────────────────────────────────────────
                // WinForms Dock=Top: oxirgi qo'shilgan eng tepaga chiqadi.
                // Shuning uchun teskari tartibda qo'shamiz: pastki element birinchi.

                Panel det = new Panel { Dock = DockStyle.Fill, BackColor = C_Bg, AutoScroll = true };
                _orderDetail.Controls.Add(det);

                det.SuspendLayout();

                // 1. JAMI (eng oxirgi — vizual pastda bo'ladi, lekin birinchi qo'shiladi)
                Panel totalCard = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = C_White };
                totalCard.Paint += (s, e) =>
                {
                    e.Graphics.DrawLine(new Pen(C_Border), 16, 0, totalCard.Width - 16, 0);
                    using (var f  = new Font("Segoe UI", 14, FontStyle.Bold))
                    using (var br = new SolidBrush(accent))
                    using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
                    {
                        e.Graphics.DrawString("JAMI", new Font("Segoe UI", 9, FontStyle.Bold),
                            new SolidBrush(C_Muted), new RectangleF(16, 0, 80, 56), sf);
                        string ts = total.ToString("N0") + " so'm";
                        SizeF  sz = e.Graphics.MeasureString(ts, f);
                        e.Graphics.DrawString(ts, f, br,
                            new RectangleF(totalCard.Width - sz.Width - 16, 0, sz.Width + 4, 56), sf);
                    }
                };
                det.Controls.Add(totalCard);

                // 2. TAOM QATORLARI (teskari tartibda qo'shamiz)
                const int PriceColW = 120;
                for (int fi = foods.Rows.Count - 1; fi >= 0; fi--)
                {
                    DataRow fr       = foods.Rows[fi];
                    string  foodName = fr["name"].ToString();
                    int     qty      = Convert.ToInt32(fr["quantity"]);
                    decimal fp       = Convert.ToDecimal(fr["price"]);

                    Panel foodRow = new Panel
                    {
                        Dock = DockStyle.Top, Height = 36, BackColor = C_White,
                        Padding = new Padding(0, 0, 0, 1)
                    };
                    foodRow.Paint += (s, e) =>
                        e.Graphics.DrawLine(new Pen(Color.FromArgb(235, 235, 235)),
                            16, 35, foodRow.Width - 16, 35);

                    Label lblPrice = new Label
                    {
                        Text      = (fp * qty).ToString("N0") + " so'm",
                        Font      = new Font("Segoe UI", 9),
                        ForeColor = C_Muted,
                        Width     = PriceColW,
                        Dock      = DockStyle.Right,
                        TextAlign = ContentAlignment.MiddleRight,
                        Padding   = new Padding(0, 0, 8, 0),
                    };
                    Label lblName = new Label
                    {
                        Text         = $"x{qty}  {foodName}",
                        Font         = new Font("Segoe UI", 9),
                        ForeColor    = C_Dark,
                        Dock         = DockStyle.Fill,
                        TextAlign    = ContentAlignment.MiddleLeft,
                        AutoEllipsis = true,
                        Padding      = new Padding(16, 0, 0, 0),
                    };
                    // lblName (Fill) birinchi, lblPrice (Right) keyin — Right avval joy oladi, Fill qolganini egallaydi
                    foodRow.Controls.Add(lblName);
                    foodRow.Controls.Add(lblPrice);
                    det.Controls.Add(foodRow);
                }

                // 3. TAOMLAR SARLAVHASI
                Panel foodHdr = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = C_Bg };
                foodHdr.Controls.Add(new Label
                {
                    Text = "TAOMLAR", Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = C_Muted,
                    TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0)
                });
                det.Controls.Add(foodHdr);

                // 4. HEADER (eng oxirgi qo'shiladi = vizual eng tepada)
                Panel hdrPan = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = C_White };
                hdrPan.Paint += (s, e) =>
                {
                    using (var br = new SolidBrush(accent))
                        e.Graphics.FillRectangle(br, 0, 0, hdrPan.Width, 4);
                    e.Graphics.DrawLine(new Pen(C_Border), 0, 71, hdrPan.Width, 71);
                };

                // Title va meta absolute — hdrPan ichida
                hdrPan.Controls.Add(new Label
                {
                    Text = $"#{orderId} — {place}",
                    Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = C_Dark,
                    Location = new Point(16, 10), AutoSize = true
                });
                hdrPan.Controls.Add(new Label
                {
                    Text = $"{creat:dd.MM.yyyy  HH:mm}  •  {(string.IsNullOrEmpty(waiter) ? "" : waiter + "  •  ")}{(paid ? "✓ Yopilgan" : "Ochiq")}",
                    Font = new Font("Segoe UI", 8), ForeColor = paid ? C_Green : C_Muted,
                    Location = new Point(16, 40), AutoSize = true
                });

                if (!paid)
                {
                    Button btnOpen = new Button
                    {
                        Text = "Tahrirlash →", Height = 28, AutoSize = true,
                        FlatStyle = FlatStyle.Flat, BackColor = accent, ForeColor = Color.White,
                        Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
                    };
                    btnOpen.FlatAppearance.BorderSize = 0;
                    btnOpen.Location = new Point(hdrPan.Width > 200 ? hdrPan.Width - btnOpen.Width - 16 : 200, 22);
                    hdrPan.Resize += (s2, e2) => btnOpen.Location = new Point(hdrPan.Width - btnOpen.Width - 16, 22);
                    btnOpen.Click += (s2, e2) =>
                    {
                        new AddOrder(0, orderId, Session.Login).ShowDialog();
                        _refreshLeft = 1; RefreshOrderList(); ShowDetailEmpty();
                    };
                    hdrPan.Controls.Add(btnOpen);
                }
                det.Controls.Add(hdrPan);

                det.ResumeLayout();
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
            _multiDlTimer?.Stop();
            _multiDlTimer?.Dispose();
            if (_newOrderHandler != null)
            {
                SyncEngine.NewOrdersArrived -= _newOrderHandler;
                _newOrderHandler = null;
            }
            if (_newOrderToastHandler != null)
            {
                SyncEngine.NewOrderCreated -= _newOrderToastHandler;
                _newOrderToastHandler = null;
            }
            if (_lowStockToastHandler != null)
            {
                StockAlertService.LowStockDetected -= _lowStockToastHandler;
                _lowStockToastHandler = null;
            }
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
