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

        static readonly Color Sidebar  = Color.FromArgb(24, 32, 48);
        static readonly Color SideHov  = Color.FromArgb(38, 50, 72);
        static readonly Color NavAct   = Color.FromArgb(22, 163, 74);
        static readonly Color BgPage   = Color.FromArgb(245, 246, 250);
        static readonly Color BgCard   = Color.White;
        static readonly Color TxtDark  = Color.FromArgb(17, 24, 39);
        static readonly Color TxtMuted = Color.FromArgb(107, 114, 128);
        static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        static readonly Color Green    = Color.FromArgb(22, 163, 74);
        static readonly Color Red      = Color.FromArgb(220, 38, 38);
        static readonly Color Blue     = Color.FromArgb(59, 130, 246);
        static readonly Color Border   = Color.FromArgb(229, 231, 235);

        public KassirPage() { Build(); }

        // ════════════════════════════════════════════════════════════════════
        // LAYOUT
        // ════════════════════════════════════════════════════════════════════
        void Build()
        {
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = BgPage;
            Text = "FoodX — Kassir";

            Panel sidebar = new Panel { Width = 220, Dock = DockStyle.Left, BackColor = Sidebar };
            this.Controls.Add(sidebar);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            this.Controls.Add(_content);
            _content.BringToFront();

            BuildSidebar(sidebar);
            this.Load += (s, e) => GoTo(1);   // default: Joylar
        }

        void BuildSidebar(Panel sb)
        {
            // ── Logo ─────────────────────────────────────────────────────────
            Panel logo = new Panel { Height = 68, Dock = DockStyle.Top, BackColor = Sidebar };
            logo.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(45, 58, 80)), 0, 67, sb.Width, 67);
            logo.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
            });
            sb.Controls.Add(logo);

            // ── Bottom: user + logout ─────────────────────────────────────────
            Panel bottom = new Panel { Height = 108, Dock = DockStyle.Bottom, BackColor = Sidebar };
            bottom.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(45, 58, 80)), 0, 0, sb.Width, 0);

            // avatar circle
            Panel av = new Panel { Width = 36, Height = 36, Location = new Point(14, 12), BackColor = Color.Transparent };
            av.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(50, 63, 90)), 0, 0, 35, 35);
                string ini = Session.UserName.Length >= 2
                    ? Session.UserName.Substring(0, 2).ToUpper()
                    : Session.UserName.ToUpper();
                using (var f  = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(ini, f, Brushes.White, new RectangleF(0, 0, 35, 35), sf);
            };
            bottom.Controls.Add(av);
            bottom.Controls.Add(new Label
            {
                Text = Session.UserName, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 220, 235), Location = new Point(58, 12), AutoSize = true
            });
            bottom.Controls.Add(new Label
            {
                Text = "Kassir", Font = new Font("Segoe UI", 8),
                ForeColor = Green, Location = new Point(58, 30), AutoSize = true
            });

            Button btnOut = SbBtn("  ⏻  Chiqish", 62);
            btnOut.ForeColor = TxtMuted;
            btnOut.MouseEnter += (s, e) => { btnOut.BackColor = Color.FromArgb(190, 30, 30); btnOut.ForeColor = Color.White; };
            btnOut.MouseLeave += (s, e) => { btnOut.BackColor = Color.Transparent; btnOut.ForeColor = TxtMuted; };
            btnOut.Click += (s, e) => { Session.Clear(); Hide(); new Form1().Show(); };
            sb.Resize += (s, e) => btnOut.Width = sb.Width;
            bottom.Controls.Add(btnOut);
            sb.Controls.Add(bottom);

            // ── Nav buttons (absolute so both always visible) ─────────────────
            string[] labels = { "≡  Buyurtmalar", "⊞  Joylar" };
            _navBtns = new Button[2];

            Panel navArea = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar };
            sb.Controls.Add(navArea);

            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                Button b = SbBtn(labels[i], i * 52);
                b.ForeColor = Color.FromArgb(190, 200, 220);
                b.Click += (s, e) => GoTo(idx);
                navArea.Resize += (s, e) => b.Width = navArea.Width;
                navArea.Controls.Add(b);
                _navBtns[i] = b;
            }
        }

        Button SbBtn(string text, int y)
        {
            Button b = new Button
            {
                Text = text, Location = new Point(0, y),
                Width = 220, Height = 50,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = SideHov;
            b.FlatAppearance.MouseDownBackColor = NavAct;
            return b;
        }

        // ════════════════════════════════════════════════════════════════════
        // NAVIGATION
        // ════════════════════════════════════════════════════════════════════
        void GoTo(int tab)
        {
            _autoRefresh?.Stop();
            _autoRefresh?.Dispose();
            _autoRefresh = null;

            for (int i = 0; i < _navBtns.Length; i++)
            {
                bool on = i == tab;
                _navBtns[i].BackColor = on ? NavAct : Color.Transparent;
                _navBtns[i].ForeColor = on ? Color.White : Color.FromArgb(190, 200, 220);
                _navBtns[i].Font      = new Font("Segoe UI", 10, on ? FontStyle.Bold : FontStyle.Regular);
                _navBtns[i].FlatAppearance.MouseOverBackColor = on ? NavAct : SideHov;
            }

            _active = tab;
            _content.Controls.Clear();

            Control page = tab == 0 ? BuildBuyurtmalar() : BuildJoylar();
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

            // Header
            Panel hdr = Hdr("Buyurtmalar", uc);

            // Stats row
            Panel statsRow = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = BgPage };
            Label lblOpenVal = null, lblSumVal = null;
            Panel sc1 = MkStat("Ochiq zakaslar",      "0",        Gold,  out lblOpenVal);
            Panel sc2 = MkStat("Kutilayotgan summa",  "0 so'm",   Green, out lblSumVal);
            sc1.Location = new Point(20, 14); sc1.Width = 210;
            sc2.Location = new Point(244, 14); sc2.Width = 260;
            statsRow.Controls.Add(sc1);
            statsRow.Controls.Add(sc2);
            uc.Controls.Add(statsRow);

            // Filter bar
            Panel fBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = BgCard };
            fBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 51, fBar.Width, 51);
            string[] fLabels = { "Barchasi", "Ochiq", "Yopilgan" };
            string[] fVals   = { "ALL", "NO", "YES" };
            string filter    = "NO";
            Button[] fBtns   = new Button[3];
            int fx = 16;
            for (int i = 0; i < 3; i++)
            {
                Button fb = new Button
                {
                    Text = fLabels[i], Location = new Point(fx, 11),
                    Width = (i == 0 ? 90 : 76), Height = 30,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
                };
                fb.FlatAppearance.BorderSize = 1;
                fb.FlatAppearance.BorderColor = Border;
                fBtns[i] = fb;
                fBar.Controls.Add(fb);
                fx += fb.Width + 6;
            }
            uc.Controls.Add(fBar);

            // Cards scroll area
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };
            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgPage, Padding = new Padding(10)
            };
            scroll.Controls.Add(flp);
            uc.Controls.Add(scroll);

            // Load
            Action<string> load = null;
            load = (f) =>
            {
                flp.SuspendLayout();
                flp.Controls.Clear();
                try
                {
                    string where = f == "ALL" ? "" : $"AND o.paid='{f}'";
                    string sql = $@"
                        SELECT o.id, o.total, o.created_at, o.paid,
                               pi.room_name, po.name AS zone,
                               (SELECT COUNT(*) FROM order_food WHERE order_id=o.id) AS items,
                               u.fullname AS waiter
                        FROM [order] o
                        JOIN place_in pi ON pi.id = o.place_id
                        JOIN place_out po ON po.id = pi.place_out_id
                        LEFT JOIN [user] u ON u.id = o.user_id
                        WHERE 1=1 {where}
                        ORDER BY o.paid ASC, o.created_at DESC";

                    DataTable dt = new DataTable();
                    using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                        da.Fill(dt);

                    // stats
                    int openCnt = 0; decimal openSum = 0;
                    foreach (DataRow dr in dt.Rows)
                        if (dr["paid"].ToString() == "NO") { openCnt++; openSum += Convert.ToDecimal(dr["total"]); }
                    lblOpenVal.Text = openCnt.ToString();
                    lblSumVal.Text  = openSum.ToString("N0") + " so'm";

                    if (dt.Rows.Count == 0)
                    {
                        flp.Controls.Add(new Label
                        {
                            Text = "Buyurtmalar topilmadi", AutoSize = true, Margin = new Padding(40),
                            Font = new Font("Segoe UI", 12), ForeColor = TxtMuted
                        });
                    }
                    else
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            int    oid    = Convert.ToInt32(row["id"]);
                            decimal total = Convert.ToDecimal(row["total"]);
                            DateTime dt2  = Convert.ToDateTime(row["created_at"]);
                            bool   paid   = row["paid"].ToString() == "YES";
                            string place  = row["zone"].ToString() + " — " + row["room_name"].ToString();
                            int    items  = Convert.ToInt32(row["items"]);
                            string waiter = row["waiter"].ToString();
                            Color  clr    = paid ? Green : Gold;

                            Panel card = new Panel
                            {
                                Width = 270, Height = paid ? 150 : 186,
                                Margin = new Padding(8), BackColor = BgCard, Cursor = Cursors.Hand
                            };
                            card.Paint += (s2, e2) =>
                            {
                                e2.Graphics.DrawRectangle(new Pen(Border), 0, 0, card.Width - 1, card.Height - 1);
                                e2.Graphics.FillRectangle(new SolidBrush(clr), 0, 0, card.Width, 4);
                            };

                            Lbl(card, place, 14, 12, 242, new Font("Segoe UI", 11, FontStyle.Bold), TxtDark);
                            Lbl(card, paid ? "✓ Yopilgan" : "● Ochiq", 14, 38, 0, new Font("Segoe UI", 9, FontStyle.Bold), clr, true);
                            Lbl(card, items + " ta taom  •  " + dt2.ToString("dd.MM.yyyy  HH:mm"), 14, 58, 242, new Font("Segoe UI", 9), TxtMuted);
                            if (!string.IsNullOrWhiteSpace(waiter))
                                Lbl(card, "Ofitsiant: " + waiter, 14, 76, 242, new Font("Segoe UI", 8), TxtMuted);
                            Lbl(card, total.ToString("N0") + " so'm", 14, 98, 242, new Font("Segoe UI", 15, FontStyle.Bold), clr);

                            if (!paid)
                            {
                                Button bv = CardBtn("Ko'rish / Tahrirlash", Blue, 14, 136, 242, 34);
                                int oId = oid;
                                bv.Click += (s2, e2) => { new AddOrder(0, oId, Session.Login).ShowDialog(); load(filter); };
                                card.Controls.Add(bv);
                            }

                            flp.Controls.Add(card);
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                finally { flp.ResumeLayout(); }
            };

            // filter style
            Action reStyle = () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    bool on = fVals[i] == filter;
                    fBtns[i].BackColor = on ? Gold : Color.FromArgb(243, 244, 246);
                    fBtns[i].ForeColor = on ? Color.White : TxtMuted;
                }
            };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                fBtns[i].Click += (s, e) => { filter = fVals[idx]; reStyle(); load(filter); };
            }

            uc.Load += (s, e) => { reStyle(); load(filter); };
            return uc;
        }

        // ════════════════════════════════════════════════════════════════════
        // TAB 1 — JOYLAR
        // ════════════════════════════════════════════════════════════════════
        Control BuildJoylar()
        {
            var uc = new UserControl { BackColor = BgPage };

            Panel hdr = Hdr("Joylar", uc);

            // Refresh button in header
            Button btnRef = new Button
            {
                Text = "↻", Width = 38, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TxtDark, Font = new Font("Segoe UI", 15), Cursor = Cursors.Hand
            };
            btnRef.FlatAppearance.BorderSize = 0;
            hdr.Resize += (s, e) => btnRef.Location = new Point(hdr.Width - 50, 15);
            hdr.Controls.Add(btnRef);

            // Legend
            Panel leg = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BgCard };
            leg.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 39, leg.Width, 39);
            LegDot(leg, Green, "Bo'sh", 18);
            LegDot(leg, Red,   "Band",  120);
            uc.Controls.Add(leg);

            // Scroll + FlowLayout
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = BgPage, AutoScroll = true };
            FlowLayoutPanel flp = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgPage, Padding = new Padding(10)
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
                        ORDER BY ISNULL(po.sort_order,9999), po.name, TRY_CAST(SUBSTRING(pi.room_name,1,PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT), pi.room_name";

                    DataTable dt = new DataTable();
                    using (var da = new SqlDataAdapter(sql, new dbconnect().GetCon()))
                        da.Fill(dt);

                    if (dt.Rows.Count == 0)
                    {
                        flp.Controls.Add(new Label
                        {
                            Text = "Hech qanday stol topilmadi", AutoSize = true, Margin = new Padding(40),
                            Font = new Font("Segoe UI", 13), ForeColor = TxtMuted
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
                                flp.Controls.Add(ZoneHeader(zone, flp));
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
            _autoRefresh.Tick += (s, e) => load();
            _autoRefresh.Start();

            return uc;
        }

        Panel TableCard(int tableId, string name, bool empty, int cnt, Action reload)
        {
            Color accent = empty ? Green : Red;
            Color bg     = empty ? BgCard : Color.FromArgb(255, 245, 245);
            string status = empty ? "Bo'sh" : "Band";
            string btnTxt = empty ? "Zakaz qo'shish" : "Ko'rish / Tahrirlash";
            Color  btnClr = empty ? Green : Gold;

            Panel card = new Panel { Width = 162, Height = 152, Margin = new Padding(8), BackColor = bg, Cursor = Cursors.Hand };
            card.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(bg), card.ClientRectangle);
                e.Graphics.DrawRectangle(new Pen(accent, 2f), 1, 1, card.Width - 3, card.Height - 3);
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, card.Width, 5);
            };

            Lbl(card, name,   10, 18, 142, new Font("Segoe UI", 13, FontStyle.Bold), TxtDark, false, ContentAlignment.MiddleCenter);
            Lbl(card, status, 10, 46, 142, new Font("Segoe UI", 9,  FontStyle.Bold), accent,  false, ContentAlignment.MiddleCenter);
            if (!empty && cnt > 0)
                Lbl(card, cnt + " ta buyurtma", 10, 66, 142, new Font("Segoe UI", 8), TxtMuted, false, ContentAlignment.MiddleCenter);

            Button btn = CardBtn(btnTxt, btnClr, 12, 108, 138, 32);
            card.Controls.Add(btn);

            EventHandler open = (s, e) =>
            {
                try
                {
                    int existId = 0;
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    using (var cmd = new SqlCommand("SELECT TOP 1 id FROM [order] WHERE place_id=@p AND paid='NO'", db.GetCon()))
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
        // SMALL HELPERS
        // ════════════════════════════════════════════════════════════════════
        static Panel Hdr(string title, UserControl owner)
        {
            Panel h = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgCard };
            h.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, 63, h.Width, 63);
            h.Controls.Add(new Label
            {
                Text = title, Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = TxtDark, AutoSize = true, Location = new Point(24, 16)
            });
            owner.Controls.Add(h);
            return h;
        }

        static Panel MkStat(string label, string initVal, Color valClr, out Label valLbl)
        {
            Panel p = new Panel { Height = 56, BackColor = BgCard };
            p.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, p.Width - 1, p.Height - 1);

            p.Controls.Add(new Label
            {
                Text = label, Font = new Font("Segoe UI", 9), ForeColor = TxtMuted,
                Location = new Point(12, 7), AutoSize = true
            });
            valLbl = new Label
            {
                Text = initVal, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = valClr,
                Location = new Point(12, 26), Width = 230, Height = 25, AutoSize = false
            };
            p.Controls.Add(valLbl);
            return p;
        }

        static Panel ZoneHeader(string title, FlowLayoutPanel flp)
        {
            Panel p = new Panel { Height = 38, Margin = new Padding(4, 14, 4, 2), BackColor = Color.Transparent };
            p.Width = flp.Width > 48 ? flp.Width - 48 : 600;
            flp.Resize += (s, e) => { if (flp.Width > 48) p.Width = flp.Width - 48; };
            p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TxtDark, AutoSize = true, Location = new Point(0, 6) });
            p.Controls.Add(new Panel { Height = 2, BackColor = Gold, Dock = DockStyle.Bottom });
            return p;
        }

        static void LegDot(Panel parent, Color c, string txt, int x)
        {
            Panel d = new Panel { Width = 12, Height = 12, Location = new Point(x, 14), BackColor = Color.Transparent };
            d.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.FillEllipse(new SolidBrush(c), 0, 0, 11, 11); };
            parent.Controls.Add(d);
            parent.Controls.Add(new Label { Text = txt, Font = new Font("Segoe UI", 9), ForeColor = TxtMuted, AutoSize = true, Location = new Point(x + 16, 12) });
        }

        static void Lbl(Panel p, string text, int x, int y, int w,
            Font font, Color fore, bool autoW = false, ContentAlignment align = ContentAlignment.TopLeft)
        {
            Label l = new Label
            {
                Text = text, Font = font, ForeColor = fore,
                Location = new Point(x, y), AutoSize = autoW,
                TextAlign = align
            };
            if (!autoW) { l.Width = w; l.Height = font.Height + 4; }
            p.Controls.Add(l);
        }

        static Button CardBtn(string text, Color bg, int x, int y, int w, int h)
        {
            Button b = new Button
            {
                Text = text, Location = new Point(x, y), Width = w, Height = h,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        protected override void Dispose(bool disposing)
        {
            _autoRefresh?.Stop();
            _autoRefresh?.Dispose();
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
