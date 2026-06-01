using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.report
{
    public class ReportPage : UserControl
    {
        // ─── Palette ──────────────────────────────────────────────────
        static readonly Color C_BG     = Color.FromArgb(243, 244, 246);
        static readonly Color C_CARD   = Color.White;
        static readonly Color C_DARK   = Color.FromArgb(17,  24,  39);
        static readonly Color C_BODY   = Color.FromArgb(55,  65,  81);
        static readonly Color C_MUTED  = Color.FromArgb(107, 114, 128);
        static readonly Color C_BORDER = Color.FromArgb(229, 231, 235);
        static readonly Color C_GREEN  = Color.FromArgb(16, 185, 129);
        static readonly Color C_GDARK  = Color.FromArgb(5,  150, 105);
        static readonly Color C_GLIGHT = Color.FromArgb(204, 250, 228);
        static readonly Color C_BLUE   = Color.FromArgb(59,  130, 246);
        static readonly Color C_BLIGHT = Color.FromArgb(219, 234, 254);
        static readonly Color C_ORANGE = Color.FromArgb(249, 115, 22);
        static readonly Color C_OLIGHT = Color.FromArgb(255, 237, 213);
        static readonly Color C_PURPLE = Color.FromArgb(139, 92, 246);
        static readonly Color C_PLIGHT = Color.FromArgb(237, 233, 254);
        static readonly Color C_VIOLET = Color.FromArgb(168, 85, 247);
        static readonly Color C_VLIGHT = Color.FromArgb(243, 232, 255);
        static readonly Color C_RED    = Color.FromArgb(239, 68, 68);
        static readonly Color C_RLIGHT = Color.FromArgb(254, 226, 226);

        DateTime _date = DateTime.Today, _dateFrom = DateTime.Today.AddDays(-6), _dateTo = DateTime.Today;
        bool _range = false;

        DateTime _ovlSavedDate, _ovlSavedFrom, _ovlSavedTo;
        bool _ovlSavedRange;

        Label _lblHeader, _lblOrderSum, _lblOrderCnt, _lblFoodProfit,
              _lblFeeProfit, _lblPlaceProfit, _lblPurchase, _lblProfit;
        Panel _chartPanel;
        Dictionary<DateTime, decimal> _chartPts = new Dictionary<DateTime, decimal>();
        Dictionary<string, Label> _miniLbls = new Dictionary<string, Label>();

        DateTimePicker _dtpSingle, _dtpFrom, _dtpTo;
        Panel _pnlSingle, _pnlRange, _scrollOuter, _statGrid, _profitCard, _miniGrid, _overlay;
        Button _btnSingle, _btnRange;

        const int PAD = 24, CARD_H = 92, CARD_G = 10, MINI_H = 112, MINI_G = 12;

        // ══════════════════════════════════════════════════════════════
        public ReportPage()
        {
            Dock = DockStyle.Fill; BackColor = C_BG; DoubleBuffered = true;
            EnsureColumns();
            BuildUI();
            Load += (s, e) => { LayoutContent(); LoadAll(); };
        }

        static void EnsureColumns()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('food') AND name='original_price')
                                 ALTER TABLE food ADD original_price DECIMAL(18,2) NULL", db.GetCon()).ExecuteNonQuery();
                new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('place_in') AND name='price')
                                 ALTER TABLE place_in ADD price DECIMAL(18,2) NULL", db.GetCon()).ExecuteNonQuery();
                new SqlCommand(@"IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='payment_id' AND is_nullable=0)
                                 ALTER TABLE [order] ALTER COLUMN payment_id INT NULL", db.GetCon()).ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        //  BUILD UI
        // ══════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Panel titleBar = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = C_CARD };
            titleBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
            titleBar.Controls.Add(new Label { Text = "Hisobotlar", Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = C_DARK, AutoSize = true, Location = new Point(PAD, 14) });
            Button btnDaily = GreenBtn("  Kunlik hisobot cheki", 220, 40);
            Button btnPeriod = GreenBtn("  oraliq davr uchun hisobotlar", 295, 40);
            titleBar.Controls.AddRange(new Control[] { btnDaily, btnPeriod });
            titleBar.Resize += (s, e) => { btnPeriod.Location = new Point(titleBar.Width - PAD - btnPeriod.Width, 12); btnDaily.Location = new Point(btnPeriod.Left - 10 - btnDaily.Width, 12); };
            btnDaily.Click += (s, e) => PrintDailyReport();
            btnPeriod.Click += (s, e) => SetMode(true);

            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = C_CARD };
            filterBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);
            _lblHeader = new Label { Text = DateHeader(), Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_DARK, AutoSize = true, Location = new Point(PAD, 18) };
            filterBar.Controls.Add(_lblHeader);
            _btnSingle = ToggleBtn("Sana", 80, 34, true);
            _btnRange = ToggleBtn("Sanalar oralig'i", 155, 34, false);
            _dtpSingle = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Value = _date, Width = 120 };
            _pnlSingle = new Panel { Width = 124, Height = 34, BackColor = Color.Transparent };
            _pnlSingle.Controls.Add(_dtpSingle); _dtpSingle.Location = new Point(0, 1);
            _dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Value = _dateFrom, Width = 115 };
            _dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Value = _dateTo, Width = 115 };
            Label lDash = new Label { Text = "—", AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = C_MUTED, Location = new Point(118, 6) };
            _pnlRange = new Panel { Width = 254, Height = 34, BackColor = Color.Transparent, Visible = false };
            _pnlRange.Controls.AddRange(new Control[] { _dtpFrom, lDash, _dtpTo });
            _dtpFrom.Location = new Point(0, 1); _dtpTo.Location = new Point(138, 1);
            filterBar.Controls.AddRange(new Control[] { _btnSingle, _btnRange, _pnlSingle, _pnlRange });
            filterBar.Resize += (s, e) => LayoutFilterRight(filterBar);
            _btnSingle.Click += (s, e) => SetMode(false); _btnRange.Click += (s, e) => SetMode(true);
            _dtpSingle.ValueChanged += (s, e) => { _date = _dtpSingle.Value.Date; LoadAll(); };
            _dtpFrom.ValueChanged += (s, e) => { _dateFrom = _dtpFrom.Value.Date; LoadAll(); };
            _dtpTo.ValueChanged += (s, e) => { _dateTo = _dtpTo.Value.Date; LoadAll(); };

            _scrollOuter = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, AutoScroll = true };
            _scrollOuter.ClientSizeChanged += (s, e) => LayoutContent();

            _statGrid = new Panel { BackColor = Color.Transparent };
            _lblOrderSum = new Label(); _lblOrderCnt = new Label(); _lblFoodProfit = new Label();
            _lblFeeProfit = new Label(); _lblPlaceProfit = new Label(); _lblPurchase = new Label();
            _statGrid.Controls.AddRange(new Control[]
            {
                StatCard("  Jami buyurtma summasi",        _lblOrderSum,    C_GLIGHT, C_GREEN),
                StatCard("  Jami buyurtmalar",              _lblOrderCnt,    C_BLIGHT, C_BLUE,   count: true),
                StatCard("  Mahsulotlardan sof daromad",    _lblFoodProfit,  C_OLIGHT, C_ORANGE),
                StatCard("  Foiz (%)lardan sof daromad",    _lblFeeProfit,   C_PLIGHT, C_PURPLE),
                StatCard("  Joylar uchun narxdan daromad",  _lblPlaceProfit, C_VLIGHT, C_VIOLET),
                StatCard("  Xaridlar (xarajatlar)",         _lblPurchase,    C_RLIGHT, C_RED),
            });

            _lblProfit = new Label();
            _profitCard = ProfitCard(_lblProfit);
            _chartPanel = new Panel { BackColor = C_CARD };
            _chartPanel.Paint += DrawChart;
            SetRound(_chartPanel, 14);

            _miniGrid = new Panel { BackColor = Color.Transparent };
            foreach (string t in new[] { "Xodimlar","Mahsulotlar","Buyurtmalar","Masalliqlar","To'lovlar","Kategoriyalar","Xaridlar","Bekor qilishlar","Chegirmalar hisoboti","Joylar","Qarzlar","Izohlar" })
            { Label vl = new Label(); _miniLbls[t] = vl; _miniGrid.Controls.Add(MiniCard(t, vl)); }

            _scrollOuter.Controls.AddRange(new Control[] { _statGrid, _profitCard, _chartPanel, _miniGrid });
            Controls.Add(_scrollOuter); Controls.Add(filterBar); Controls.Add(titleBar);
            WireDetailClicks();
        }

        void WireDetailClicks()
        {
            Connect("Xodimlar",            Detail_Xodimlar);
            Connect("Mahsulotlar",          Detail_Mahsulotlar);
            Connect("Buyurtmalar",          Detail_Buyurtmalar);
            Connect("To'lovlar",            Detail_Tolovlar);
            Connect("Kategoriyalar",        Detail_Kategoriyalar);
            Connect("Joylar",              Detail_Joylar);
            Connect("Xaridlar",            Detail_Xaridlar);
            Connect("Bekor qilishlar",     Detail_BekorQilishlar);
            Connect("Masalliqlar",         Detail_Masalliqlar);
            Connect("Chegirmalar hisoboti",Detail_Chegirmalar);
            Connect("Qarzlar",            Detail_Qarzlar);
            Connect("Izohlar",            Detail_Izohlar);
        }

        void Connect(string key, Action handler)
        {
            if (!_miniLbls.ContainsKey(key)) return;
            Panel card = _miniLbls[key].Parent as Panel; if (card == null) return;
            card.Cursor = Cursors.Hand;
            EventHandler h = (s, e) => handler();
            card.Click += h;
            foreach (Control c in card.Controls) { c.Click += h; c.Cursor = Cursors.Hand; }
        }

        // ══════════════════════════════════════════════════════════════
        //  OVERLAY SYSTEM
        // ══════════════════════════════════════════════════════════════
        void OpenOverlay(string title, Action<Panel> populate, params Button[] extraBtns)
        {
            CloseOverlay();

            // Save main page dates so we can restore when overlay closes
            _ovlSavedDate  = _date;
            _ovlSavedFrom  = _dateFrom;
            _ovlSavedTo    = _dateTo;
            _ovlSavedRange = _range;

            // Overlay starts with same date range as main page
            _range = true;
            _dateFrom = _ovlSavedRange ? _ovlSavedFrom : _ovlSavedDate;
            _dateTo   = _ovlSavedRange ? _ovlSavedTo   : _ovlSavedDate;

            _overlay = new Panel { BackColor = C_BG, Location = Point.Empty };
            _overlay.Size = ClientSize;
            this.Resize += SyncOverlay;

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = C_CARD };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);

            Button btnBack = new Button { Text = "←", Width = 40, Height = 40, Location = new Point(16, 11), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = C_DARK, Font = new Font("Segoe UI", 14), Cursor = Cursors.Hand };
            btnBack.FlatAppearance.BorderSize = 1; btnBack.FlatAppearance.BorderColor = C_BORDER;
            btnBack.Click += (s, e) => CloseOverlay();
            hdr.Controls.Add(btnBack);
            hdr.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = C_DARK, AutoSize = true, Location = new Point(68, 18) });

            if (extraBtns != null && extraBtns.Length > 0)
            {
                hdr.Controls.AddRange(extraBtns);
                hdr.Resize += (s, e) => { int rx = hdr.Width - 16; foreach (var b in extraBtns) { b.Location = new Point(rx - b.Width, 11); rx -= b.Width + 10; } };
            }

            // ── Date filter bar ──────────────────────────────────────────
            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = C_CARD };
            filterBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);

            filterBar.Controls.Add(new Label { Text = "Dan:", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = C_MUTED, AutoSize = true, Location = new Point(24, 17) });

            DateTimePicker ovlDtFrom = new DateTimePicker
            {
                Location = new Point(60, 12), Width = 130, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy", Font = new Font("Segoe UI", 9),
                Value = _dateFrom,
                CalendarTitleBackColor = C_GREEN, CalendarTitleForeColor = Color.White,
                CalendarTrailingForeColor = C_MUTED
            };

            filterBar.Controls.Add(new Label { Text = "Gacha:", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = C_MUTED, AutoSize = true, Location = new Point(204, 17) });

            DateTimePicker ovlDtTo = new DateTimePicker
            {
                Location = new Point(252, 12), Width = 130, Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy", Font = new Font("Segoe UI", 9),
                Value = _dateTo,
                CalendarTitleBackColor = C_GREEN, CalendarTitleForeColor = Color.White,
                CalendarTrailingForeColor = C_MUTED
            };

            filterBar.Controls.Add(ovlDtFrom);
            filterBar.Controls.Add(ovlDtTo);

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, AutoScroll = true };

            Action refreshContent = () =>
            {
                _dateFrom = ovlDtFrom.Value.Date;
                _dateTo   = ovlDtTo.Value.Date;
                _range    = true;
                content.Controls.Clear();
                try { populate(content); } catch { }
            };

            ovlDtFrom.ValueChanged += (s, e) => refreshContent();
            ovlDtTo.ValueChanged   += (s, e) => refreshContent();

            _overlay.Controls.Add(content);
            _overlay.Controls.Add(filterBar);
            _overlay.Controls.Add(hdr);
            Controls.Add(_overlay);
            _overlay.BringToFront();

            // Defer populate until after layout is complete so content.Width is valid
            BeginInvoke(new Action(() => { try { populate(content); } catch { } }));
        }

        void SyncOverlay(object s, EventArgs e) { if (_overlay != null) _overlay.Size = ClientSize; }

        void CloseOverlay()
        {
            Resize -= SyncOverlay;
            if (_overlay != null && Controls.Contains(_overlay))
            {
                Controls.Remove(_overlay); _overlay.Dispose(); _overlay = null;
                _date     = _ovlSavedDate;
                _dateFrom = _ovlSavedFrom;
                _dateTo   = _ovlSavedTo;
                _range    = _ovlSavedRange;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SHARED: row builder helpers
        // ══════════════════════════════════════════════════════════════

        // Adds a column header row to content. Returns the panel.
        Panel AddColHeader(Panel content, string[] cols, float[] pct)
        {
            Panel hdr = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 44 };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            Action buildHdr = () =>
            {
                hdr.Controls.Clear();
                int x = 16, w = hdr.Width;
                for (int i = 0; i < cols.Length; i++)
                {
                    int cw = (int)(w * pct[i]);
                    hdr.Controls.Add(new Label { Text = cols[i], Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = C_GREEN, Location = new Point(x, 0), Width = cw, Height = 44, TextAlign = ContentAlignment.MiddleLeft });
                    x += cw;
                }
            };
            buildHdr();
            hdr.Resize += (s, e) => buildHdr();
            hdr.SetBounds(0, 0, content.Width, 44);
            hdr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(hdr);
            content.Resize += (s, e) => hdr.Width = content.Width;
            return hdr;
        }

        // Creates a data row with icon+text columns. build() called immediately.
        Panel AddDataRow(Panel content, ref int y, bool alt, int height, string[] icons, string[] texts, Color[] colors, FontStyle[] styles, float[] pct)
        {
            Panel row = new Panel { BackColor = alt ? Color.FromArgb(249, 250, 251) : C_CARD, Height = height };
            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);

            string[] ico = icons, tx = texts; Color[] col = colors; FontStyle[] sty = styles; float[] p = pct;
            Action build = () =>
            {
                row.Controls.Clear();
                int rw = row.Width > 20 ? row.Width : content.Width; int x = 16;
                for (int i = 0; i < tx.Length; i++)
                {
                    int cw = (int)(rw * p[i]);
                    row.Controls.Add(new Label { Text = (ico[i] + "  " + tx[i]).TrimStart(), Font = new Font("Segoe UI", 10, sty[i]), ForeColor = col[i], Location = new Point(x, 0), Width = cw, Height = row.Height, TextAlign = ContentAlignment.MiddleLeft });
                    x += cw;
                }
            };

            row.SetBounds(0, y, content.Width, height);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(row);
            content.Resize += (s, e) => { row.Width = content.Width; build(); };
            build(); // populate immediately
            y += height;
            return row;
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: XODIMLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Xodimlar()
        {
            OpenOverlay("Xodimlar", content =>
            {
                AddColHeader(content, new[] { "Xodim", "Lavozim", "Buyurtmalar", "Daromad", "" },
                    new float[] { .22f, .18f, .20f, .28f, .12f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT u.id, u.name, ISNULL(uc.name,'—') as role,
                               COUNT(o.id) as cnt, ISNULL(SUM(o.total),0) as total
                        FROM [user] u
                        LEFT JOIN user_category uc ON uc.id=u.user_category_id
                        LEFT JOIN [order] o ON o.user_id=u.id AND o.paid='YES'
                            AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY u.id, u.name, uc.name ORDER BY total DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int id, string name, string role, int cnt, decimal total)>();
                    while (dr.Read()) rows.Add((Convert.ToInt32(dr["id"]), dr["name"].ToString(), dr["role"].ToString(), Convert.ToInt32(dr["cnt"]), Convert.ToDecimal(dr["total"])));
                    dr.Close(); db.CloseCon();

                    int y = 44; bool alt = false;
                    foreach (var r in rows)
                    {
                        int uid = r.id; string uname = r.name, urole = r.role;
                        int ucnt = r.cnt; decimal utotal = r.total;
                        bool a = alt;

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 56 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);

                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.22f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.22f),(int)(rw*.14f) };
                            row.Controls.Add(RowLbl("👤  " + uname, x, ws[0], C_DARK, FontStyle.Bold, row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl("💼  " + urole, x, ws[1], C_MUTED, FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("🛒  Buyurtmalar: " + ucnt, x, ws[2], C_BODY, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("💰  " + Fmt(utotal), x, ws[3], C_GREEN, FontStyle.Bold, row.Height)); x += ws[3];
                            Button btn = new Button { Text = "Batafsil →", Width = ws[4] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_XodimBatafsil(uid, uname);
                            row.Controls.Add(btn);
                        };

                        row.SetBounds(0, y, content.Width, 56);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row);
                        content.Resize += (s, e) => { row.Width = content.Width; build(); };
                        build();
                        y += 56; alt = !alt;
                    }
                }
                catch { db.CloseCon(); }
            }, GreenBtn("  Saqlab olish", 160, 40));
        }

        void Detail_XodimBatafsil(int uid, string uname)
        {
            OpenOverlay(uname + " — buyurtmalari", content =>
            {
                AddColHeader(content, new[] { "#", "Sana va Vaqt", "Joy", "To'lov", "Summa", "" },
                    new float[] { .08f, .22f, .20f, .18f, .20f, .12f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT o.id, o.created_at, o.total,
                               ISNULL(p.name,'—') as pay,
                               ISNULL(pi.room_name,'—') as place
                        FROM [order] o
                        LEFT JOIN payment p ON p.id=o.payment_id
                        LEFT JOIN place_in pi ON pi.id=o.place_id
                        WHERE o.user_id=@uid AND o.paid='YES'
                          AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        ORDER BY o.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@uid", uid);
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (dr.Read())
                    {
                        int  rawId = Convert.ToInt32(dr["id"]);
                        DateTime created = Convert.ToDateTime(dr["created_at"]);
                        string pay = dr["pay"].ToString(), place = dr["place"].ToString();
                        decimal tot = Convert.ToDecimal(dr["total"]);
                        bool a = alt;
                        int oid = rawId; string py = pay, pl = place; decimal t = tot;
                        string dtStr = created.ToString("dd.MM.yy  HH:mm");
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            for (int ci = row.Controls.Count - 1; ci >= 0; ci--)
                                if (row.Controls[ci] is Label) row.Controls.RemoveAt(ci);
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.08f),(int)(rw*.22f),(int)(rw*.20f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.12f) };
                            row.Controls.Add(RowLbl("#" + oid, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl("📅  " + dtStr, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("🪑  " + pl,    x, ws[2], C_BODY,  FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("💳  " + py,    x, ws[3], C_MUTED, FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("💰  " + Fmt(t),x, ws[4], C_GREEN, FontStyle.Bold,    row.Height)); x += ws[4];
                            Button btn = new Button { Text = "→", Width = ws[5] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_XodimOrder(oid, "#" + oid + "  " + dtStr);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    dr.Close(); db.CloseCon();
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_XodimOrder(int orderId, string title)
        {
            OpenOverlay(title, content =>
            {
                AddColHeader(content, new[] { "Taom nomi", "Miqdor", "Narxi", "Jami" },
                    new float[] { .45f, .18f, .20f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var metaCmd = new SqlCommand(@"
                        SELECT o.created_at, ISNULL(o.total,0) AS total,
                               ISNULL(o.discount_amount,0) AS disc,
                               ISNULL(u.name,'—') AS waiter,
                               ISNULL(p.name,'—') AS pay,
                               ISNULL(pi.room_name,'—') AS place,
                               ISNULL(po.name,'') AS zone,
                               ISNULL(o.order_note,'') AS note
                        FROM [order] o
                        LEFT JOIN [user]    u  ON u.id  = o.user_id
                        LEFT JOIN payment   p  ON p.id  = o.payment_id
                        LEFT JOIN place_in  pi ON pi.id = o.place_id
                        LEFT JOIN place_out po ON po.id = pi.place_out_id
                        WHERE o.id = @oid", db.GetCon());
                    metaCmd.Parameters.AddWithValue("@oid", orderId);
                    var mdr = metaCmd.ExecuteReader();
                    DateTime created = DateTime.Now; decimal oTotal = 0, discAmt = 0;
                    string waiter = "—", pay = "—", place = "—", zone = "", note = "";
                    if (mdr.Read())
                    {
                        created  = Convert.ToDateTime(mdr["created_at"]);
                        oTotal   = Convert.ToDecimal(mdr["total"]);
                        discAmt  = Convert.ToDecimal(mdr["disc"]);
                        waiter   = mdr["waiter"].ToString();
                        pay      = mdr["pay"].ToString();
                        place    = mdr["place"].ToString();
                        zone     = mdr["zone"].ToString();
                        note     = mdr["note"].ToString();
                    }
                    mdr.Close();
                    try
                    {
                        var payParts = new List<string>();
                        using (var pc = new SqlCommand("SELECT p.name, op.amount FROM order_payments op JOIN payment p ON p.id=op.payment_id WHERE op.order_id=@oid ORDER BY op.id", db.GetCon()))
                        {
                            pc.Parameters.AddWithValue("@oid", orderId);
                            using (var pdr = pc.ExecuteReader())
                                while (pdr.Read()) payParts.Add(pdr["name"] + ": " + Fmt(Convert.ToDecimal(pdr["amount"])));
                        }
                        if (payParts.Count > 0) pay = string.Join("  /  ", payParts);
                    }
                    catch { }

                    var itemCmd = new SqlCommand(@"
                        SELECT f.name, ofd.quantity, f.selling_price,
                               ofd.quantity * f.selling_price AS subtotal
                        FROM order_food ofd
                        JOIN food f ON f.id = ofd.food_id
                        WHERE ofd.order_id = @oid
                        ORDER BY f.name", db.GetCon());
                    itemCmd.Parameters.AddWithValue("@oid", orderId);
                    var idr = itemCmd.ExecuteReader();
                    int y = 44; bool alt = false; decimal itemsTotal = 0;
                    while (idr.Read())
                    {
                        string fname = idr["name"].ToString();
                        int qty = Convert.ToInt32(idr["quantity"]);
                        decimal price = Convert.ToDecimal(idr["selling_price"]), sub = Convert.ToDecimal(idr["subtotal"]);
                        itemsTotal += sub;
                        bool a = alt;
                        string fn = fname, qs = qty + " ta", ps = Fmt(price), ss = Fmt(sub);
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 48 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.45f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(qs, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(ps, x, ws[2], C_MUTED, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(ss, x, ws[3], C_GREEN, FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 48); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 48; alt = !alt;
                    }
                    idr.Close(); db.CloseCon();

                    // Info footer
                    var lines = new List<(string l, string v, Color c)>();
                    lines.Add(("Sana va vaqt:", created.ToString("dd.MM.yyyy  HH:mm"), C_BODY));
                    lines.Add(("Joy:", (!string.IsNullOrEmpty(zone) ? zone + " / " : "") + place, C_BODY));
                    lines.Add(("Ofitsiant:", waiter, C_BODY));
                    lines.Add(("To'lov turi:", pay, C_BODY));
                    if (discAmt > 0) lines.Add(("Chegirma:", "−" + Fmt(discAmt), C_RED));
                    if (!string.IsNullOrEmpty(note)) lines.Add(("Izoh:", note, C_MUTED));
                    lines.Add(("JAMI:", Fmt(oTotal), C_GREEN));

                    int fy = y + 12;
                    int infoW = Math.Min(520, content.Width - 32);
                    foreach (var (l, v, c) in lines)
                    {
                        Panel fp2 = new Panel { BackColor = C_CARD, Height = 32 };
                        fp2.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, fp2.Height - 1, fp2.Width, fp2.Height - 1);
                        fp2.Controls.Add(new Label { Text = l, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = C_MUTED, AutoSize = false, Width = 140, Height = 32, TextAlign = ContentAlignment.MiddleLeft, Location = new Point(0, 0) });
                        fp2.Controls.Add(new Label { Text = v, Font = new Font("Segoe UI", 9, l == "JAMI:" ? FontStyle.Bold : FontStyle.Regular), ForeColor = c, AutoSize = false, Width = infoW - 144, Height = 32, TextAlign = ContentAlignment.MiddleLeft, Location = new Point(144, 0) });
                        fp2.SetBounds(16, fy, infoW, 32); fp2.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                        content.Controls.Add(fp2);
                        fy += 32;
                    }
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: MAHSULOTLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Mahsulotlar()
        {
            OpenOverlay("Mahsulotlar", content =>
            {
                AddColHeader(content, new[] { "Mahsulot nomi", "Narx", "Tannarx", "Sotuv miqdori", "Umumiy summa", "Xarajat", "Foyda" },
                    new float[] { .22f, .11f, .11f, .13f, .14f, .14f, .15f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT f.name, f.selling_price, ISNULL(f.original_price,0) AS original_price,
                               SUM(ofd.quantity) as qty,
                               SUM(ofd.quantity*f.selling_price) as revenue,
                               SUM(ofd.quantity*ISNULL(f.original_price,0)) as cost,
                               SUM(ofd.quantity*(f.selling_price-ISNULL(f.original_price,0))) as profit
                        FROM food f
                        JOIN order_food ofd ON ofd.food_id=f.id
                        JOIN [order] o ON o.id=ofd.order_id
                        WHERE o.paid='YES' AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY f.id,f.name,f.selling_price,f.original_price
                        ORDER BY revenue DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (dr.Read())
                    {
                        string fn = dr["name"].ToString();
                        decimal narx = Convert.ToDecimal(dr["selling_price"]), tannarx = Convert.ToDecimal(dr["original_price"]);
                        int qty = Convert.ToInt32(dr["qty"]);
                        decimal rev = Convert.ToDecimal(dr["revenue"]), cost = Convert.ToDecimal(dr["cost"]), profit = Convert.ToDecimal(dr["profit"]);
                        bool a = alt, pos = profit >= 0;
                        string f = fn, nr = Fmt(narx), tn = Fmt(tannarx), qt = qty + " dona", rv = Fmt(rev), cs = Fmt(cost), pf = Fmt(profit);
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.22f),(int)(rw*.11f),(int)(rw*.11f),(int)(rw*.13f),(int)(rw*.14f),(int)(rw*.14f),(int)(rw*.15f) };
                            row.Controls.Add(RowLbl(f,  x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(nr, x, ws[1], C_BODY,   FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(tn, x, ws[2], C_ORANGE, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(qt, x, ws[3], C_BODY,   FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(rv, x, ws[4], C_DARK,   FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(cs, x, ws[5], C_RED,    FontStyle.Regular, row.Height)); x += ws[5];
                            row.Controls.Add(RowLbl(pf, x, ws[6], pos ? C_GREEN : C_RED, FontStyle.Bold, row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    dr.Close(); db.CloseCon();
                }
                catch { db.CloseCon(); }
            }, GreenBtn("  Saqlab olish", 160, 40));
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: BUYURTMALAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Buyurtmalar()
        {
            OpenOverlay("Buyurtmalar", content =>
            {
                AddColHeader(content, new[] { "Sana", "Hafta kuni", "Buyurtmalar", "Jami summa", "" },
                    new float[] { .24f, .20f, .20f, .22f, .14f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT CAST(created_at AS DATE) as dt, COUNT(*) as cnt, ISNULL(SUM(total),0) as tot
                        FROM [order] WHERE paid='YES'
                          AND CAST(created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY CAST(created_at AS DATE) ORDER BY dt DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (dr.Read())
                    {
                        DateTime dt = Convert.ToDateTime(dr["dt"]);
                        int cnt = Convert.ToInt32(dr["cnt"]); decimal tot = Convert.ToDecimal(dr["tot"]);
                        bool a = alt;
                        DateTime dDate = dt;
                        string ds = dt.ToString("d-MMMM, yyyy"), dn = DayNameUz(dt.DayOfWeek),
                               cn = "🛒  " + cnt + " ta", ts = Fmt(tot);
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 56 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            for (int ci = row.Controls.Count - 1; ci >= 0; ci--)
                                if (row.Controls[ci] is Label) row.Controls.RemoveAt(ci);
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.24f),(int)(rw*.20f),(int)(rw*.20f),(int)(rw*.22f),(int)(rw*.14f) };
                            row.Controls.Add(RowLbl("📅  " + ds, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl("📆  " + dn, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cn,           x, ws[2], C_BODY,  FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("💰  " + ts,  x, ws[3], C_GREEN, FontStyle.Bold,    row.Height)); x += ws[3];
                            Button btn = new Button { Text = "→", Width = ws[4] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_KunlikBuyurtmalar(dDate, ds);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 56); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 56; alt = !alt;
                    }
                    dr.Close(); db.CloseCon();
                }
                catch { db.CloseCon(); }
            }, GreenBtn("  Saqlab olish", 160, 40));
        }

        void Detail_KunlikBuyurtmalar(DateTime date, string dateLabel)
        {
            OpenOverlay(dateLabel + " — buyurtmalar", content =>
            {
                AddColHeader(content, new[] { "#", "Vaqt", "Joy", "Ofitsiant", "To'lov", "Summa", "" },
                    new float[] { .07f, .13f, .16f, .17f, .14f, .18f, .15f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT o.id, o.created_at, o.total,
                               ISNULL(p.name,'—') AS pay,
                               ISNULL(pi.room_name,'—') AS place,
                               ISNULL(po.name,'') AS zone,
                               ISNULL(u.name,'—') AS waiter,
                               (SELECT COUNT(*) FROM order_food WHERE order_id=o.id) AS items
                        FROM [order] o
                        LEFT JOIN payment   p  ON p.id  = o.payment_id
                        LEFT JOIN place_in  pi ON pi.id = o.place_id
                        LEFT JOIN place_out po ON po.id = pi.place_out_id
                        LEFT JOIN [user]    u  ON u.id  = o.user_id
                        WHERE o.paid='YES' AND CAST(o.created_at AS DATE) = @dt
                        ORDER BY o.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@dt", date.Date);
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    decimal grandTotal = 0;
                    while (dr.Read())
                    {
                        int    oid     = Convert.ToInt32(dr["id"]);
                        DateTime created = Convert.ToDateTime(dr["created_at"]);
                        decimal tot    = Convert.ToDecimal(dr["total"]);
                        string  pay    = dr["pay"].ToString();
                        string  place  = dr["place"].ToString();
                        string  zone   = dr["zone"].ToString();
                        string  waiter = dr["waiter"].ToString();
                        int     items  = Convert.ToInt32(dr["items"]);
                        grandTotal += tot;
                        bool a = alt;
                        int _oid = oid; string _pay = pay, _waiter = waiter;
                        string _place = (!string.IsNullOrEmpty(zone) ? zone + "/" : "") + place;
                        string _time = created.ToString("HH:mm"), _tot = Fmt(tot);
                        string _items = items + " taom";
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            for (int ci = row.Controls.Count - 1; ci >= 0; ci--)
                                if (row.Controls[ci] is Label) row.Controls.RemoveAt(ci);
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.07f),(int)(rw*.13f),(int)(rw*.16f),(int)(rw*.17f),(int)(rw*.14f),(int)(rw*.18f),(int)(rw*.15f) };
                            row.Controls.Add(RowLbl("#" + _oid,   x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl("🕐 " + _time, x, ws[1], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("🪑 " + _place,x, ws[2], C_BODY,   FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("👤 " + _waiter,x,ws[3], C_BODY,   FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("💳 " + _pay,  x, ws[4], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl("💰 " + _tot,  x, ws[5], C_GREEN,  FontStyle.Bold,    row.Height)); x += ws[5];
                            Button btn = new Button { Text = "→", Width = ws[6] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_XodimOrder(_oid, "#" + _oid + "  " + _time + "  " + _place);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    dr.Close();

                    // JAMI footer
                    Panel footer = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 48 };
                    footer.Paint += (s, e) =>
                    {
                        e.Graphics.DrawLine(new Pen(C_GREEN), 0, 0, footer.Width, 0);
                        e.Graphics.DrawLine(new Pen(C_BORDER), 0, footer.Height - 1, footer.Width, footer.Height - 1);
                    };
                    string gt = grandTotal.ToString("N0");
                    Action fbuild = () =>
                    {
                        footer.Controls.Clear(); int rw = footer.Width > 20 ? footer.Width : content.Width;
                        footer.Controls.Add(new Label { Text = "JAMI:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_MUTED, Location = new Point(rw - 260, 0), Width = 100, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                        footer.Controls.Add(new Label { Text = gt + " so'm", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_GREEN, Location = new Point(rw - 155, 0), Width = 140, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                    };
                    footer.SetBounds(0, y, content.Width, 48); footer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(footer); content.Resize += (s, e) => { footer.Width = content.Width; fbuild(); }; fbuild();

                    db.CloseCon();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: TO'LOVLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Tolovlar()
        {
            OpenOverlay("To'lovlar", content =>
            {
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT p.name,
                            ISNULL((SELECT SUM(op.amount) FROM order_payments op
                                    JOIN [order] o2 ON o2.id=op.order_id AND o2.paid='YES'
                                    AND CAST(o2.created_at AS DATE) BETWEEN @d1 AND @d2
                                    WHERE op.payment_id=p.id), 0) +
                            ISNULL((SELECT SUM(o3.total) FROM [order] o3
                                    WHERE o3.payment_id=p.id AND o3.paid='YES'
                                    AND CAST(o3.created_at AS DATE) BETWEEN @d1 AND @d2
                                    AND NOT EXISTS(SELECT 1 FROM order_payments op2 WHERE op2.order_id=o3.id)), 0) AS tot
                        FROM payment p
                        ORDER BY tot DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 0; bool alt = false;
                    while (dr.Read())
                    {
                        string pname = dr["name"].ToString(); decimal tot = Convert.ToDecimal(dr["tot"]);
                        bool a = alt, hasVal = tot > 0;
                        string pn = pname, ts = Fmt(tot);
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 56 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width;
                            row.Controls.Add(new Label { Text = pn, Font = new Font("Segoe UI", 12), ForeColor = C_DARK, Location = new Point(24, 0), Width = rw / 2, Height = row.Height, TextAlign = ContentAlignment.MiddleLeft });
                            row.Controls.Add(new Label { Text = ts, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = hasVal ? C_GREEN : C_MUTED, Location = new Point(rw / 2, 0), Width = rw / 2 - 24, Height = row.Height, TextAlign = ContentAlignment.MiddleRight });
                        };
                        row.SetBounds(0, y, content.Width, 56); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 56; alt = !alt;
                    }
                    dr.Close(); db.CloseCon();
                }
                catch { db.CloseCon(); }
            }, GreenBtn("  Saqlab olish", 160, 40));
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: KATEGORIYALAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Kategoriyalar()
        {
            OpenOverlay("Kategoriyalar", content =>
            {
                AddColHeader(content, new[] { "Kategoriya", "Taomlar", "Sotilgan", "Daromad", "" },
                    new float[] { .27f, .18f, .22f, .22f, .11f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT fc.id AS cid, fc.name,
                               COUNT(DISTINCT f.id)                        AS fcnt,
                               ISNULL(SUM(ofd.quantity),0)                 AS qty,
                               ISNULL(SUM(ofd.quantity*f.selling_price),0) AS rev
                        FROM food_category fc
                        LEFT JOIN food f ON f.food_category_id=fc.id
                        LEFT JOIN order_food ofd ON ofd.food_id=f.id
                        LEFT JOIN [order] o ON o.id=ofd.order_id AND o.paid='YES'
                            AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY fc.id,fc.name ORDER BY rev DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int cid, string name, int fcnt, int qty, decimal rev)>();
                    while (dr.Read())
                        rows.Add((Convert.ToInt32(dr["cid"]), dr["name"].ToString(),
                            Convert.ToInt32(dr["fcnt"]), Convert.ToInt32(dr["qty"]),
                            Convert.ToDecimal(dr["rev"])));
                    dr.Close(); db.CloseCon();

                    int y = 44; bool alt = false;
                    foreach (var r in rows)
                    {
                        int    cid = r.cid;  bool a = alt;
                        string c   = r.name, fc2 = r.fcnt + " ta taom",
                               qt  = r.qty + " dona sotilgan", rv = Fmt(r.rev);
                        bool   hasData = r.qty > 0;

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.27f),(int)(rw*.18f),(int)(rw*.22f),(int)(rw*.22f),(int)(rw*.11f) };
                            row.Controls.Add(RowLbl(c,   x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(fc2, x, ws[1], C_BODY,   FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(qt,  x, ws[2], C_PURPLE, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(rv,  x, ws[3], C_GREEN,  FontStyle.Bold,    row.Height)); x += ws[3];
                            Button btn = new Button { Text = "→", Width = ws[4] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = hasData ? C_GREEN : C_BORDER, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = hasData ? Cursors.Hand : Cursors.Default, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            if (hasData) btn.Click += (bs, be) => Detail_KategoriyaFoods(cid, c);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    if (rows.Count == 0) content.Controls.Add(EmptyLabel("Kategoriyalar topilmadi"));
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_KategoriyaFoods(int categoryId, string categoryName)
        {
            OpenOverlay(categoryName + " — taomlar", content =>
            {
                AddColHeader(content,
                    new[] { "Taom nomi", "Narxi", "Tannarx", "Sotilgan", "Daromad", "Foyda" },
                    new float[] { .28f, .13f, .13f, .12f, .17f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT f.name,
                               f.selling_price,
                               ISNULL(f.original_price, 0)                                       AS cost_price,
                               ISNULL(SUM(ofd.quantity), 0)                                      AS qty,
                               ISNULL(SUM(ofd.quantity * f.selling_price), 0)                   AS rev,
                               ISNULL(SUM(ofd.quantity * (f.selling_price - ISNULL(f.original_price,0))), 0) AS profit
                        FROM food f
                        LEFT JOIN order_food ofd ON ofd.food_id = f.id
                        LEFT JOIN [order] o ON o.id = ofd.order_id AND o.paid = 'YES'
                            AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        WHERE f.food_category_id = @cid
                        GROUP BY f.id, f.name, f.selling_price, f.original_price
                        ORDER BY rev DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@cid", categoryId);
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(string name, decimal price, decimal cost, int qty, decimal rev, decimal profit)>();
                    while (dr.Read())
                        rows.Add((dr["name"].ToString(),
                            Convert.ToDecimal(dr["selling_price"]),
                            Convert.ToDecimal(dr["cost_price"]),
                            Convert.ToInt32(dr["qty"]),
                            Convert.ToDecimal(dr["rev"]),
                            Convert.ToDecimal(dr["profit"])));
                    dr.Close(); db.CloseCon();

                    int y = 44; bool alt = false;
                    decimal grandRev = 0, grandProfit = 0; int grandQty = 0;
                    foreach (var r in rows)
                    {
                        grandRev    += r.rev;
                        grandProfit += r.profit;
                        grandQty    += r.qty;
                        bool a = alt, pos = r.profit >= 0;
                        string fn = r.name, pr = Fmt(r.price), cp = Fmt(r.cost),
                               qs = r.qty + " dona", rv = Fmt(r.rev), pf = Fmt(r.profit);

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.28f),(int)(rw*.13f),(int)(rw*.13f),(int)(rw*.12f),(int)(rw*.17f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,              FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(pr, x, ws[1], C_BODY,              FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cp, x, ws[2], C_ORANGE,            FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(qs, x, ws[3], C_PURPLE,            FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(rv, x, ws[4], C_GREEN,             FontStyle.Bold,    row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(pf, x, ws[5], pos ? C_GREEN : C_RED, FontStyle.Bold,  row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }

                    if (rows.Count == 0) { content.Controls.Add(EmptyLabel("Bu sanada " + categoryName + " kategoriyasida sotuvlar yo'q")); return; }

                    // Summary footer
                    Panel foot = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 52 };
                    foot.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(C_GREEN), 0, 0, foot.Width, 0); e.Graphics.DrawLine(new Pen(C_BORDER), 0, foot.Height - 1, foot.Width, foot.Height - 1); };
                    string gq = grandQty + " dona", gr = Fmt(grandRev), gp = Fmt(grandProfit);
                    bool gpos = grandProfit >= 0;
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear(); int fw = foot.Width, x = 16;
                        int[] ws = { (int)(fw*.28f),(int)(fw*.13f),(int)(fw*.13f),(int)(fw*.12f),(int)(fw*.17f),(int)(fw*.17f) };
                        foot.Controls.Add(new Label { Text = "JAMI", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_DARK, Location = new Point(x, 0), Width = ws[0], Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft }); x += ws[0];
                        x += ws[1]; x += ws[2];
                        foot.Controls.Add(new Label { Text = gq, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_PURPLE, Location = new Point(x, 0), Width = ws[3], Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft }); x += ws[3];
                        foot.Controls.Add(new Label { Text = gr, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_GREEN,  Location = new Point(x, 0), Width = ws[4], Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft }); x += ws[4];
                        foot.Controls.Add(new Label { Text = gp, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = gpos ? C_GREEN : C_RED, Location = new Point(x, 0), Width = ws[5], Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft });
                    };
                    foot.SetBounds(0, y, content.Width, 52); foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot); content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); }; buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: JOYLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Joylar()
        {
            OpenOverlay("Joylar", content =>
            {
                AddColHeader(content,
                    new[] { "Zona / Joy", "Holat", "Buyurtmalar", "O'rtacha chek", "Daromad", "" },
                    new float[] { .26f, .10f, .12f, .17f, .25f, .10f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT pi.id AS pid, po.name AS zone, pi.room_name, pi.empty,
                               COUNT(o.id)            AS ocnt,
                               ISNULL(SUM(o.total),0) AS rev,
                               ISNULL(AVG(o.total),0) AS avg_rev
                        FROM place_in pi
                        JOIN place_out po ON po.id = pi.place_out_id
                        LEFT JOIN [order] o ON o.place_id = pi.id AND o.paid = 'YES'
                            AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY po.name, pi.id, pi.room_name, pi.empty
                        ORDER BY rev DESC, po.name, pi.room_name", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int pid, string zone, string room, bool isEmpty, int cnt, decimal rev, decimal avg)>();
                    while (dr.Read())
                        rows.Add((Convert.ToInt32(dr["pid"]), dr["zone"].ToString(), dr["room_name"].ToString(),
                            dr["empty"].ToString().ToUpper() == "YES",
                            Convert.ToInt32(dr["ocnt"]), Convert.ToDecimal(dr["rev"]), Convert.ToDecimal(dr["avg_rev"])));
                    dr.Close(); db.CloseCon();

                    decimal maxRev = rows.Count > 0 && rows.Max(r => r.rev) > 0 ? rows.Max(r => r.rev) : 1m;

                    int y = 44; bool alt = false; bool first = true;
                    foreach (var r in rows)
                    {
                        int     pid    = r.pid;
                        bool    a      = alt, isFree = r.isEmpty, isTop = first && r.rev > 0;
                        float   barPct = (float)(r.rev / maxRev);
                        string  place  = r.zone + " / " + r.room;
                        string  holat  = isFree ? "Bo'sh" : "Band";
                        string  cntS   = r.cnt > 0 ? r.cnt + " ta" : "0 ta";
                        string  avgS   = r.cnt > 0 ? Fmt(r.avg) : "—";
                        string  revS   = Fmt(r.rev);
                        first = false;

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        float bp = barPct;
                        row.Paint += (s, e) =>
                        {
                            e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                            if (bp > 0.01f)
                            {
                                int revX = (int)(row.Width * (.26f + .10f + .12f + .17f));
                                int revW = (int)(row.Width * .25f) - 12;
                                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(25, C_GREEN)), revX, row.Height - 4, revW, 3);
                                e.Graphics.FillRectangle(new SolidBrush(C_GREEN), revX, row.Height - 4, (int)(revW * bp), 3);
                            }
                        };

                        bool top = isTop, free = isFree;
                        string pl = place, hl = holat, cs = cntS, avs = avgS, rs = revS;
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.26f),(int)(rw*.10f),(int)(rw*.12f),(int)(rw*.17f),(int)(rw*.25f),(int)(rw*.10f) };
                            string icon = top ? "🏆" : "🪑";
                            row.Controls.Add(RowLbl(icon + "  " + pl, x, ws[0], top ? C_ORANGE : C_DARK, top ? FontStyle.Bold : FontStyle.Regular, row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(hl, x, ws[1], free ? C_GREEN : C_RED, FontStyle.Bold, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cs, x, ws[2], C_BODY, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(avs, x, ws[3], C_MUTED, FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(rs, x, ws[4], top ? C_ORANGE : C_GREEN, FontStyle.Bold, row.Height)); x += ws[4];
                            Button btn = new Button { Text = "→", Width = ws[5] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_JoyOrders(pid, pl);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    if (rows.Count == 0) content.Controls.Add(EmptyLabel("Joylar topilmadi"));
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_JoyOrders(int placeId, string placeName)
        {
            OpenOverlay(placeName + " — buyurtmalar", content =>
            {
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT o.id, o.created_at,
                               ISNULL(u.name,'—') AS waiter,
                               ISNULL(p.name,'—') AS pay,
                               o.total,
                               COUNT(DISTINCT ofd.food_id) AS item_types,
                               ISNULL(SUM(ofd.quantity),0) AS item_qty
                        FROM [order] o
                        LEFT JOIN [user]      u   ON u.id        = o.user_id
                        LEFT JOIN payment     p   ON p.id        = o.payment_id
                        LEFT JOIN order_food  ofd ON ofd.order_id = o.id
                        WHERE o.place_id = @pid AND o.paid = 'YES'
                          AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY o.id, o.created_at, u.name, p.name, o.total
                        ORDER BY o.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@pid", placeId);
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int oid, DateTime dt, string waiter, string pay, decimal total, int types, int qty)>();
                    while (dr.Read())
                        rows.Add((Convert.ToInt32(dr["id"]), Convert.ToDateTime(dr["created_at"]),
                            dr["waiter"].ToString(), dr["pay"].ToString(),
                            Convert.ToDecimal(dr["total"]),
                            Convert.ToInt32(dr["item_types"]), Convert.ToInt32(dr["item_qty"])));
                    dr.Close(); db.CloseCon();

                    // ── Summary stats card ──────────────────────────────────
                    decimal totalRev = rows.Count > 0 ? rows.Sum(r => r.total) : 0;
                    decimal avgRev   = rows.Count > 0 ? totalRev / rows.Count  : 0;
                    decimal maxOrder = rows.Count > 0 ? rows.Max(r => r.total) : 0;

                    Panel sumCard = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 60 };
                    sumCard.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_GREEN), 0, sumCard.Height - 1, sumCard.Width, sumCard.Height - 1);
                    Action buildSum = () =>
                    {
                        sumCard.Controls.Clear();
                        int sw = sumCard.Width / 4;
                        void Stat(string lbl, string val, Color vc, int xp)
                        {
                            sumCard.Controls.Add(new Label { Text = lbl, Font = new Font("Segoe UI", 8), ForeColor = C_GDARK, Location = new Point(xp + 14, 6), AutoSize = true });
                            sumCard.Controls.Add(new Label { Text = val, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = vc, Location = new Point(xp + 14, 26), AutoSize = true });
                        }
                        Stat("Jami buyurtmalar", rows.Count + " ta",  C_GREEN,  0);
                        Stat("Jami daromad",     Fmt(totalRev),        C_GREEN,  sw);
                        Stat("O'rtacha chek",    Fmt(avgRev),          C_GDARK,  sw * 2);
                        Stat("Eng katta chek",   Fmt(maxOrder),        C_ORANGE, sw * 3);
                    };
                    sumCard.SetBounds(0, 0, content.Width, 60);
                    sumCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(sumCard);
                    content.Resize += (s, e) => { sumCard.Width = content.Width; buildSum(); };
                    buildSum();

                    // ── Manual col header at y=60 ──────────────────────────
                    string[] hCols = { "#", "Sana / Vaqt", "Ofitsiant", "Taomlar", "To'lov", "Jami", "" };
                    float[]  hPct  = { .07f, .17f, .18f, .15f, .15f, .19f, .09f };
                    Panel hdrPanel = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 44 };
                    hdrPanel.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, hdrPanel.Height - 1, hdrPanel.Width, hdrPanel.Height - 1);
                    Action buildHdr = () =>
                    {
                        hdrPanel.Controls.Clear();
                        int hw = hdrPanel.Width, hx = 16;
                        for (int i = 0; i < hCols.Length; i++)
                        {
                            int cw = (int)(hw * hPct[i]);
                            hdrPanel.Controls.Add(new Label { Text = hCols[i], Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = C_GREEN, Location = new Point(hx, 0), Width = cw, Height = 44, TextAlign = ContentAlignment.MiddleLeft });
                            hx += cw;
                        }
                    };
                    hdrPanel.SetBounds(0, 60, content.Width, 44);
                    hdrPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(hdrPanel);
                    content.Resize += (s, e) => { hdrPanel.Width = content.Width; buildHdr(); };
                    buildHdr();

                    // ── Order rows ─────────────────────────────────────────
                    int y = 104; bool alt = false;
                    foreach (var r in rows)
                    {
                        int  oid    = r.oid; bool a = alt;
                        DateTime dt = r.dt;
                        string cOid  = "#" + r.oid;
                        string cDt   = r.dt.ToString("dd.MM  HH:mm");
                        string cWait = r.waiter;
                        string cItem = r.types + " xil, " + r.qty + " ta";
                        string cPay  = r.pay;
                        string cTot  = Fmt(r.total);

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.07f),(int)(rw*.17f),(int)(rw*.18f),(int)(rw*.15f),(int)(rw*.15f),(int)(rw*.19f),(int)(rw*.09f) };
                            row.Controls.Add(RowLbl(cOid,         x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(cDt,          x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("👤 " + cWait,x, ws[2], C_BODY,  FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(cItem,        x, ws[3], C_MUTED, FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(cPay,         x, ws[4], C_MUTED, FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(cTot,         x, ws[5], C_GREEN, FontStyle.Bold,    row.Height)); x += ws[5];
                            Button btn = new Button { Text = "→", Width = ws[6] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_JoyOrderItems(oid, "#" + oid + "  " + dt.ToString("dd.MM HH:mm"));
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    if (rows.Count == 0) content.Controls.Add(EmptyLabel("Bu sanada " + placeName + " da buyurtmalar yo'q"));
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_JoyOrderItems(int orderId, string header)
        {
            OpenOverlay("Buyurtma " + header, content =>
            {
                AddColHeader(content, new[] { "Taom nomi", "Miqdor", "Narxi", "Jami" },
                    new float[] { .45f, .18f, .20f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var metaCmd = new SqlCommand(@"
                        SELECT o.created_at, ISNULL(o.total,0) AS total,
                               ISNULL(o.discount_amount,0) AS disc,
                               ISNULL(u.name,'—')          AS waiter,
                               ISNULL(p.name,'—')          AS pay,
                               ISNULL(pi.room_name,'—')    AS place,
                               ISNULL(po.name,'')          AS zone,
                               ISNULL(o.order_note,'')     AS note,
                               ISNULL(o.customer_name,'') AS cust
                        FROM [order] o
                        LEFT JOIN [user]    u  ON u.id  = o.user_id
                        LEFT JOIN payment   p  ON p.id  = o.payment_id
                        LEFT JOIN place_in  pi ON pi.id = o.place_id
                        LEFT JOIN place_out po ON po.id = pi.place_out_id
                        WHERE o.id = @oid", db.GetCon());
                    metaCmd.Parameters.AddWithValue("@oid", orderId);
                    var mdr = metaCmd.ExecuteReader();
                    DateTime created = DateTime.Now; decimal oTotal = 0, discAmt = 0;
                    string waiter = "—", pay = "—", place = "—", zone = "", note = "", cust = "";
                    if (mdr.Read())
                    {
                        created = Convert.ToDateTime(mdr["created_at"]); oTotal = Convert.ToDecimal(mdr["total"]);
                        discAmt = Convert.ToDecimal(mdr["disc"]); waiter = mdr["waiter"].ToString();
                        pay     = mdr["pay"].ToString();    place  = mdr["place"].ToString();
                        zone    = mdr["zone"].ToString();   note   = mdr["note"].ToString();
                        cust    = mdr["cust"].ToString();
                    }
                    mdr.Close();

                    var itemCmd = new SqlCommand(@"
                        SELECT f.name, ofd.quantity, f.selling_price,
                               ofd.quantity * f.selling_price AS subtotal
                        FROM order_food ofd JOIN food f ON f.id = ofd.food_id
                        WHERE ofd.order_id = @oid ORDER BY f.name", db.GetCon());
                    itemCmd.Parameters.AddWithValue("@oid", orderId);
                    var idr = itemCmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (idr.Read())
                    {
                        string fname = idr["name"].ToString();
                        int    qty   = Convert.ToInt32(idr["quantity"]);
                        decimal price = Convert.ToDecimal(idr["selling_price"]);
                        decimal sub   = Convert.ToDecimal(idr["subtotal"]);
                        bool a = alt;
                        string fn = fname, qs = qty + " ta", ps = Fmt(price), ss = Fmt(sub);

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 48 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.45f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(qs, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(ps, x, ws[2], C_MUTED, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(ss, x, ws[3], C_GREEN, FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 48); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 48; alt = !alt;
                    }
                    idr.Close(); db.CloseCon();

                    var lines = new List<(string l, string v, Color c)>();
                    lines.Add(("Sana:",     created.ToString("dd.MM.yyyy  HH:mm"),  C_BODY));
                    if (!string.IsNullOrEmpty(cust)) lines.Add(("Mijoz:", cust, C_BODY));
                    lines.Add(("Joy:",      (!string.IsNullOrEmpty(zone) ? zone + " / " : "") + place, C_BODY));
                    lines.Add(("Ofitsiant:",waiter, C_BODY));
                    lines.Add(("To'lov:",  pay,     C_BODY));
                    if (discAmt > 0) lines.Add(("Chegirma:", "−" + Fmt(discAmt), C_RED));
                    lines.Add(("Jami:",    Fmt(oTotal), C_GREEN));
                    if (!string.IsNullOrEmpty(note)) lines.Add(("Izoh:", note, C_MUTED));

                    Panel foot = new Panel { BackColor = Color.FromArgb(249, 249, 251), Height = lines.Count * 36 + 8 };
                    foot.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, 0, foot.Width, 0);
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear(); int fy = 8;
                        foreach (var ln in lines)
                        {
                            foot.Controls.Add(new Label { Text = ln.l, Font = new Font("Segoe UI", 10), ForeColor = C_MUTED,     Location = new Point(24,  fy), AutoSize = true });
                            foot.Controls.Add(new Label { Text = ln.v, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ln.c, Location = new Point(200, fy), AutoSize = true });
                            fy += 36;
                        }
                    };
                    foot.SetBounds(0, y, content.Width, foot.Height); foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot); content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); }; buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: XARIDLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Xaridlar()
        {
            OpenOverlay("Xaridlar", content =>
            {
                AddColHeader(content, new[] { "Nomi", "Turi", "Sana", "Narxi" }, new float[] { .40f, .14f, .20f, .26f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT i.name, ip.total_price, ip.purchased_at AS created_at, 'Masalliq' AS tip
                        FROM ingredient_purchase ip
                        JOIN ingredient i ON i.id = ip.ingredient_id
                        WHERE CAST(ip.purchased_at AS DATE) BETWEEN @d1 AND @d2
                        UNION ALL
                        SELECT f.name, fp.total_price, fp.purchased_at, 'Mahsulot'
                        FROM food_purchase fp
                        JOIN food f ON f.id = fp.food_id
                        WHERE CAST(fp.purchased_at AS DATE) BETWEEN @d1 AND @d2
                        ORDER BY created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false; decimal grandTotal = 0;
                    while (dr.Read())
                    {
                        string pn = dr["name"].ToString(), dt = Convert.ToDateTime(dr["created_at"]).ToString("dd.MM.yyyy");
                        string tip = dr["tip"].ToString();
                        decimal tot = Convert.ToDecimal(dr["total_price"]); bool a = alt;
                        grandTotal += tot;
                        string p = pn, d = dt, ts = Fmt(tot), tp = tip;
                        bool isMahsulot = tp == "Mahsulot";
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.40f),(int)(rw*.14f),(int)(rw*.20f),(int)(rw*.26f) };
                            row.Controls.Add(RowLbl((isMahsulot ? "🍽  " : "🛍  ") + p, x, ws[0], C_DARK, FontStyle.Bold, row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(tp,         x, ws[1], isMahsulot ? C_BLUE : C_ORANGE, FontStyle.Bold,    row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("📅  " + d, x, ws[2], C_MUTED,                        FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("💰  " + ts,x, ws[3], C_RED,                           FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    dr.Close();

                    if (y == 44) { db.CloseCon(); content.Controls.Add(EmptyLabel("Bu sanada xaridlar yo'q")); return; }

                    // JAMI footer
                    string gtStr = grandTotal.ToString("N0");
                    Panel footer = new Panel { BackColor = Color.FromArgb(255, 242, 242), Height = 48 };
                    footer.Paint += (s, e) =>
                    {
                        e.Graphics.DrawLine(new Pen(C_RED), 0, 0, footer.Width, 0);
                        e.Graphics.DrawLine(new Pen(C_BORDER), 0, footer.Height - 1, footer.Width, footer.Height - 1);
                    };
                    Action fbuild = () =>
                    {
                        footer.Controls.Clear(); int rw = footer.Width > 20 ? footer.Width : content.Width;
                        footer.Controls.Add(new Label { Text = "JAMI xarajat:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_MUTED, Location = new Point(rw - 300, 0), Width = 140, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                        footer.Controls.Add(new Label { Text = gtStr + " so'm", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_RED, Location = new Point(rw - 155, 0), Width = 140, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                    };
                    footer.SetBounds(0, y, content.Width, 48); footer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(footer); content.Resize += (s, e) => { footer.Width = content.Width; fbuild(); }; fbuild();
                    db.CloseCon();
                }
                catch { db.CloseCon(); content.Controls.Add(EmptyLabel("Xaridlar jadvali topilmadi")); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: BEKOR QILISHLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_BekorQilishlar()
        {
            OpenOverlay("Bekor qilishlar", content =>
            {
                AddColHeader(content, new[] { "#", "Sana", "Vaqt", "Ofitsiant", "Joy", "Bekor taomlar", "" },
                    new float[] { .07f, .13f, .08f, .19f, .16f, .28f, .09f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    // Read from cancellation log, grouped by order
                    var cmd = new SqlCommand(@"
                        SELECT cl.order_id,
                               CAST(MIN(cl.cancelled_at) AS DATE)    AS dt,
                               CAST(MIN(cl.cancelled_at) AS TIME(0)) AS tm,
                               MIN(cl.cancelled_by)                  AS cancelled_by,
                               ISNULL(pi.room_name,'—')              AS place,
                               ISNULL(po.name,'')                    AS zone,
                               SUM(cl.cancelled_qty)                 AS total_qty,
                               COUNT(DISTINCT cl.food_id)            AS item_cnt
                        FROM order_cancellation_log cl
                        LEFT JOIN [order]   o  ON o.id   = cl.order_id
                        LEFT JOIN place_in  pi ON pi.id  = o.place_id
                        LEFT JOIN place_out po ON po.id  = pi.place_out_id
                        WHERE CAST(cl.cancelled_at AS DATE) BETWEEN @d1 AND @d2
                        GROUP BY cl.order_id, pi.room_name, po.name
                        ORDER BY MIN(cl.cancelled_at) DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int oid, string dt, string tm, string who, string place, string zone, int qty, int cnt)>();
                    while (dr.Read())
                        rows.Add((
                            Convert.ToInt32(dr["order_id"]),
                            Convert.ToDateTime(dr["dt"]).ToString("dd.MM.yyyy"),
                            dr["tm"].ToString().Substring(0, 5),
                            dr["cancelled_by"].ToString(),
                            dr["place"].ToString(),
                            dr["zone"].ToString(),
                            Convert.ToInt32(dr["total_qty"]),
                            Convert.ToInt32(dr["item_cnt"])
                        ));
                    dr.Close(); db.CloseCon();

                    int y = 44; bool alt = false;
                    foreach (var r in rows)
                    {
                        int    oid  = r.oid;
                        bool   a    = alt;
                        string joyTxt = (!string.IsNullOrEmpty(r.zone) ? r.zone + " / " : "") + r.place;
                        string cOid = "#" + r.oid, cDt = r.dt, cTm = r.tm, cWho = r.who;
                        string cJoy = joyTxt, cItems = r.cnt + " xil, " + r.qty + " ta bekor";

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.07f),(int)(rw*.13f),(int)(rw*.08f),(int)(rw*.19f),(int)(rw*.16f),(int)(rw*.28f),(int)(rw*.09f) };
                            row.Controls.Add(RowLbl(cOid,          x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(cDt,           x, ws[1], C_BODY,   FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cTm,           x, ws[2], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("👤  " + cWho, x, ws[3], C_BODY,   FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("🪑  " + cJoy, x, ws[4], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl("⊖  " + cItems,x, ws[5], C_RED,    FontStyle.Bold,    row.Height)); x += ws[5];
                            Button btn = new Button { Text = "→", Width = ws[6] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_RED, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_BekorOrder(oid);
                            row.Controls.Add(btn);
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    if (rows.Count == 0) content.Controls.Add(EmptyLabel("Bu sanada bekor qilingan taomlar yo'q"));
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_BekorOrder(int orderId)
        {
            OpenOverlay("Bekor qilingan taomlar — #" + orderId, content =>
            {
                AddColHeader(content, new[] { "Taom nomi", "Kategoriya", "Bekor miqdori", "Vaqt", "Kim tomonidan" },
                    new float[] { .30f, .20f, .15f, .18f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    // Cancellation log entries for this order
                    var logCmd = new SqlCommand(@"
                        SELECT food_name, food_category, SUM(cancelled_qty) AS qty,
                               MIN(cancelled_at) AS first_at, cancelled_by
                        FROM order_cancellation_log
                        WHERE order_id = @oid
                        GROUP BY food_id, food_name, food_category, cancelled_by
                        ORDER BY MIN(cancelled_at)", db.GetCon());
                    logCmd.Parameters.AddWithValue("@oid", orderId);
                    var ldr = logCmd.ExecuteReader();

                    int y = 44; bool alt = false; bool hasRows = false;
                    while (ldr.Read())
                    {
                        hasRows = true;
                        string fname = ldr["food_name"].ToString();
                        string fcat  = ldr["food_category"].ToString();
                        int    qty   = Convert.ToInt32(ldr["qty"]);
                        string vaqt  = Convert.ToDateTime(ldr["first_at"]).ToString("dd.MM  HH:mm");
                        string who   = ldr["cancelled_by"].ToString();
                        bool a = alt;
                        string fn = fname, fc = fcat, qs = "−" + qty + " ta", vq = vaqt, wh = who;

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(255, 241, 241) : C_CARD, Height = 48 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.30f),(int)(rw*.20f),(int)(rw*.15f),(int)(rw*.18f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(fc, x, ws[1], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(qs, x, ws[2], C_RED,    FontStyle.Bold,    row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(vq, x, ws[3], C_BODY,   FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(wh, x, ws[4], C_PURPLE, FontStyle.Regular, row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 48);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row);
                        content.Resize += (s, e) => { row.Width = content.Width; build(); };
                        build();
                        y += 48; alt = !alt;
                    }
                    ldr.Close();

                    if (!hasRows) { db.CloseCon(); content.Controls.Add(EmptyLabel("Bu buyurtma uchun bekor qilish yozuvlari yo'q")); return; }

                    // Order meta footer
                    var metaCmd = new SqlCommand(@"
                        SELECT o.created_at, ISNULL(o.total,0) AS total,
                               ISNULL(u.name,'—') AS waiter,
                               ISNULL(pi.room_name,'—') AS place, ISNULL(po.name,'') AS zone
                        FROM [order] o
                        LEFT JOIN [user] u ON u.id=o.user_id
                        LEFT JOIN place_in pi ON pi.id=o.place_id
                        LEFT JOIN place_out po ON po.id=pi.place_out_id
                        WHERE o.id=@oid", db.GetCon());
                    metaCmd.Parameters.AddWithValue("@oid", orderId);
                    var mdr = metaCmd.ExecuteReader();
                    string mWaiter = "—", mPlace = "—", mZone = ""; DateTime mCreated = DateTime.Now; decimal mTotal = 0;
                    if (mdr.Read()) { mCreated = Convert.ToDateTime(mdr["created_at"]); mTotal = Convert.ToDecimal(mdr["total"]); mWaiter = mdr["waiter"].ToString(); mPlace = mdr["place"].ToString(); mZone = mdr["zone"].ToString(); }
                    mdr.Close(); db.CloseCon();

                    string mJoy = (!string.IsNullOrEmpty(mZone) ? mZone + " / " : "") + mPlace;
                    var lines = new List<(string l, string v, Color c)>
                    {
                        ("Buyurtma yaratilgan:", mCreated.ToString("dd.MM.yyyy  HH:mm"), C_BODY),
                        ("Ofitsiant:", mWaiter, C_BODY),
                        ("Joy:", mJoy, C_BODY),
                        ("Buyurtma jami:", Fmt(mTotal), C_GREEN),
                    };

                    Panel foot = new Panel { BackColor = Color.FromArgb(249, 249, 251), Height = lines.Count * 36 + 8 };
                    foot.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, 0, foot.Width, 0);
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear(); int fy = 8;
                        foreach (var ln in lines)
                        {
                            foot.Controls.Add(new Label { Text = ln.l, Font = new Font("Segoe UI", 10), ForeColor = C_MUTED, Location = new Point(24, fy), AutoSize = true });
                            foot.Controls.Add(new Label { Text = ln.v, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ln.c, Location = new Point(200, fy), AutoSize = true });
                            fy += 36;
                        }
                    };
                    foot.SetBounds(0, y, content.Width, foot.Height);
                    foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot);
                    content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); };
                    buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: CHEGIRMALAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Chegirmalar()
        {
            OpenOverlay("Chegirmalar hisoboti", content =>
            {
                AddColHeader(content,
                    new[] { "#", "Sana", "Vaqt", "Ofitsiant", "Joy", "Chegirma%", "Chegirma", "Jami", "" },
                    new float[] { .07f, .12f, .09f, .16f, .13f, .09f, .13f, .13f, .08f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT o.id,
                               CAST(o.created_at AS DATE)           AS dt,
                               CAST(o.created_at AS TIME(0))        AS tm,
                               ISNULL(u.name,'—')                   AS waiter,
                               ISNULL(pi.room_name,'—')             AS place,
                               ISNULL(o.discount_pct,0)             AS disc_pct,
                               ISNULL(o.discount_amount,0)          AS disc_amt,
                               ISNULL(o.total,0)                    AS total
                        FROM [order] o
                        LEFT JOIN [user]     u  ON u.id  = o.user_id
                        LEFT JOIN place_in   pi ON pi.id = o.place_id
                        WHERE o.discount_amount > 0
                          AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        ORDER BY o.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());

                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int id, string dt, string tm, string waiter, string place, decimal discPct, decimal discAmt, decimal total)>();
                    while (dr.Read())
                        rows.Add((
                            Convert.ToInt32(dr["id"]),
                            Convert.ToDateTime(dr["dt"]).ToString("dd.MM.yyyy"),
                            dr["tm"].ToString().Substring(0, 5),
                            dr["waiter"].ToString(),
                            dr["place"].ToString(),
                            Convert.ToDecimal(dr["disc_pct"]),
                            Convert.ToDecimal(dr["disc_amt"]),
                            Convert.ToDecimal(dr["total"])
                        ));
                    dr.Close(); db.CloseCon();

                    int y = 44; bool alt = false;
                    decimal grandDisc = 0;
                    foreach (var r in rows)
                    {
                        grandDisc += r.discAmt;
                        int    oid  = r.id;
                        string hdr  = "#" + r.id + "  " + r.dt + "  " + r.waiter;
                        bool   a    = alt;

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);

                        string cOid = "#" + r.id, cDt = r.dt, cTm = r.tm, cWt = r.waiter, cPl = r.place;
                        string cPct = r.discPct > 0 ? r.discPct.ToString("N1") + "%" : "—";
                        string cDa  = Fmt(r.discAmt), cTot = Fmt(r.total);

                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = {
                                (int)(rw*.07f),(int)(rw*.12f),(int)(rw*.09f),(int)(rw*.16f),
                                (int)(rw*.13f),(int)(rw*.09f),(int)(rw*.13f),(int)(rw*.13f),(int)(rw*.08f)
                            };
                            row.Controls.Add(RowLbl(cOid, x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(cDt,  x, ws[1], C_BODY,   FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cTm,  x, ws[2], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("👤  " + cWt, x, ws[3], C_BODY,  FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("🪑  " + cPl, x, ws[4], C_BODY,  FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(cPct,  x, ws[5], C_ORANGE, FontStyle.Bold,    row.Height)); x += ws[5];
                            row.Controls.Add(RowLbl("−" + cDa, x, ws[6], C_RED,  FontStyle.Bold,    row.Height)); x += ws[6];
                            row.Controls.Add(RowLbl(cTot,  x, ws[7], C_GREEN,  FontStyle.Bold,    row.Height)); x += ws[7];
                            Button btn = new Button { Text = "→", Width = ws[8] - 8, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_BLUE, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(x, (row.Height - 34) / 2) };
                            btn.FlatAppearance.BorderSize = 0;
                            btn.Click += (bs, be) => Detail_ChegirmaOrder(oid, hdr);
                            row.Controls.Add(btn);
                        };

                        row.SetBounds(0, y, content.Width, 52);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row);
                        content.Resize += (s, e) => { row.Width = content.Width; build(); };
                        build();
                        y += 52; alt = !alt;
                    }

                    if (rows.Count == 0)
                    {
                        content.Controls.Add(EmptyLabel("Bu sanada chegirmalar yo'q"));
                        return;
                    }

                    // Totals footer
                    Panel foot = new Panel { BackColor = Color.FromArgb(254, 242, 242), Height = 48 };
                    foot.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(C_RED), 0, 0, foot.Width, 0); e.Graphics.DrawLine(new Pen(C_BORDER), 0, foot.Height - 1, foot.Width, foot.Height - 1); };
                    string totalDisc = grandDisc.ToString("N0") + " UZS";
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear();
                        foot.Controls.Add(new Label { Text = "Jami chegirma: " + totalDisc, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_RED, AutoSize = true, Location = new Point(16, 0), Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft });
                        foot.Controls.Add(new Label { Text = rows.Count + " ta buyurtma", Font = new Font("Segoe UI", 10), ForeColor = C_MUTED, Height = foot.Height, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Width = 180, Left = foot.Width - 196 });
                    };
                    foot.SetBounds(0, y, content.Width, 48);
                    foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot);
                    content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); };
                    buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        void Detail_ChegirmaOrder(int orderId, string header)
        {
            OpenOverlay("Buyurtma " + header, content =>
            {
                AddColHeader(content, new[] { "Taom nomi", "Miqdor", "Narxi", "Jami" }, new float[] { .45f, .18f, .20f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    // Order meta
                    var metaCmd = new SqlCommand(@"
                        SELECT ISNULL(o.total,0) AS total,
                               ISNULL(o.discount_pct,0)    AS disc_pct,
                               ISNULL(o.discount_amount,0) AS disc_amt,
                               ISNULL(o.customer_name,'')  AS cust,
                               ISNULL(o.order_note,'')     AS note,
                               ISNULL(p.name,'—')          AS pay,
                               ISNULL(pi.room_name,'—')    AS place
                        FROM [order] o
                        LEFT JOIN payment  p  ON p.id  = o.payment_id
                        LEFT JOIN place_in pi ON pi.id = o.place_id
                        WHERE o.id = @oid", db.GetCon());
                    metaCmd.Parameters.AddWithValue("@oid", orderId);
                    var mdr = metaCmd.ExecuteReader();
                    decimal oTotal = 0, dPct = 0, dAmt = 0; string cust = "", note = "", pay = "—", place = "—";
                    if (mdr.Read())
                    {
                        oTotal = Convert.ToDecimal(mdr["total"]); dPct = Convert.ToDecimal(mdr["disc_pct"]); dAmt = Convert.ToDecimal(mdr["disc_amt"]);
                        cust = mdr["cust"].ToString(); note = mdr["note"].ToString(); pay = mdr["pay"].ToString(); place = mdr["place"].ToString();
                    }
                    mdr.Close();

                    // Food items
                    var itemCmd = new SqlCommand(@"
                        SELECT f.name, ofd.quantity, f.selling_price,
                               ofd.quantity * f.selling_price AS subtotal
                        FROM order_food ofd
                        JOIN food f ON f.id = ofd.food_id
                        WHERE ofd.order_id = @oid
                        ORDER BY f.name", db.GetCon());
                    itemCmd.Parameters.AddWithValue("@oid", orderId);
                    var idr = itemCmd.ExecuteReader();

                    int y = 44; bool alt = false;
                    while (idr.Read())
                    {
                        string fname = idr["name"].ToString();
                        int qty = Convert.ToInt32(idr["quantity"]);
                        decimal price = Convert.ToDecimal(idr["selling_price"]);
                        decimal sub   = Convert.ToDecimal(idr["subtotal"]);
                        bool a = alt;
                        string fn = fname, qs = qty + " ta", ps = Fmt(price), ss = Fmt(sub);

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 48 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.45f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(qs, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(ps, x, ws[2], C_MUTED, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(ss, x, ws[3], C_GREEN, FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 48);
                        row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row);
                        content.Resize += (s, e) => { row.Width = content.Width; build(); };
                        build();
                        y += 48; alt = !alt;
                    }
                    idr.Close(); db.CloseCon();

                    // Summary footer
                    var lines = new List<(string lbl, string val, Color fc)>();
                    if (!string.IsNullOrEmpty(cust))   lines.Add(("Mijoz:", cust, C_BODY));
                    if (!string.IsNullOrEmpty(place))   lines.Add(("Joy:", place, C_BODY));
                    lines.Add(("To'lov:", pay, C_BODY));
                    if (dPct > 0) lines.Add(("Chegirma:", dPct.ToString("N1") + "% = −" + Fmt(dAmt), C_RED));
                    else          lines.Add(("Chegirma:", "−" + Fmt(dAmt), C_RED));
                    lines.Add(("Jami:", Fmt(oTotal), C_GREEN));
                    if (!string.IsNullOrEmpty(note)) lines.Add(("Izoh:", note, C_MUTED));

                    Panel foot = new Panel { BackColor = Color.FromArgb(249, 249, 251), Height = lines.Count * 36 + 8 };
                    foot.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, 0, foot.Width, 0);
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear();
                        int fy = 8;
                        foreach (var ln in lines)
                        {
                            foot.Controls.Add(new Label { Text = ln.lbl, Font = new Font("Segoe UI", 10), ForeColor = C_MUTED, Location = new Point(24, fy), AutoSize = true });
                            foot.Controls.Add(new Label { Text = ln.val, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ln.fc, Location = new Point(160, fy), AutoSize = true });
                            fy += 36;
                        }
                    };
                    foot.SetBounds(0, y, content.Width, foot.Height);
                    foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot);
                    content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); };
                    buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: QARZLAR
        // ══════════════════════════════════════════════════════════════
        void Detail_Qarzlar()
        {
            OpenOverlay("Qarzlar", content =>
            {
                AddColHeader(content,
                    new[] { "#", "Sana", "Vaqt", "Qarzdor", "Telefon", "Buyurtma", "Summa", "Holat", "" },
                    new float[] { .06f, .11f, .08f, .17f, .13f, .08f, .14f, .10f, .13f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT od.id,
                               od.order_id,
                               CAST(od.created_at AS DATE)    AS dt,
                               CAST(od.created_at AS TIME(0)) AS tm,
                               od.debtor_name,
                               ISNULL(od.debtor_phone,'—')    AS phone,
                               od.amount                      AS amt,
                               od.is_paid,
                               ISNULL(od.debt_note,'')        AS debt_note
                        FROM order_debt od
                        WHERE CAST(od.created_at AS DATE) BETWEEN @d1 AND @d2
                        ORDER BY od.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    var rows = new List<(int id, int oid, string dt, string tm, string name, string phone, decimal amt, bool isPaid, string note)>();
                    while (dr.Read())
                        rows.Add((
                            Convert.ToInt32(dr["id"]),
                            Convert.ToInt32(dr["order_id"]),
                            Convert.ToDateTime(dr["dt"]).ToString("dd.MM.yyyy"),
                            dr["tm"].ToString().Substring(0, 5),
                            dr["debtor_name"].ToString(),
                            dr["phone"].ToString(),
                            Convert.ToDecimal(dr["amt"]),
                            Convert.ToBoolean(dr["is_paid"]),
                            dr["debt_note"].ToString()
                        ));
                    dr.Close(); db.CloseCon();

                    decimal grandDebt = 0;
                    int y = 44; bool alt = false;
                    foreach (var r in rows)
                    {
                        if (!r.isPaid) grandDebt += r.amt;
                        int  did = r.id; int oid = r.oid; bool a = alt, paid = r.isPaid;
                        string cId    = "#" + r.oid,
                               cDt    = r.dt, cTm = r.tm,
                               cName  = r.name, cPh = r.phone,
                               cAmt   = Fmt(r.amt),
                               cHolat = paid ? "To'landi" : "Qarzdor";

                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear();
                            int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.06f),(int)(rw*.11f),(int)(rw*.08f),(int)(rw*.17f),(int)(rw*.13f),(int)(rw*.08f),(int)(rw*.14f),(int)(rw*.10f),(int)(rw*.13f) };
                            row.Controls.Add(RowLbl(cId,          x, ws[0], C_DARK,             FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(cDt,          x, ws[1], C_BODY,             FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(cTm,          x, ws[2], C_MUTED,            FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("👤 " + cName,x, ws[3], C_DARK,             FontStyle.Bold,    row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("📞 " + cPh,  x, ws[4], C_MUTED,            FontStyle.Regular, row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(cId,          x, ws[5], C_BLUE,             FontStyle.Regular, row.Height)); x += ws[5];
                            row.Controls.Add(RowLbl(cAmt,         x, ws[6], paid ? C_GREEN : C_RED, FontStyle.Bold, row.Height)); x += ws[6];
                            row.Controls.Add(RowLbl(cHolat,       x, ws[7], paid ? C_GREEN : C_ORANGE, FontStyle.Bold, row.Height)); x += ws[7];
                            // Mark paid button OR detail button
                            int bw = ws[8] - 4; int bx = x + 2;
                            if (!paid)
                            {
                                Button btnPay = new Button { Text = "✓ To'landi", Width = bw, Height = 24, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(bx, 4) };
                                btnPay.FlatAppearance.BorderSize = 0;
                                btnPay.Click += (bs, be) => { MarkDebtPaid(did); Detail_Qarzlar(); };
                                Button btnDet = new Button { Text = "→", Width = bw, Height = 22, FlatStyle = FlatStyle.Flat, BackColor = C_BLUE, ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(bx, 30) };
                                btnDet.FlatAppearance.BorderSize = 0;
                                btnDet.Click += (bs, be) => Detail_QarzOrder(oid, cName);
                                row.Controls.Add(btnPay); row.Controls.Add(btnDet);
                            }
                            else
                            {
                                Button btnDet = new Button { Text = "→", Width = bw, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = C_MUTED, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand, Location = new Point(bx, (row.Height - 34) / 2) };
                                btnDet.FlatAppearance.BorderSize = 0;
                                btnDet.Click += (bs, be) => Detail_QarzOrder(oid, cName);
                                row.Controls.Add(btnDet);
                            }
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }

                    if (rows.Count == 0) { content.Controls.Add(EmptyLabel("Bu sanada qarzlar yo'q")); return; }

                    // Footer
                    Panel foot = new Panel { BackColor = Color.FromArgb(254, 242, 242), Height = 48 };
                    foot.Paint += (s, e) => { e.Graphics.DrawLine(new Pen(C_RED), 0, 0, foot.Width, 0); e.Graphics.DrawLine(new Pen(C_BORDER), 0, foot.Height - 1, foot.Width, foot.Height - 1); };
                    string td = Fmt(grandDebt);
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear();
                        foot.Controls.Add(new Label { Text = "Jami to'lanmagan qarz: " + td, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_RED, AutoSize = true, Location = new Point(16, 0), Height = foot.Height, TextAlign = ContentAlignment.MiddleLeft });
                        foot.Controls.Add(new Label { Text = rows.Count + " ta qarz yozuvi", Font = new Font("Segoe UI", 10), ForeColor = C_MUTED, Height = foot.Height, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Right, Width = 180, Left = foot.Width - 196 });
                    };
                    foot.SetBounds(0, y, content.Width, 48); foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot); content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); }; buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        void MarkDebtPaid(int debtId)
        {
            try
            {
                var db = new dbconnect(); db.OpenCon();
                var cmd = new SqlCommand("UPDATE order_debt SET is_paid=1, paid_at=GETDATE() WHERE id=@id", db.GetCon());
                cmd.Parameters.AddWithValue("@id", debtId);
                cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        void Detail_QarzOrder(int orderId, string debtorName)
        {
            OpenOverlay("Buyurtma #" + orderId + " — " + debtorName, content =>
            {
                AddColHeader(content, new[] { "Taom nomi", "Miqdor", "Narxi", "Jami" },
                    new float[] { .45f, .18f, .20f, .17f });

                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var metaCmd = new SqlCommand(@"
                        SELECT o.created_at, ISNULL(o.total,0) AS total,
                               ISNULL(o.discount_amount,0) AS disc,
                               ISNULL(u.name,'—') AS waiter,
                               ISNULL(p.name,'—') AS pay,
                               ISNULL(pi.room_name,'—') AS place,
                               ISNULL(po.name,'') AS zone,
                               ISNULL(o.order_note,'') AS note,
                               ISNULL(od.debtor_name,'') AS dname,
                               ISNULL(od.debtor_phone,'') AS dphone,
                               ISNULL(od.debt_note,'') AS dnote,
                               ISNULL(od.is_paid,0) AS dpaid
                        FROM [order] o
                        LEFT JOIN [user]      u  ON u.id  = o.user_id
                        LEFT JOIN payment     p  ON p.id  = o.payment_id
                        LEFT JOIN place_in    pi ON pi.id = o.place_id
                        LEFT JOIN place_out   po ON po.id = pi.place_out_id
                        LEFT JOIN order_debt  od ON od.order_id = o.id
                        WHERE o.id = @oid", db.GetCon());
                    metaCmd.Parameters.AddWithValue("@oid", orderId);
                    var mdr = metaCmd.ExecuteReader();
                    DateTime created = DateTime.Now; decimal oTotal = 0, discAmt = 0;
                    string waiter = "—", pay = "—", place = "—", zone = "", note = "", dname = "", dphone = "", dnote = "";
                    bool dpaid = false;
                    if (mdr.Read())
                    {
                        created = Convert.ToDateTime(mdr["created_at"]); oTotal = Convert.ToDecimal(mdr["total"]);
                        discAmt = Convert.ToDecimal(mdr["disc"]); waiter = mdr["waiter"].ToString();
                        pay     = mdr["pay"].ToString();     place  = mdr["place"].ToString();
                        zone    = mdr["zone"].ToString();    note   = mdr["note"].ToString();
                        dname   = mdr["dname"].ToString();   dphone = mdr["dphone"].ToString();
                        dnote   = mdr["dnote"].ToString();   dpaid  = Convert.ToBoolean(mdr["dpaid"]);
                    }
                    mdr.Close();
                    try
                    {
                        var payParts = new List<string>();
                        using (var pc = new SqlCommand("SELECT p.name, op.amount FROM order_payments op JOIN payment p ON p.id=op.payment_id WHERE op.order_id=@oid ORDER BY op.id", db.GetCon()))
                        {
                            pc.Parameters.AddWithValue("@oid", orderId);
                            using (var pdr = pc.ExecuteReader())
                                while (pdr.Read()) payParts.Add(pdr["name"] + ": " + Fmt(Convert.ToDecimal(pdr["amount"])));
                        }
                        if (payParts.Count > 0) pay = string.Join("  /  ", payParts);
                    }
                    catch { }

                    var itemCmd = new SqlCommand(@"
                        SELECT f.name, ofd.quantity, f.selling_price,
                               ofd.quantity * f.selling_price AS subtotal
                        FROM order_food ofd JOIN food f ON f.id = ofd.food_id
                        WHERE ofd.order_id = @oid ORDER BY f.name", db.GetCon());
                    itemCmd.Parameters.AddWithValue("@oid", orderId);
                    var idr = itemCmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (idr.Read())
                    {
                        string fname = idr["name"].ToString(); int qty = Convert.ToInt32(idr["quantity"]);
                        decimal price = Convert.ToDecimal(idr["selling_price"]), sub = Convert.ToDecimal(idr["subtotal"]);
                        bool a = alt;
                        string fn = fname, qs = qty + " ta", ps = Fmt(price), ss = Fmt(sub);
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 48 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.45f),(int)(rw*.18f),(int)(rw*.20f),(int)(rw*.17f) };
                            row.Controls.Add(RowLbl(fn, x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(qs, x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(ps, x, ws[2], C_MUTED, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(ss, x, ws[3], C_GREEN, FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 48); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 48; alt = !alt;
                    }
                    idr.Close(); db.CloseCon();

                    var lines = new List<(string l, string v, Color c)>();
                    lines.Add(("Sana:",       created.ToString("dd.MM.yyyy  HH:mm"), C_BODY));
                    lines.Add(("Joy:",        (!string.IsNullOrEmpty(zone) ? zone + " / " : "") + place, C_BODY));
                    lines.Add(("Ofitsiant:",  waiter, C_BODY));
                    lines.Add(("To'lov turi:", pay, C_BODY));
                    if (discAmt > 0) lines.Add(("Chegirma:", "−" + Fmt(discAmt), C_RED));
                    lines.Add(("Jami summa:", Fmt(oTotal), C_GREEN));
                    if (!string.IsNullOrEmpty(note)) lines.Add(("Izoh:", note, C_MUTED));
                    lines.Add(("── Qarz ──────────────────", "", C_MUTED));
                    lines.Add(("Qarzdor:", dname, C_RED));
                    if (!string.IsNullOrEmpty(dphone)) lines.Add(("Telefon:", dphone, C_BODY));
                    if (!string.IsNullOrEmpty(dnote))  lines.Add(("Qarz izoh:", dnote, C_MUTED));
                    lines.Add(("Holat:", dpaid ? "✓ To'langan" : "⚠ Qarzdor", dpaid ? C_GREEN : C_RED));

                    Panel foot = new Panel { BackColor = Color.FromArgb(254, 242, 242), Height = lines.Count * 36 + 8 };
                    foot.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_RED), 0, 0, foot.Width, 0);
                    Action buildFoot = () =>
                    {
                        foot.Controls.Clear(); int fy = 8;
                        foreach (var ln in lines)
                        {
                            foot.Controls.Add(new Label { Text = ln.l, Font = new Font("Segoe UI", 10), ForeColor = C_MUTED, Location = new Point(24, fy), AutoSize = true });
                            if (!string.IsNullOrEmpty(ln.v)) foot.Controls.Add(new Label { Text = ln.v, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ln.c, Location = new Point(200, fy), AutoSize = true });
                            fy += 36;
                        }
                    };
                    foot.SetBounds(0, y, content.Width, foot.Height); foot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(foot); content.Resize += (s, e) => { foot.Width = content.Width; buildFoot(); }; buildFoot();
                }
                catch { db.CloseCon(); }
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  DETAIL: SIMPLE
        // ══════════════════════════════════════════════════════════════
        void Detail_Izohlar()
        {
            OpenOverlay("Izohlar", content =>
            {
                AddColHeader(content,
                    new[] { "#", "Sana va Vaqt", "Joy", "Ofitsiant", "Taom", "Izoh" },
                    new float[] { .07f, .16f, .14f, .16f, .18f, .29f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT o.id, o.created_at,
                               ISNULL(pi.room_name,'—') AS place,
                               ISNULL(po.name,'') AS zone,
                               ISNULL(u.name,'—') AS waiter,
                               f.name AS food_name,
                               ofd.note
                        FROM order_food ofd
                        JOIN [order]    o  ON o.id  = ofd.order_id
                        JOIN food       f  ON f.id  = ofd.food_id
                        LEFT JOIN [user]    u  ON u.id  = o.user_id
                        LEFT JOIN place_in  pi ON pi.id = o.place_id
                        LEFT JOIN place_out po ON po.id = pi.place_out_id
                        WHERE o.paid='YES'
                          AND ISNULL(ofd.note,'') <> ''
                          AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                        ORDER BY o.created_at DESC", db.GetCon());
                    cmd.Parameters.AddWithValue("@d1", D1());
                    cmd.Parameters.AddWithValue("@d2", D2());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    while (dr.Read())
                    {
                        int    oid     = Convert.ToInt32(dr["id"]);
                        DateTime dt    = Convert.ToDateTime(dr["created_at"]);
                        string  place  = dr["place"].ToString();
                        string  zone   = dr["zone"].ToString();
                        string  waiter = dr["waiter"].ToString();
                        string  food   = dr["food_name"].ToString();
                        string  note   = dr["note"].ToString();
                        bool a = alt;
                        string _oid = "#" + oid, _dt = dt.ToString("dd.MM.yy  HH:mm"),
                               _place = (!string.IsNullOrEmpty(zone) ? zone + "/" : "") + place,
                               _waiter = waiter, _food = food, _note = note;
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 56 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.07f),(int)(rw*.16f),(int)(rw*.14f),(int)(rw*.16f),(int)(rw*.18f),(int)(rw*.29f) };
                            row.Controls.Add(RowLbl(_oid,    x, ws[0], C_DARK,   FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl("📅 "+_dt,  x, ws[1], C_MUTED,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl("🪑 "+_place, x, ws[2], C_BODY, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl("👤 "+_waiter,x, ws[3], C_BODY, FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl("🍽 "+_food,  x, ws[4], C_DARK, FontStyle.Bold,    row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl("✎ "+_note,   x, ws[5], Color.FromArgb(109,40,217), FontStyle.Italic, row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 56); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 56; alt = !alt;
                    }
                    dr.Close(); db.CloseCon();
                    if (y == 44) content.Controls.Add(EmptyLabel("Bu sanada izohli buyurtmalar yo'q"));
                }
                catch { db.CloseCon(); content.Controls.Add(EmptyLabel("Izohlarni yuklashda xatolik")); }
            });
        }

        void Detail_Masalliqlar()
        {
            OpenOverlay("Masalliqlar", content =>
            {
                AddColHeader(content,
                    new[] { "Nomi", "Miqdor", "Birlik", "Narx/birlik", "Jami qiymat", "Min miqdor", "Holat" },
                    new float[] { .22f, .12f, .08f, .14f, .16f, .12f, .16f });
                var db = new dbconnect(); db.OpenCon();
                try
                {
                    var cmd = new SqlCommand(@"
                        SELECT name, ISNULL(quantity,0) AS qty, ISNULL(unit,'') AS unit,
                               ISNULL(price_per_unit,0) AS ppu,
                               ISNULL(quantity,0)*ISNULL(price_per_unit,0) AS total_val,
                               ISNULL(min_quantity,0) AS minq
                        FROM ingredient
                        ORDER BY name", db.GetCon());
                    var dr = cmd.ExecuteReader();
                    int y = 44; bool alt = false;
                    decimal grandVal = 0;
                    while (dr.Read())
                    {
                        string nm   = dr["name"].ToString();
                        decimal qty = Convert.ToDecimal(dr["qty"]);
                        string unit = dr["unit"].ToString();
                        decimal ppu = Convert.ToDecimal(dr["ppu"]);
                        decimal tv  = Convert.ToDecimal(dr["total_val"]);
                        decimal minq = Convert.ToDecimal(dr["minq"]);
                        grandVal += tv;
                        bool a = alt, low = qty < minq, empty = qty <= 0;
                        string holat = empty ? "✗ Tugagan" : low ? "⚠ Kam" : "✓ Normal";
                        Color holatClr = empty ? C_RED : low ? C_ORANGE : C_GREEN;
                        string _nm = nm, _qty = qty.ToString("G29"), _unit = unit, _ppu = Fmt(ppu), _tv = Fmt(tv), _minq = minq.ToString("G29"), _h = holat;
                        Color _hc = holatClr;
                        Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : C_CARD, Height = 52 };
                        row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(C_BORDER), 0, row.Height - 1, row.Width, row.Height - 1);
                        Action build = () =>
                        {
                            row.Controls.Clear(); int rw = row.Width > 20 ? row.Width : content.Width, x = 16;
                            int[] ws = { (int)(rw*.22f),(int)(rw*.12f),(int)(rw*.08f),(int)(rw*.14f),(int)(rw*.16f),(int)(rw*.12f),(int)(rw*.16f) };
                            row.Controls.Add(RowLbl(_nm,          x, ws[0], C_DARK,  FontStyle.Bold,    row.Height)); x += ws[0];
                            row.Controls.Add(RowLbl(_qty,         x, ws[1], C_BODY,  FontStyle.Regular, row.Height)); x += ws[1];
                            row.Controls.Add(RowLbl(_unit,        x, ws[2], C_MUTED, FontStyle.Regular, row.Height)); x += ws[2];
                            row.Controls.Add(RowLbl(_ppu+" so'm", x, ws[3], C_MUTED, FontStyle.Regular, row.Height)); x += ws[3];
                            row.Controls.Add(RowLbl(_tv +" so'm", x, ws[4], C_GREEN, FontStyle.Bold,    row.Height)); x += ws[4];
                            row.Controls.Add(RowLbl(_minq,        x, ws[5], C_MUTED, FontStyle.Regular, row.Height)); x += ws[5];
                            row.Controls.Add(RowLbl(_h,           x, ws[6], _hc,     FontStyle.Bold,    row.Height));
                        };
                        row.SetBounds(0, y, content.Width, 52); row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        content.Controls.Add(row); content.Resize += (s, e) => { row.Width = content.Width; build(); }; build();
                        y += 52; alt = !alt;
                    }
                    dr.Close();

                    if (y == 44) { db.CloseCon(); content.Controls.Add(EmptyLabel("Masalliqlar topilmadi")); return; }

                    // JAMI footer
                    string gtStr = grandVal.ToString("N0");
                    Panel footer = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 48 };
                    footer.Paint += (s, e) =>
                    {
                        e.Graphics.DrawLine(new Pen(C_GREEN), 0, 0, footer.Width, 0);
                        e.Graphics.DrawLine(new Pen(C_BORDER), 0, footer.Height - 1, footer.Width, footer.Height - 1);
                    };
                    Action fbuild = () =>
                    {
                        footer.Controls.Clear(); int rw = footer.Width > 20 ? footer.Width : content.Width;
                        footer.Controls.Add(new Label { Text = "Ombor jami qiymati:", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_MUTED, Location = new Point(rw - 340, 0), Width = 180, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                        footer.Controls.Add(new Label { Text = gtStr + " so'm", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = C_GREEN, Location = new Point(rw - 155, 0), Width = 140, Height = 48, TextAlign = ContentAlignment.MiddleRight });
                    };
                    footer.SetBounds(0, y, content.Width, 48); footer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(footer); content.Resize += (s, e) => { footer.Width = content.Width; fbuild(); }; fbuild();
                    db.CloseCon();
                }
                catch { db.CloseCon(); content.Controls.Add(EmptyLabel("Masalliqlarni yuklashda xatolik")); }
            });
        }

        void Detail_Simple(string title, string sql, string fallback = null, bool dateFilter = false)
        {
            OpenOverlay(title, content =>
            {
                string val = "0";
                if (sql != null)
                {
                    var db = new dbconnect(); db.OpenCon();
                    try
                    {
                        var cmd = new SqlCommand(sql, db.GetCon());
                        if (dateFilter) { cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2()); }
                        object r = cmd.ExecuteScalar(); val = (r == null || r == DBNull.Value) ? "0" : r.ToString();
                        db.CloseCon();
                    }
                    catch
                    {
                        db.CloseCon();
                        if (fallback != null) try { var db2 = new dbconnect(); db2.OpenCon(); var cmd2 = new SqlCommand(fallback, db2.GetCon()); object r2 = cmd2.ExecuteScalar(); val = r2 == null || r2 == DBNull.Value ? "0" : r2.ToString(); db2.CloseCon(); } catch { }
                    }
                }
                content.Controls.Add(new Label { Text = title + ": " + val, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = C_GREEN, AutoSize = true, Location = new Point(32, 32) });
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  LAYOUT
        // ══════════════════════════════════════════════════════════════
        void LayoutFilterRight(Panel bar)
        {
            int rx = bar.Width - PAD;
            _btnRange.Location = new Point(rx - _btnRange.Width, (bar.Height - _btnRange.Height) / 2);
            _btnSingle.Location = new Point(_btnRange.Left - 8 - _btnSingle.Width, (bar.Height - _btnSingle.Height) / 2);
            int px = _btnSingle.Left - 10 - (_range ? _pnlRange.Width : _pnlSingle.Width);
            _pnlSingle.Location = new Point(px, _btnSingle.Top); _pnlRange.Location = new Point(px, _btnSingle.Top);
        }

        void LayoutContent()
        {
            if (_scrollOuter == null || _scrollOuter.ClientSize.Width < 50) return;
            int w = _scrollOuter.ClientSize.Width - PAD * 2, x = PAD, y = PAD;
            int cw = (w - CARD_G) / 2;
            for (int i = 0; i < _statGrid.Controls.Count; i++)
                _statGrid.Controls[i].SetBounds((i % 2) * (cw + CARD_G), (i / 2) * (CARD_H + CARD_G), cw, CARD_H);
            _statGrid.SetBounds(x, y, w, 3 * CARD_H + 2 * CARD_G); y += _statGrid.Height + 14;
            _profitCard.SetBounds(x, y, w, 122); y += 122 + 14;
            _chartPanel.SetBounds(x, y, w, 295); _chartPanel.Invalidate(); y += 295 + 14;
            int mw = (w - MINI_G * 2) / 3;
            for (int i = 0; i < _miniGrid.Controls.Count; i++)
                _miniGrid.Controls[i].SetBounds((i % 3) * (mw + MINI_G), (i / 3) * (MINI_H + MINI_G), mw, MINI_H);
            int miniRows = (int)Math.Ceiling(_miniGrid.Controls.Count / 3.0);
            _miniGrid.SetBounds(x, y, w, miniRows * MINI_H + (miniRows - 1) * MINI_G);
        }

        // ══════════════════════════════════════════════════════════════
        //  DATA
        // ══════════════════════════════════════════════════════════════
        void LoadAll()
        {
            _lblHeader.Text = DateHeader();
            try { LoadStats(); } catch { }
            try { LoadChartPts(); } catch { }
            try { LoadMini(); } catch { }
            _chartPanel?.Invalidate();
        }

        void LoadStats()
        {
            var db = new dbconnect();
            db.OpenCon();
            string wc = "paid='YES' AND CAST(created_at AS DATE) BETWEEN @d1 AND @d2";
            decimal oSum = 0, fp = 0, fee = 0, plP = 0, pur = 0;
            int oCnt = 0;

            try { oSum = Q<decimal>(db, $"SELECT ISNULL(SUM(total),0) FROM [order] WHERE {wc}"); } catch { }
            _lblOrderSum.Text = Fmt(oSum);

            try { oCnt = Q<int>(db, $"SELECT COUNT(*) FROM [order] WHERE {wc}"); } catch { }
            _lblOrderCnt.Text = oCnt.ToString();

            try { fp = Q<decimal>(db, "SELECT ISNULL(SUM((f.selling_price - ISNULL(f.original_price,0)) * ofd.quantity),0) FROM [order] o JOIN order_food ofd ON ofd.order_id=o.id JOIN food f ON f.id=ofd.food_id WHERE o.paid='YES' AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2"); } catch { }
            _lblFoodProfit.Text = Fmt(fp);

            // Try fee_amount column; if missing, derive from service_charge setting
            bool feeFromDb = false;
            try { fee = Q<decimal>(db, $"SELECT ISNULL(SUM(fee_amount),0) FROM [order] WHERE {wc}"); feeFromDb = true; } catch { }
            if (!feeFromDb)
            {
                decimal svcPct = 0;
                decimal.TryParse(PrintService.GetSetting("service_charge", "0"), out svcPct);
                if (svcPct > 0) fee = Math.Round(oSum * svcPct / 100);
            }
            _lblFeeProfit.Text = Fmt(fee);

            try { plP = Q<decimal>(db, "SELECT ISNULL(SUM(pi.price),0) FROM [order] o JOIN place_in pi ON pi.id=o.place_id WHERE o.paid='YES' AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2"); } catch { }
            _lblPlaceProfit.Text = Fmt(plP);

            try { pur = Q<decimal>(db, "SELECT ISNULL((SELECT SUM(total_price) FROM ingredient_purchase WHERE CAST(purchased_at AS DATE) BETWEEN @d1 AND @d2),0) + ISNULL((SELECT SUM(total_price) FROM food_purchase WHERE CAST(purchased_at AS DATE) BETWEEN @d1 AND @d2),0)"); } catch { }
            _lblPurchase.Text = Fmt(pur);

            decimal profit = fp + fee + plP;
            _lblProfit.Text = Fmt(profit);
            if (_lblProfit.Parent != null) _lblProfit.Location = new Point(Math.Max(0, (_lblProfit.Parent.Width - _lblProfit.PreferredWidth) / 2), _lblProfit.Location.Y);
            db.CloseCon();
        }

        void LoadChartPts()
        {
            _chartPts.Clear();
            DateTime s = _range ? _dateFrom : _date.AddDays(-6), e = _range ? _dateTo : _date;
            var db = new dbconnect(); db.OpenCon();
            var cmd = new SqlCommand("SELECT CAST(created_at AS DATE) AS dt, ISNULL(SUM(total),0) AS tot FROM [order] WHERE paid='YES' AND CAST(created_at AS DATE) BETWEEN @s AND @e GROUP BY CAST(created_at AS DATE)", db.GetCon());
            cmd.Parameters.AddWithValue("@s", s); cmd.Parameters.AddWithValue("@e", e);
            var dr = cmd.ExecuteReader();
            while (dr.Read()) _chartPts[Convert.ToDateTime(dr["dt"]).Date] = Convert.ToDecimal(dr["tot"]);
            dr.Close(); db.CloseCon();
        }

        void LoadMini()
        {
            var db = new dbconnect(); db.OpenCon();
            SetMini(db, "Xodimlar",            "SELECT COUNT(*) FROM [user]");
            SetMini(db, "Mahsulotlar",          "SELECT COUNT(*) FROM food");
            SetMini(db, "Buyurtmalar",          "SELECT COUNT(*) FROM [order] WHERE paid='YES' AND CAST(created_at AS DATE) BETWEEN @d1 AND @d2", true);
            SetMini(db, "Masalliqlar",          "SELECT COUNT(*) FROM material", false, "SELECT COUNT(*) FROM ingredient");
            SetMini(db, "To'lovlar",            "SELECT COUNT(*) FROM payment");
            SetMini(db, "Kategoriyalar",        "SELECT COUNT(*) FROM food_category");
            SetMini(db, "Xaridlar", "SELECT (SELECT COUNT(*) FROM ingredient_purchase WHERE CAST(purchased_at AS DATE) BETWEEN @d1 AND @d2)+(SELECT COUNT(*) FROM food_purchase WHERE CAST(purchased_at AS DATE) BETWEEN @d1 AND @d2)", true);
            SetMini(db, "Bekor qilishlar",      "SELECT ISNULL(SUM(cancelled_qty),0) FROM order_cancellation_log WHERE CAST(cancelled_at AS DATE) BETWEEN @d1 AND @d2", true);
            SetMini(db, "Chegirmalar hisoboti", "SELECT ISNULL(SUM(discount_amount),0) FROM [order] WHERE CAST(created_at AS DATE) BETWEEN @d1 AND @d2", true, money: true);
            SetMini(db, "Joylar",   "SELECT COUNT(*) FROM place_in");
            SetMini(db, "Qarzlar",  "SELECT COUNT(*) FROM order_debt WHERE is_paid=0 AND CAST(created_at AS DATE) BETWEEN @d1 AND @d2", true);
            SetMini(db, "Izohlar",  "SELECT COUNT(*) FROM order_food WHERE ISNULL(note,'') <> '' AND order_id IN (SELECT id FROM [order] WHERE paid='YES' AND CAST(created_at AS DATE) BETWEEN @d1 AND @d2)", true);
            db.CloseCon();
        }

        void SetMini(dbconnect db, string key, string sql, bool df = false, string fb = null, bool money = false)
        {
            if (!_miniLbls.ContainsKey(key)) return;
            try
            {
                var cmd = new SqlCommand(sql, db.GetCon());
                if (df) { cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2()); }
                object v = cmd.ExecuteScalar(); string t = (v == null || v == DBNull.Value) ? "0" : v.ToString();
                if (money && decimal.TryParse(t, out decimal m)) t = Fmt(m);
                _miniLbls[key].Text = t;
            }
            catch
            {
                if (fb != null) try { var c2 = new SqlCommand(fb, db.GetCon()); object v2 = c2.ExecuteScalar(); _miniLbls[key].Text = (v2 == null || v2 == DBNull.Value) ? "0" : v2.ToString(); } catch { _miniLbls[key].Text = "0"; }
                else _miniLbls[key].Text = "0";
            }
        }

        T Q<T>(dbconnect db, string sql) { var cmd = new SqlCommand(sql, db.GetCon()); cmd.Parameters.AddWithValue("@d1", D1()); cmd.Parameters.AddWithValue("@d2", D2()); object r = cmd.ExecuteScalar(); if (r == null || r == DBNull.Value) return default(T); return (T)Convert.ChangeType(r, typeof(T)); }

        // ══════════════════════════════════════════════════════════════
        //  CHART
        // ══════════════════════════════════════════════════════════════
        void DrawChart(object sender, PaintEventArgs e)
        {
            Panel p = (Panel)sender; Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            int W = p.Width, H = p.Height, pL = 88, pR = 28, pT = 46, pB = 38, cW = W - pL - pR, cH = H - pT - pB;
            if (cW < 10 || cH < 10) return;
            g.FillRectangle(new SolidBrush(C_CARD), 0, 0, W, H);
            using (var f = new Font("Segoe UI", 12, FontStyle.Bold)) g.DrawString("Buyurtmalar statistikasi", f, new SolidBrush(C_DARK), pL, 12);
            DateTime start = _range ? _dateFrom : _date.AddDays(-6), end = _range ? _dateTo : _date;
            var dates = new List<DateTime>(); for (DateTime d = start; d <= end; d = d.AddDays(1)) dates.Add(d);
            if (dates.Count == 0) return; int n = dates.Count;
            decimal maxV = 1; foreach (DateTime d in dates) if (_chartPts.ContainsKey(d) && _chartPts[d] > maxV) maxV = _chartPts[d];
            using (var gp = new Pen(Color.FromArgb(234, 236, 240))) using (var af = new Font("Segoe UI", 8)) using (var ab = new SolidBrush(C_MUTED))
            {
                var rf = new StringFormat { Alignment = StringAlignment.Far };
                for (int i = 0; i <= 4; i++) { float fy = pT + cH * (1f - i / 4f); g.DrawLine(gp, pL, (int)fy, pL + cW, (int)fy); decimal yv = maxV * i / 4; string lbl = yv >= 1_000_000 ? (yv / 1_000_000m).ToString("N1") + "M" : yv >= 1_000 ? (yv / 1_000m).ToString("N0") + "K" : yv.ToString("N0"); g.DrawString(lbl, af, ab, new RectangleF(0, fy - 9, pL - 5, 18), rf); }
                int step = Math.Max(1, n / 8); var cf = new StringFormat { Alignment = StringAlignment.Center };
                for (int i = 0; i < n; i += step) { float fx = n == 1 ? pL + cW / 2f : pL + cW * i / (n - 1f); g.DrawString(dates[i].ToString("dd-MMM"), af, ab, new RectangleF(fx - 28, pT + cH + 5, 56, 16), cf); }
            }
            var pts = new PointF[n];
            for (int i = 0; i < n; i++) { decimal v = _chartPts.ContainsKey(dates[i]) ? _chartPts[dates[i]] : 0; float fx = n == 1 ? pL + cW / 2f : pL + cW * i / (n - 1f); pts[i] = new PointF(fx, pT + cH * (1f - (float)(v / maxV))); }
            if (n > 1)
            {
                var poly = new PointF[n + 2]; poly[0] = new PointF(pts[0].X, pT + cH); for (int i = 0; i < n; i++) poly[i + 1] = pts[i]; poly[n + 1] = new PointF(pts[n - 1].X, pT + cH);
                // FillPolygon with LinearGradientBrush throws OverflowException when all Y values
                // are identical (zero-area polygon — e.g. no data in range). Guard against it.
                float minPtY = float.MaxValue; foreach (var pt in pts) if (pt.Y < minPtY) minPtY = pt.Y;
                if (minPtY < pT + cH - 1f)
                    using (var gb = new LinearGradientBrush(new PointF(0, pT), new PointF(0, pT + cH), Color.FromArgb(130, C_GREEN), Color.FromArgb(12, C_GREEN))) g.FillPolygon(gb, poly);
                using (var lp = new Pen(C_GREEN, 2.5f)) g.DrawLines(lp, pts);
                foreach (var pt in pts) { g.FillEllipse(new SolidBrush(C_CARD), pt.X - 4.5f, pt.Y - 4.5f, 9, 9); g.FillEllipse(new SolidBrush(C_GREEN), pt.X - 3f, pt.Y - 3f, 6, 6); }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CARD BUILDERS
        // ══════════════════════════════════════════════════════════════
        Panel StatCard(string title, Label valLbl, Color bg, Color accent, bool count = false)
        {
            Panel card = new Panel { BackColor = bg }; SetRound(card);
            Panel bar = new Panel { Width = 5, Location = Point.Empty, BackColor = accent }; card.Controls.Add(bar);
            card.Resize += (s, e) => bar.Height = card.Height;
            Label lblT = new Label { Text = title, Font = new Font("Segoe UI", 9), ForeColor = C_BODY, Location = new Point(18, 13), AutoSize = false, Height = 19 }; card.Controls.Add(lblT);
            card.Resize += (s, e) => lblT.Width = card.Width - 22;
            valLbl.Text = count ? "0" : "0 UZS"; valLbl.Font = count ? new Font("Segoe UI", 25, FontStyle.Bold) : new Font("Segoe UI", 17, FontStyle.Bold);
            valLbl.ForeColor = accent; valLbl.Location = new Point(18, 38); valLbl.AutoSize = false; valLbl.Height = 42; card.Controls.Add(valLbl);
            card.Resize += (s, e) => valLbl.Width = card.Width - 22;
            return card;
        }

        Panel ProfitCard(Label valLbl)
        {
            Panel card = new Panel { BackColor = C_GREEN }; SetRound(card, 16);
            card.Paint += (s, e) => { var c = (Panel)s; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = RPath(new Rectangle(0, 0, c.Width, c.Height), 16)) using (var gb = new LinearGradientBrush(new Point(0, 0), new Point(c.Width, c.Height), Color.FromArgb(22, 197, 142), Color.FromArgb(5, 142, 99))) e.Graphics.FillPath(gb, path); };
            var sub = new Label { Text = "$ Jami foyda:", Font = new Font("Segoe UI", 12), ForeColor = Color.FromArgb(209, 250, 229), AutoSize = true }; card.Controls.Add(sub);
            card.Resize += (s, e) => { sub.Location = new Point(Math.Max(0, (card.Width - sub.PreferredWidth) / 2), 18); valLbl.Location = new Point(Math.Max(0, (card.Width - valLbl.PreferredWidth) / 2), 52); };
            valLbl.Text = "0 UZS"; valLbl.Font = new Font("Segoe UI", 30, FontStyle.Bold); valLbl.ForeColor = Color.White; valLbl.AutoSize = true; card.Controls.Add(valLbl);
            return card;
        }

        Panel MiniCard(string title, Label valLbl)
        {
            Panel card = new Panel { BackColor = C_CARD }; SetRound(card);
            card.Paint += (s, e) => { var c = (Panel)s; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = RPath(new Rectangle(0, 0, c.Width, c.Height), 12)) { e.Graphics.FillPath(new SolidBrush(C_CARD), path); e.Graphics.DrawPath(new Pen(C_BORDER, 1f), path); } };
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = C_DARK, AutoSize = true, Location = new Point(18, 14) });
            valLbl.Text = "0"; valLbl.Font = new Font("Segoe UI", 22, FontStyle.Bold); valLbl.ForeColor = C_GREEN; valLbl.AutoSize = true; valLbl.Location = new Point(18, 44); card.Controls.Add(valLbl);
            var lm = new Label { Text = "Ko'proq →", Font = new Font("Segoe UI", 9), ForeColor = C_GREEN, AutoSize = true }; card.Controls.Add(lm);
            card.Resize += (s, e) => lm.Location = new Point(card.Width - lm.PreferredWidth - 14, MINI_H - lm.PreferredHeight - 10);
            return card;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════
        void SetMode(bool range)
        {
            _range = range; _pnlSingle.Visible = !range; _pnlRange.Visible = range;
            _btnSingle.BackColor = range ? C_BORDER : C_GREEN; _btnSingle.ForeColor = range ? C_DARK : Color.White;
            _btnRange.BackColor = range ? C_GREEN : C_BORDER; _btnRange.ForeColor = range ? Color.White : C_DARK;
            LoadAll();
        }

        DateTime D1() => _range ? _dateFrom : _date;
        DateTime D2() => _range ? _dateTo   : _date;

        static Label RowLbl(string text, int x, int w, Color fc, FontStyle fs, int h)
            => new Label { Text = text, Font = new Font("Segoe UI", 10, fs), ForeColor = fc, Location = new Point(x, 0), Width = w, Height = h, TextAlign = ContentAlignment.MiddleLeft };

        static Label EmptyLabel(string text)
            => new Label { Text = text, Font = new Font("Segoe UI", 12), ForeColor = C_MUTED, AutoSize = true, Location = new Point(32, 32) };

        void SetRound(Panel p, int r = 12)
        {
            p.Resize += (s, e) => { if (p.Width >= r * 2 && p.Height >= r * 2) p.Region = new Region(RPath(new Rectangle(0, 0, p.Width, p.Height), r)); };
        }

        static GraphicsPath RPath(Rectangle r, int radius)
        {
            int d = radius * 2; var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90); path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        static string Fmt(decimal v) => v == 0 ? "0 UZS" : v.ToString("N0") + " UZS";

        string DateHeader() => _range
            ? $"{_dateFrom:yyyy, dd-MMMM} — {_dateTo:dd-MMMM} sana uchun hisobotlar"
            : $"{_date:yyyy, dd-MMMM} sana uchun hisobotlar";

        static string DayNameUz(DayOfWeek d)
        {
            switch (d)
            {
                case DayOfWeek.Monday: return "Dushanba"; case DayOfWeek.Tuesday: return "Seshanba";
                case DayOfWeek.Wednesday: return "Chorshanba"; case DayOfWeek.Thursday: return "Payshanba";
                case DayOfWeek.Friday: return "Juma"; case DayOfWeek.Saturday: return "Shanba";
                default: return "Yakshanba";
            }
        }

        Button GreenBtn(string text, int w, int h)
        {
            var b = new Button { Text = text, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = C_GREEN, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.BorderColor = C_GDARK; return b;
        }

        Button ToggleBtn(string text, int w, int h, bool active)
        {
            var b = new Button { Text = text, Width = w, Height = h, FlatStyle = FlatStyle.Flat, BackColor = active ? C_GREEN : C_BORDER, ForeColor = active ? Color.White : C_DARK, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.BorderColor = b.BackColor; return b;
        }

        // ── DAILY REPORT PRINT ────────────────────────────────────────────────
        private class RL
        {
            public string Label, Value;
            public bool IsHeader, IsSep, Bold;
            public RL(string label, string value = null, bool header = false, bool sep = false, bool bold = false)
            { Label = label; Value = value; IsHeader = header; IsSep = sep; Bold = bold; }
            public static RL Hdr(string t, bool bold = true) => new RL(t, null, true,  false, bold);
            public static RL Sep()                           => new RL("", null, false, true,  false);
            public static RL Row(string l, string v, bool bold = false) => new RL(l, v, false, false, bold);
            public static RL Txt(string l)                  => new RL(l, null, false, false, false);
        }

        void PrintDailyReport()
        {
            DateTime from, to;
            if (!PickReportDateTime(out from, out to)) return;

            string printer = PrintService.GetSetting("main_printer", "");
            if (string.IsNullOrEmpty(printer))
                try { printer = new PrinterSettings().PrinterName; } catch { return; }
            if (string.IsNullOrEmpty(printer)) return;

            string cafe = PrintService.GetSetting("cafe_name", "FoodX Cafe");
            var lines = BuildReportLines(cafe, from, to);
            if (lines.Count == 0) return;

            int idx = 0;
            var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printer;
            try { doc.DefaultPageSettings.PaperSize = new PaperSize("Thermal80", 315, 2750); } catch { }
            doc.DefaultPageSettings.Margins = new Margins(8, 8, 5, 5);
            doc.PrintPage += (s, e) =>
            {
                var g = e.Graphics;
                Font fB = new Font("Courier New", 8, FontStyle.Bold);
                Font fN = new Font("Courier New", 8);
                Font fS = new Font("Courier New", 7);
                Brush br = Brushes.Black;
                var ctr = new StringFormat { Alignment = StringAlignment.Center };
                var rgt = new StringFormat { Alignment = StringAlignment.Far };
                float x = 8, y = 8, w = e.MarginBounds.Width, lh = 14;
                float maxY = e.MarginBounds.Bottom - 8;

                while (idx < lines.Count)
                {
                    RL ln = lines[idx];
                    float h = ln.IsSep ? lh : ln.IsHeader ? lh + 4 : lh;
                    if (y + h > maxY) { e.HasMorePages = true; break; }

                    if (ln.IsSep)
                    {
                        g.DrawString(new string('-', 32), fS, br, x, y);
                    }
                    else if (ln.IsHeader)
                    {
                        g.DrawString(ln.Label, ln.Bold ? fB : fN, br, new RectangleF(x, y, w, h), ctr);
                    }
                    else
                    {
                        Font f = ln.Bold ? fB : fN;
                        g.DrawString(ln.Label, f, br, x, y);
                        if (ln.Value != null)
                            g.DrawString(ln.Value, f, br, new RectangleF(x, y, w, lh), rgt);
                    }
                    y += h;
                    idx++;
                }
                fB.Dispose(); fN.Dispose(); fS.Dispose();
            };
            try { doc.Print(); } catch { }
        }

        bool PickReportDateTime(out DateTime from, out DateTime to)
        {
            from = to = DateTime.Now;
            Form dlg = new Form
            {
                Text = "Sana va soatni tanlang",
                Width = 350,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = C_BG
            };
            var cal = new MonthCalendar
            {
                Location = new Point(16, 12),
                MaxSelectionCount = 1,
                ShowToday = true, ShowTodayCircle = true
            };
            cal.SelectionStart = cal.SelectionEnd = DateTime.Today;

            var lbl = new Label
            {
                Text = "Qaysi soatdan boshlab:",
                Font = new Font("Segoe UI", 10),
                ForeColor = C_DARK,
                AutoSize = true
            };
            lbl.Location = new Point(16, cal.Bottom + 10);

            var dtpTime = new DateTimePicker
            {
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Width = 316,
                Value = DateTime.Today
            };
            dtpTime.Location = new Point(16, lbl.Bottom + 4);

            var btn = new Button
            {
                Text = "Hisobot chiqarish",
                Width = 316, Height = 44,
                BackColor = C_GREEN, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btn.Location = new Point(16, dtpTime.Bottom + 14);
            btn.FlatAppearance.BorderSize = 0;
            dlg.Height = btn.Bottom + 52;

            bool ok = false;
            DateTime f = DateTime.Now, t = DateTime.Now;
            btn.Click += (s, e) =>
            {
                DateTime date = cal.SelectionStart.Date;
                f = date + dtpTime.Value.TimeOfDay;
                t = date.AddDays(1).AddTicks(-1);
                ok = true;
                dlg.Close();
            };
            dlg.Controls.AddRange(new Control[] { cal, lbl, dtpTime, btn });
            dlg.ShowDialog(this);
            if (ok) { from = f; to = t; }
            return ok;
        }

        List<RL> BuildReportLines(string cafe, DateTime from, DateTime to)
        {
            var L = new List<RL>();
            try
            {
                var db = new dbconnect();
                db.OpenCon();

                // ── Header ────────────────────────────────────────────────
                L.Add(RL.Hdr(cafe, true));
                L.Add(RL.Hdr("KUNLIK HISOBOT", false));
                L.Add(RL.Sep());
                L.Add(RL.Hdr(from.ToString("yyyy.MM.dd"), true));
                L.Add(RL.Row("Soat:", from.ToString("HH:mm") + " dan"));
                L.Add(RL.Sep());

                // ── Summary ───────────────────────────────────────────────
                int cnt = 0;
                decimal total = 0, feeP = 0, foodP = 0, placeP = 0;

                using (var cmd = new SqlCommand(
                    "SELECT COUNT(*), ISNULL(SUM(total),0) FROM [order] WHERE paid='YES' AND created_at BETWEEN @f AND @t", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@f", from);
                    cmd.Parameters.AddWithValue("@t", to);
                    using (var dr = cmd.ExecuteReader())
                    { if (dr.Read()) { cnt = Convert.ToInt32(dr[0]); total = Convert.ToDecimal(dr[1]); } }
                }

                bool feePFromDb = false;
                try
                {
                    using (var cmd = new SqlCommand(
                        "SELECT ISNULL(SUM(fee_amount),0) FROM [order] WHERE paid='YES' AND created_at BETWEEN @f AND @t", db.GetCon()))
                    { cmd.Parameters.AddWithValue("@f", from); cmd.Parameters.AddWithValue("@t", to); feeP = Convert.ToDecimal(cmd.ExecuteScalar()); feePFromDb = true; }
                }
                catch { }
                if (!feePFromDb)
                {
                    decimal svcPct = 0;
                    decimal.TryParse(PrintService.GetSetting("service_charge", "0"), out svcPct);
                    if (svcPct > 0) feeP = Math.Round(total * svcPct / 100);
                }

                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM((f.selling_price - ISNULL(f.original_price,0)) * ofd.quantity), 0)
                        FROM [order] o
                        JOIN order_food ofd ON ofd.order_id = o.id
                        JOIN food f ON f.id = ofd.food_id
                        WHERE o.paid='YES' AND o.created_at BETWEEN @f AND @t", db.GetCon()))
                    { cmd.Parameters.AddWithValue("@f", from); cmd.Parameters.AddWithValue("@t", to); foodP = Convert.ToDecimal(cmd.ExecuteScalar()); }
                }
                catch { }

                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(pi.price), 0)
                        FROM [order] o JOIN place_in pi ON pi.id = o.place_id
                        WHERE o.paid='YES' AND o.created_at BETWEEN @f AND @t", db.GetCon()))
                    { cmd.Parameters.AddWithValue("@f", from); cmd.Parameters.AddWithValue("@t", to); placeP = Convert.ToDecimal(cmd.ExecuteScalar()); }
                }
                catch { }

                decimal jami = feeP + foodP + placeP;

                L.Add(RL.Row("Buyurtmalar:",                   cnt.ToString(),  true));
                L.Add(RL.Row("Foiz (%)lardan sof daromad:",    FmtP(feeP)));
                L.Add(RL.Row("Mahsulotlardan sof daromad:",    FmtP(foodP)));
                L.Add(RL.Row("Joylar uchun narxdan daromad:",  FmtP(placeP)));
                L.Add(RL.Row("Jami foyda:",                    FmtP(jami),   true));
                L.Add(RL.Row("Jami summa:",                    FmtP(total),  true));
                L.Add(RL.Sep());

                // ── Payment breakdown ─────────────────────────────────────
                using (var cmd = new SqlCommand(@"
                    SELECT p.name, ISNULL(SUM(o.total), 0) AS tot
                    FROM payment p
                    LEFT JOIN [order] o ON o.payment_id = p.id
                        AND o.paid='YES' AND o.created_at BETWEEN @f AND @t
                    GROUP BY p.id, p.name ORDER BY tot DESC", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@f", from);
                    cmd.Parameters.AddWithValue("@t", to);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            decimal ptot = Convert.ToDecimal(dr["tot"]);
                            if (ptot > 0)
                                L.Add(RL.Row(dr["name"] + ":", FmtP(ptot)));
                        }
                    }
                }

                // ── Per-user ──────────────────────────────────────────────
                L.Add(RL.Sep());
                L.Add(RL.Sep());

                // Try with fee_amount; if column missing, fall back
                string userSql = @"
                    SELECT u.name, ISNULL(uc.name,'') AS role,
                           COUNT(o.id) AS cnt,
                           ISNULL(SUM(o.total), 0) AS total,
                           ISNULL(SUM(o.fee_amount), 0) AS fee
                    FROM [user] u
                    JOIN user_category uc ON uc.id = u.user_category_id
                    LEFT JOIN [order] o ON o.user_id = u.id
                        AND o.paid='YES' AND o.created_at BETWEEN @f AND @t
                    GROUP BY u.id, u.name, uc.name
                    HAVING COUNT(o.id) > 0
                    ORDER BY total DESC";
                bool hasFee = true;
                try
                {
                    using (var cmd = new SqlCommand(userSql, db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@f", from);
                        cmd.Parameters.AddWithValue("@t", to);
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string uname = dr["name"].ToString(), role = dr["role"].ToString();
                                int ucnt = Convert.ToInt32(dr["cnt"]);
                                decimal utotal = Convert.ToDecimal(dr["total"]);
                                decimal ufee   = Convert.ToDecimal(dr["fee"]);
                                L.Add(RL.Hdr(uname + ", " + role, true));
                                L.Add(RL.Sep());
                                L.Add(RL.Row("Buyurtmalar:",                ucnt.ToString(), true));
                                L.Add(RL.Row("Foiz (%)lardan sof daromad:", FmtP(ufee)));
                                L.Add(RL.Row("Jami summa:",                 FmtP(utotal)));
                                L.Add(RL.Txt(""));
                            }
                        }
                    }
                }
                catch
                {
                    hasFee = false;
                }

                if (!hasFee)
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT u.name, ISNULL(uc.name,'') AS role,
                               COUNT(o.id) AS cnt,
                               ISNULL(SUM(o.total), 0) AS total
                        FROM [user] u
                        JOIN user_category uc ON uc.id = u.user_category_id
                        LEFT JOIN [order] o ON o.user_id = u.id
                            AND o.paid='YES' AND o.created_at BETWEEN @f AND @t
                        GROUP BY u.id, u.name, uc.name
                        HAVING COUNT(o.id) > 0
                        ORDER BY total DESC", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@f", from);
                        cmd.Parameters.AddWithValue("@t", to);
                        using (var dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                string uname = dr["name"].ToString(), role = dr["role"].ToString();
                                int ucnt = Convert.ToInt32(dr["cnt"]);
                                decimal utotal = Convert.ToDecimal(dr["total"]);
                                L.Add(RL.Hdr(uname + ", " + role, true));
                                L.Add(RL.Sep());
                                L.Add(RL.Row("Buyurtmalar:", ucnt.ToString(), true));
                                L.Add(RL.Row("Jami summa:",  FmtP(utotal)));
                                L.Add(RL.Txt(""));
                            }
                        }
                    }
                }

                // ── Per-food ──────────────────────────────────────────────
                L.Add(RL.Sep());

                using (var cmd = new SqlCommand(@"
                    SELECT f.name, fc.name AS cat,
                           SUM(ofd.quantity) AS qty,
                           SUM(ofd.quantity * f.selling_price) AS revenue,
                           SUM(ofd.quantity * (f.selling_price - ISNULL(f.original_price,0))) AS profit
                    FROM food f
                    JOIN food_category fc ON fc.id = f.food_category_id
                    JOIN order_food ofd ON ofd.food_id = f.id
                    JOIN [order] o ON o.id = ofd.order_id
                    WHERE o.paid='YES' AND o.created_at BETWEEN @f AND @t
                    GROUP BY f.id, f.name, fc.name
                    ORDER BY revenue DESC", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@f", from);
                    cmd.Parameters.AddWithValue("@t", to);
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string fname = dr["name"].ToString(), fcat = dr["cat"].ToString();
                            int qty = Convert.ToInt32(dr["qty"]);
                            decimal rev = Convert.ToDecimal(dr["revenue"]);
                            decimal profit = Convert.ToDecimal(dr["profit"]);
                            L.Add(RL.Hdr(fname + " (" + fcat + ")", true));
                            L.Add(RL.Sep());
                            L.Add(RL.Row("Miqdor:",     qty + " portsiya"));
                            L.Add(RL.Row("Jami summa:", FmtP(rev)));
                            L.Add(RL.Row("Jami foyda:", FmtP(profit)));
                            L.Add(RL.Txt(""));
                        }
                    }
                }

                db.CloseCon();
            }
            catch { }
            return L;
        }

        static string FmtP(decimal v) => v.ToString("N0") + " UZS";
    }
}
