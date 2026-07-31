using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.main
{
    public class KassirPage : Form
    {
        private Panel   _content;
        private Button[] _navBtns;
        private int     _active = -1;
        private Timer   _autoRefresh;
        private Action  _newOrderHandler;

        // ── Rang palitasi ────────────────────────────────────────────────────
        static readonly Color Sidebar    = Color.FromArgb(15, 23, 42);
        static readonly Color SideHov    = Color.FromArgb(30, 41, 59);
        static readonly Color SideActive = Color.FromArgb(20, 83, 45);
        static readonly Color BgPage     = Color.FromArgb(241, 245, 249);
        static readonly Color BgCard     = Color.White;
        static readonly Color TxtDark    = Color.FromArgb(15, 23, 42);
        static readonly Color TxtMuted   = Color.FromArgb(100, 116, 139);
        static readonly Color Border     = Color.FromArgb(226, 232, 240);
        static readonly Color Green      = Color.FromArgb(22, 163, 74);
        static readonly Color GreenBg    = Color.FromArgb(240, 253, 244);
        static readonly Color Red        = Color.FromArgb(220, 38, 38);
        static readonly Color RedBg      = Color.FromArgb(254, 242, 242);
        static readonly Color Gold       = Color.FromArgb(217, 119, 6);
        static readonly Color GoldBg     = Color.FromArgb(255, 251, 235);
        static readonly Color Blue       = Color.FromArgb(37, 99, 235);
        static readonly Color BlueBg     = Color.FromArgb(239, 246, 255);

        public KassirPage() { Build(); }

        // ════════════════════════════════════════════════════════════════════
        // ASOSIY LAYOUT
        // ════════════════════════════════════════════════════════════════════
        void Build()
        {
            WindowState     = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            BackColor       = BgPage;
            Text            = "FoodX — Kassir";

            Panel sidebar = new Panel { Width = UIScale.Sidebar, Dock = DockStyle.Left, BackColor = Sidebar };
            Controls.Add(sidebar);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            Controls.Add(_content);
            _content.BringToFront();

            BuildSidebar(sidebar);
            Load += (s, e) => GoTo(1);
        }

        void BuildSidebar(Panel sb)
        {
            // Logo
            Panel logo = new Panel { Height = 72, Dock = DockStyle.Top, BackColor = Color.FromArgb(10, 15, 30) };
            logo.Controls.Add(new Label
            {
                Text = "FoodX",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Green, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            sb.Controls.Add(logo);

            // Pastki qism — foydalanuvchi + chiqish
            Panel bottom = new Panel { Height = 100, Dock = DockStyle.Bottom, BackColor = Color.FromArgb(10, 15, 30) };
            bottom.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(30, 41, 59)), 16, 0, sb.Width - 16, 0);

            Panel av = new Panel { Width = 42, Height = 42, Location = new Point(16, 16), BackColor = Color.Transparent };
            av.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var br = new SolidBrush(Color.FromArgb(30, 41, 59)))
                    e.Graphics.FillEllipse(br, 0, 0, 41, 41);
                string ini = Session.UserName.Length >= 2
                    ? Session.UserName.Substring(0, 2).ToUpper()
                    : Session.UserName.ToUpper();
                using (var f  = new Font("Segoe UI", 13, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(ini, f, Brushes.White, new RectangleF(0, 0, 41, 41), sf);
            };
            bottom.Controls.Add(av);

            bottom.Controls.Add(new Label
            {
                Text = Session.UserName,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(68, 14), AutoSize = true
            });
            bottom.Controls.Add(new Label
            {
                Text = "Kassir",
                Font = new Font("Segoe UI", 8),
                ForeColor = Green,
                Location = new Point(68, 34), AutoSize = true
            });

            Button btnOut = new Button
            {
                Text      = "⏻  Chiqish",
                Location  = new Point(14, 66),
                Width     = sb.Width - 28, Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font      = new Font("Segoe UI", 9),
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnOut.FlatAppearance.BorderSize = 0;
            btnOut.MouseEnter += (s, e) => { btnOut.BackColor = Color.FromArgb(185, 28, 28); btnOut.ForeColor = Color.White; };
            btnOut.MouseLeave += (s, e) => { btnOut.BackColor = Color.FromArgb(30, 41, 59);  btnOut.ForeColor = Color.FromArgb(148, 163, 184); };
            btnOut.Click      += (s, e) => { Session.Clear(); Hide(); new Form1().Show(); };
            sb.Resize         += (s, e) => btnOut.Width = sb.Width - 28;
            bottom.Controls.Add(btnOut);
            sb.Controls.Add(bottom);

            // Nav menyu
            Panel navArea = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar };
            sb.Controls.Add(navArea);

            string[] labels = { "≡   Buyurtmalar", "⊞   Joylar" };
            _navBtns = new Button[labels.Length];
            int y = 20;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                Button b = new Button
                {
                    Text      = labels[i],
                    Location  = new Point(8, y),
                    Width     = sb.Width - 16, Height = 50,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(148, 163, 184),
                    Font      = new Font("Segoe UI", 10),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding   = new Padding(14, 0, 0, 0),
                    Cursor    = Cursors.Hand
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = SideHov;
                b.Click    += (s, e) => GoTo(idx);
                navArea.Resize += (s, e) => b.Width = navArea.Width - 16;
                navArea.Controls.Add(b);
                _navBtns[i] = b;
                y += 56;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // NAVIGATSIYA
        // ════════════════════════════════════════════════════════════════════
        void GoTo(int tab)
        {
            _autoRefresh?.Stop();
            _autoRefresh?.Dispose();
            _autoRefresh = null;
            if (_newOrderHandler != null)
            {
                SyncEngine.NewOrdersArrived -= _newOrderHandler;
                _newOrderHandler = null;
            }

            for (int i = 0; i < _navBtns.Length; i++)
            {
                bool on = (i == tab);
                _navBtns[i].BackColor = on ? SideActive : Color.Transparent;
                _navBtns[i].ForeColor = on ? Color.White : Color.FromArgb(148, 163, 184);
                _navBtns[i].Font      = new Font("Segoe UI", 10, on ? FontStyle.Bold : FontStyle.Regular);
                _navBtns[i].FlatAppearance.MouseOverBackColor = on ? SideActive : SideHov;
                _navBtns[i].Padding = new Padding(on ? 10 : 14, 0, 0, 0);
            }

            _active = tab;
            _content.Controls.Clear();

            Control page = (tab == 0) ? BuildBuyurtmalar() : BuildJoylar();
            if (page == null) return;
            page.Dock = DockStyle.Fill;
            _content.Controls.Add(page);
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB 0 — BUYURTMALAR
        // ════════════════════════════════════════════════════════════════════
        Control BuildBuyurtmalar()
        {
            var uc = new UserControl { BackColor = BgPage };

            // ── Header ───────────────────────────────────────────────────────
            Panel hdr = MkPageHeader("Buyurtmalar", uc, out Button btnRefHdr);
            btnRefHdr.Font = new Font("Segoe UI", 14);

            // ── Statistika satri ─────────────────────────────────────────────
            Panel statsRow = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = BgPage };
            Label lblOpenVal = null, lblSumVal = null, lblTodayVal = null;
            Panel st1 = MkStatCard("Ochiq buyurtmalar", "0",        Gold,  GoldBg,  out lblOpenVal);
            Panel st2 = MkStatCard("Kutilayotgan summa", "0 so'm",  Green, GreenBg, out lblSumVal);
            Panel st3 = MkStatCard("Bugun yopilgan",     "0 ta",    Blue,  BlueBg,  out lblTodayVal);
            statsRow.Controls.Add(st1);
            statsRow.Controls.Add(st2);
            statsRow.Controls.Add(st3);
            statsRow.Resize += (s, e) =>
            {
                int gap = 12;
                int total = statsRow.Width - gap * 4;
                int w = total / 3;
                st1.SetBounds(gap,           12, w, 76);
                st2.SetBounds(gap * 2 + w,   12, w, 76);
                st3.SetBounds(gap * 3 + w*2, 12, w, 76);
            };
            uc.Controls.Add(statsRow);

            // ── Filter paneli ─────────────────────────────────────────────────
            Panel fBar = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = BgCard };
            fBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0,  fBar.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, 57, fBar.Width, 57);
            };

            string[] fLabels = { "Barchasi", "Ochiq", "Yopilgan" };
            string[] fVals   = { "ALL", "NO", "YES" };
            string   filter  = "NO";
            Button[] fBtns   = new Button[3];
            int      fx      = 20;
            for (int i = 0; i < 3; i++)
            {
                Button fb = new Button
                {
                    Text      = fLabels[i],
                    Location  = new Point(fx, 14),
                    Width     = 100, Height = 30,
                    FlatStyle = FlatStyle.Flat,
                    Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                fb.FlatAppearance.BorderSize = 1;
                fBtns[i] = fb;
                fBar.Controls.Add(fb);
                fx += 108;
            }
            uc.Controls.Add(fBar);

            // ── Kartalar joyi ─────────────────────────────────────────────────
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };
            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock         = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor    = BgPage, Padding = new Padding(12)
            };
            scroll.Controls.Add(flp);
            uc.Controls.Add(scroll);

            // ── Ma'lumot yuklash ──────────────────────────────────────────────
            Action<string> load = null;
            load = (f) =>
            {
                flp.SuspendLayout();
                flp.Controls.Clear();
                try
                {
                    // Statistika uchun alohida so'rov (filterga bog'liq emas)
                    {
                        dbconnect db = new dbconnect();
                        db.OpenCon();
                        string statSql = @"
                            SELECT
                                SUM(CASE WHEN paid='NO'  THEN 1 ELSE 0 END) AS open_cnt,
                                SUM(CASE WHEN paid='NO'  THEN total ELSE 0 END) AS open_sum,
                                SUM(CASE WHEN paid='YES' AND CAST(created_at AS DATE)=CAST(GETDATE() AS DATE)
                                    THEN 1 ELSE 0 END) AS today_closed
                            FROM [order]";
                        using (var cmd = new SqlCommand(statSql, db.GetCon()))
                        using (var dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                lblOpenVal.Text  = dr["open_cnt"]    == DBNull.Value ? "0" : dr["open_cnt"].ToString();
                                lblSumVal.Text   = dr["open_sum"]    == DBNull.Value ? "0 so'm"
                                    : Convert.ToDecimal(dr["open_sum"]).ToString("N0") + " so'm";
                                lblTodayVal.Text = dr["today_closed"] == DBNull.Value ? "0 ta"
                                    : dr["today_closed"].ToString() + " ta";
                            }
                        }
                        db.CloseCon();
                    }

                    // Buyurtmalar ro'yxati
                    string where = f == "ALL" ? "" : $"AND o.paid='{f}'";
                    string sql = $@"
                        SELECT o.id, o.total, o.created_at, o.paid,
                               ISNULL(pi.room_name,'—') AS room_name,
                               ISNULL(po.name,'—')      AS zone,
                               (SELECT COUNT(*) FROM order_food WHERE order_id=o.id) AS items,
                               ISNULL(u.name,'')         AS waiter,
                               ISNULL(o.is_customer_order,0) AS is_customer_order,
                               ISNULL(o.customer_name,'')    AS customer_name
                        FROM [order] o
                        LEFT JOIN place_in  pi ON pi.id = o.place_id
                        LEFT JOIN place_out po ON po.id = pi.place_out_id
                        LEFT JOIN [user]     u ON u.id  = o.user_id
                        WHERE 1=1 {where}
                        ORDER BY o.paid ASC, o.created_at DESC";

                    DataTable dt = new DataTable();
                    using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                        da.Fill(dt);

                    if (dt.Rows.Count == 0)
                    {
                        var empty = new Label
                        {
                            Text = "Buyurtmalar topilmadi", AutoSize = true,
                            Margin = new Padding(50),
                            Font = new Font("Segoe UI", 14), ForeColor = TxtMuted
                        };
                        flp.Controls.Add(empty);
                    }
                    else
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            int     oid      = Convert.ToInt32(row["id"]);
                            decimal total    = Convert.ToDecimal(row["total"]);
                            DateTime created = Convert.ToDateTime(row["created_at"]);
                            bool    paid     = row["paid"].ToString() == "YES";
                            string  place    = row["zone"] + " — " + row["room_name"];
                            int     items    = Convert.ToInt32(row["items"]);
                            string  waiter   = row["waiter"].ToString();
                            bool    isCust   = Convert.ToInt32(row["is_customer_order"]) == 1;
                            string  custName = row["customer_name"].ToString();

                            flp.Controls.Add(
                                BuildOrderCard(oid, total, created, paid, place,
                                    items, waiter, isCust, custName, () => load(f)));
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { flp.ResumeLayout(); }
            };

            Action reStyle = () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    bool on = fVals[i] == filter;
                    fBtns[i].BackColor = on ? TxtDark : Color.FromArgb(248, 250, 252);
                    fBtns[i].ForeColor = on ? Color.White : TxtMuted;
                    fBtns[i].FlatAppearance.BorderColor = on ? TxtDark : Border;
                }
            };

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                fBtns[i].Click += (s, e) => { filter = fVals[idx]; reStyle(); load(filter); };
            }
            btnRefHdr.Click += (s, e) => load(filter);

            uc.Load += (s, e) => { reStyle(); load(filter); };
            return uc;
        }

        Panel BuildOrderCard(int oid, decimal total, DateTime created, bool paid,
            string place, int items, string waiter, bool isCust, string custName, Action reload)
        {
            Color accent = paid ? Green : (isCust ? Gold : Blue);
            Color bg     = paid ? GreenBg : (isCust ? GoldBg : BgCard);

            Panel card = new Panel
            {
                Width  = 300, Height = paid ? 152 : 190,
                Margin = new Padding(8), BackColor = bg, Cursor = Cursors.Hand
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = new Pen(Color.FromArgb(180, accent), 1f))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                using (var b = new SolidBrush(accent))
                    e.Graphics.FillRectangle(b, 0, 0, card.Width, 5);
            };

            // Sarlavha
            string title = isCust
                ? (string.IsNullOrEmpty(custName) ? "Mijoz buyurtmasi" : custName)
                : place;
            card.Controls.Add(new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TxtDark,
                Location  = new Point(14, 14),
                Width     = card.Width - 28, Height = 24
            });

            // Holat
            string statusTxt = paid ? "✓ Yopilgan" : (isCust ? "● Mijoz  ·  Ochiq" : "● Ochiq");
            card.Controls.Add(new Label
            {
                Text      = statusTxt, AutoSize = true,
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = accent, Location = new Point(14, 42)
            });

            // Ma'lumot qatori: taom soni + vaqt
            card.Controls.Add(new Label
            {
                Text      = $"{items} ta taom  ·  {created:dd.MM.yyyy  HH:mm}",
                Font      = new Font("Segoe UI", 8), ForeColor = TxtMuted,
                Location  = new Point(14, 62), Width = card.Width - 28, Height = 18
            });

            // Ofitsiant (faqat agar bor bo'lsa)
            int yNext = 82;
            if (!isCust && !string.IsNullOrWhiteSpace(waiter))
            {
                card.Controls.Add(new Label
                {
                    Text      = "Ofitsiant: " + waiter,
                    Font      = new Font("Segoe UI", 8), ForeColor = TxtMuted,
                    Location  = new Point(14, yNext), Width = card.Width - 28, Height = 18
                });
                yNext += 20;
            }

            // Summa — katta shrift
            card.Controls.Add(new Label
            {
                Text      = total.ToString("N0") + " so'm",
                Font      = new Font("Segoe UI", 17, FontStyle.Bold),
                ForeColor = accent,
                Location  = new Point(14, yNext),
                Width     = card.Width - 28, Height = 32
            });

            // Tahrirlash tugmasi (faqat ochiq buyurtmalar uchun)
            if (!paid)
            {
                Button btn = new Button
                {
                    Text      = "Ko'rish / Tahrirlash",
                    Location  = new Point(14, card.Height - 42),
                    Width     = card.Width - 28, Height = 34,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = accent, ForeColor = Color.White,
                    Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor    = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                int oId = oid;
                btn.Click  += (s, e) => { new AddOrder(0, oId, Session.Login).ShowDialog(); reload?.Invoke(); };
                card.Click += (s, e) => { new AddOrder(0, oid, Session.Login).ShowDialog(); reload?.Invoke(); };
                card.Controls.Add(btn);
            }

            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB 1 — JOYLAR
        // ════════════════════════════════════════════════════════════════════
        Control BuildJoylar()
        {
            var uc = new UserControl { BackColor = BgPage };

            // ── Header ───────────────────────────────────────────────────────
            Panel hdr = MkPageHeader("Joylar", uc, out Button btnRef);

            // ── Statistika: bo'sh / band ──────────────────────────────────────
            Panel statsRow = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = BgPage };
            Label lblBoshVal = null, lblBandVal = null;
            Panel sj1 = MkStatCard("Bo'sh joylar", "0", Green, GreenBg, out lblBoshVal);
            Panel sj2 = MkStatCard("Band joylar",  "0", Red,   RedBg,   out lblBandVal);
            statsRow.Controls.Add(sj1);
            statsRow.Controls.Add(sj2);
            statsRow.Resize += (s, e) =>
            {
                int w = (statsRow.Width - 36) / 2;
                sj1.SetBounds(12,        10, w, 68);
                sj2.SetBounds(12 + w + 12, 10, w, 68);
            };
            uc.Controls.Add(statsRow);

            // ── Izoh paneli ───────────────────────────────────────────────────
            Panel legend = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BgCard };
            legend.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0,  legend.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, 39, legend.Width, 39);
            };
            LegDot(legend, Green, "Bo'sh",  18);
            LegDot(legend, Red,   "Band",  100);
            LegDot(legend, Gold,  "Aktiv buyurtma", 168);
            uc.Controls.Add(legend);

            // ── Scroll joyi ───────────────────────────────────────────────────
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };
            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock         = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor    = BgPage, Padding = new Padding(14, 8, 14, 14)
            };
            scroll.Controls.Add(flp);
            uc.Controls.Add(scroll);

            Action load = null;
            load = () =>
            {
                flp.SuspendLayout();
                flp.Controls.Clear();
                try
                {
                    string sql = @"
                        SELECT po.name AS zone, pi.id AS tid, pi.room_name, pi.empty,
                               (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS cnt
                        FROM place_out po
                        JOIN place_category pc ON pc.id = po.place_category_id
                        JOIN place_in pi ON pi.place_out_id = po.id
                        ORDER BY ISNULL(po.sort_order,9999), po.name,
                            TRY_CAST(SUBSTRING(pi.room_name,1,
                                PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT),
                            pi.room_name";

                    DataTable dt = new DataTable();
                    using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                        da.Fill(dt);

                    // Statistika
                    int boshCnt = 0, bandCnt = 0;
                    foreach (DataRow r in dt.Rows)
                    {
                        bool isEmpty = r["empty"].ToString().ToUpper() == "YES"
                                       && Convert.ToInt32(r["cnt"]) == 0;
                        if (isEmpty) boshCnt++; else bandCnt++;
                    }
                    if (lblBoshVal != null) lblBoshVal.Text = boshCnt.ToString();
                    if (lblBandVal != null) lblBandVal.Text = bandCnt.ToString();

                    if (dt.Rows.Count == 0)
                    {
                        flp.Controls.Add(new Label
                        {
                            Text = "Hech qanday stol topilmadi", AutoSize = true,
                            Margin = new Padding(50),
                            Font = new Font("Segoe UI", 14), ForeColor = TxtMuted
                        });
                    }
                    else
                    {
                        string curZone = "";
                        foreach (DataRow row in dt.Rows)
                        {
                            string zone  = row["zone"].ToString();
                            int    tid   = Convert.ToInt32(row["tid"]);
                            string rname = row["room_name"].ToString();
                            int    cnt   = Convert.ToInt32(row["cnt"]);
                            bool   empty = row["empty"].ToString().ToUpper() == "YES" && cnt == 0;

                            if (zone != curZone)
                            {
                                curZone = zone;
                                flp.Controls.Add(MkZoneHeader(zone, flp));
                            }
                            flp.Controls.Add(TableCard(tid, rname, empty, cnt, () => load?.Invoke()));
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { flp.ResumeLayout(); }
            };

            btnRef.Click += (s, e) => load();
            uc.Load      += (s, e) => load();

            _autoRefresh = new Timer { Interval = 3000 };
            _autoRefresh.Tick += (s, e) => { if (_active == 1) load(); };
            _autoRefresh.Start();

            // Yangi buyurtma tushganda darhol yangilanish (3 soniyani kutmasdan)
            _newOrderHandler = () =>
            {
                if (_active == 1 && !IsDisposed)
                    try { BeginInvoke(new Action(load)); } catch { }
            };
            SyncEngine.NewOrdersArrived += _newOrderHandler;

            return uc;
        }

        Panel TableCard(int tableId, string name, bool empty, int cnt, Action reload)
        {
            Color accent   = empty ? Green : (cnt > 0 ? Gold : Red);
            Color bg       = empty ? GreenBg : (cnt > 0 ? GoldBg : RedBg);
            string status  = empty ? "● Bo'sh" : "● Band";
            string btnText = empty ? "+ Yangi zakaz" : "Ko'rish";

            Panel card = new Panel
            {
                Width  = 160, Height = 170,
                Margin = new Padding(6), BackColor = bg, Cursor = Cursors.Hand
            };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(160, accent), 1.5f))
                    e.Graphics.DrawRectangle(pen, 1, 1, card.Width - 3, card.Height - 3);
                using (var br = new SolidBrush(accent))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 6);
            };

            // Stol nomi — katta
            card.Controls.Add(new Label
            {
                Text      = name,
                Font      = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TxtDark, TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(6, 14), Width = card.Width - 12, Height = 32
            });

            // Holat
            card.Controls.Add(new Label
            {
                Text      = status, AutoSize = false,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = accent, TextAlign = ContentAlignment.MiddleCenter,
                Location  = new Point(6, 50), Width = card.Width - 12, Height = 22
            });

            // Aktiv buyurtmalar soni
            if (cnt > 0)
            {
                card.Controls.Add(new Label
                {
                    Text      = cnt + " ta aktiv",
                    Font      = new Font("Segoe UI", 8), ForeColor = TxtMuted,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location  = new Point(6, 72), Width = card.Width - 12, Height = 18
                });
            }

            // Tugma
            Button btn = new Button
            {
                Text      = btnText,
                Location  = new Point(10, card.Height - 44),
                Width     = card.Width - 20, Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = accent, ForeColor = Color.White,
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btn);

            EventHandler open = (s, e) =>
            {
                try
                {
                    int existId = 0;
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 id FROM [order] WHERE place_id=@p AND paid='NO'", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@p", tableId);
                        object r = cmd.ExecuteScalar();
                        if (r != null) existId = Convert.ToInt32(r);
                    }
                    db.CloseCon();
                    new AddOrder(tableId, existId, Session.Login).ShowDialog();
                    reload?.Invoke();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };
            card.Click += open;
            btn.Click  += open;

            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        // HELPER METODLAR
        // ════════════════════════════════════════════════════════════════════

        // Sahifa sarlavhasi (header panel)
        static Panel MkPageHeader(string title, UserControl owner, out Button refreshBtn)
        {
            Panel h = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = BgCard };
            h.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 69, h.Width, 69);
            h.Controls.Add(new Label
            {
                Text = title, Font = new Font("Segoe UI", 19, FontStyle.Bold),
                ForeColor = TxtDark, AutoSize = true, Location = new Point(24, 18)
            });

            Button rb = new Button
            {
                Text = "↻", Width = 38, Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = TxtDark, Cursor = Cursors.Hand
            };
            rb.FlatAppearance.BorderSize  = 1;
            rb.FlatAppearance.BorderColor = Border;
            h.Resize += (s, e) => rb.Location = new Point(h.Width - 56, 16);
            h.Controls.Add(rb);

            owner.Controls.Add(h);
            refreshBtn = rb;
            return h;
        }

        // Statistika kartasi
        static Panel MkStatCard(string label, string initVal, Color accent, Color bg, out Label valLbl)
        {
            Panel p = new Panel { BackColor = bg };
            p.Paint += (s, e) =>
            {
                e.Graphics.DrawRectangle(new Pen(Color.FromArgb(60, accent)), 0, 0, p.Width - 1, p.Height - 1);
                using (var b = new SolidBrush(accent))
                    e.Graphics.FillRectangle(b, 0, 0, 5, p.Height);
            };
            p.Controls.Add(new Label
            {
                Text = label, Font = new Font("Segoe UI", 8), ForeColor = TxtMuted,
                Location = new Point(14, 8), AutoSize = true
            });
            valLbl = new Label
            {
                Text = initVal,
                Font = new Font("Segoe UI", 19, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(14, 28), Width = 300, Height = 34
            };
            p.Controls.Add(valLbl);
            return p;
        }

        // Zona sarlavhasi (FlowLayoutPanel ichida)
        static Panel MkZoneHeader(string title, FlowLayoutPanel flp)
        {
            Panel p = new Panel { Height = 46, Margin = new Padding(4, 18, 4, 2), BackColor = Color.Transparent };
            p.Width = flp.Width > 60 ? flp.Width - 60 : 600;
            flp.Resize += (s, e) => { if (flp.Width > 60) p.Width = flp.Width - 60; };

            p.Controls.Add(new Label
            {
                Text = title, Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TxtDark, AutoSize = true, Location = new Point(2, 4)
            });
            p.Controls.Add(new Panel { Height = 2, BackColor = Border, Dock = DockStyle.Bottom });
            return p;
        }

        // Legenda nuqtasi
        static void LegDot(Panel parent, Color c, string txt, int x)
        {
            Panel d = new Panel { Width = 12, Height = 12, Location = new Point(x, 14), BackColor = Color.Transparent };
            d.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.FillEllipse(new SolidBrush(c), 0, 0, 11, 11); };
            parent.Controls.Add(d);
            parent.Controls.Add(new Label { Text = txt, Font = new Font("Segoe UI", 9), ForeColor = TxtMuted, AutoSize = true, Location = new Point(x + 16, 11) });
        }

        protected override void Dispose(bool disposing)
        {
            _autoRefresh?.Stop();
            _autoRefresh?.Dispose();
            if (_newOrderHandler != null)
            {
                SyncEngine.NewOrdersArrived -= _newOrderHandler;
                _newOrderHandler = null;
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new Size(1400, 800);
            this.Name = "KassirPage";
            this.ResumeLayout(false);
        }
    }
}
