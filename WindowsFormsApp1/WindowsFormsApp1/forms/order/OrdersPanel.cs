using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.order
{
    public class OrdersPanel : UserControl
    {
        private FlowLayoutPanel flpOrders;
        private Label lblCount, lblRevenue, lblRefreshTime, lblCountTitle, lblRevenueTitle;
        private Timer refreshTimer;
        private Button btnAll, btnOpen, btnClosed;
        private DateTimePicker dtpFrom, dtpTo;
        private string _statusFilter = "NO"; // NO=ochiq, YES=yopilgan, ALL=barchasi
        private string _lastSnapshot = null; // state hash for smart diff

        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color BgMain    = Color.FromArgb(248, 248, 250);
        private static readonly Color CardBg    = Color.White;
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color BtnNorm   = Color.FromArgb(243, 244, 246);

        public OrdersPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = BgMain;
            BuildUI();

            // Defer first load until control is fully laid out and visible
            this.Load += (s, e) => BeginInvoke((Action)LoadOrders);

            refreshTimer = new Timer { Interval = 2000 };
            refreshTimer.Tick += (s, e) => LoadOrders();
            refreshTimer.Start();
        }

        private void BuildUI()
        {
            // === STATS BAR ===
            Panel statsBar = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = BgMain };

            Panel statOrders = MakeStatCard("Ochiq zakaslar", "0", Gold, out lblCount, out lblCountTitle);
            statOrders.Location = new Point(20, 8);
            statsBar.Controls.Add(statOrders);

            Panel statRev = MakeStatCard("Kutilayotgan summa", "0 so'm", Success, out lblRevenue, out lblRevenueTitle);
            statRev.Location = new Point(20 + statOrders.Width + 10, 8);
            statsBar.Controls.Add(statRev);

            statsBar.Resize += (s, e) =>
            {
                statOrders.Location = new Point(20, 8);
                statRev.Location    = new Point(20 + statOrders.Width + 10, 8);
            };

            Button btnRefresh = new Button
            {
                Text = "↻  Yangilash", Width = 110, Height = 32,
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => LoadOrders();
            statsBar.Controls.Add(btnRefresh);
            statsBar.Resize += (s, e) => btnRefresh.Location = new Point(statsBar.Width - 130, 10);

            lblRefreshTime = new Label { Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted, AutoSize = true };
            statsBar.Controls.Add(lblRefreshTime);
            statsBar.Resize += (s, e) => lblRefreshTime.Location = new Point(statsBar.Width - 130, 48);

            // === FILTER BAR ===
            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = CardBg };
            filterBar.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, 0, filterBar.Width, 0);
                e.Graphics.DrawLine(new Pen(Border), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);
            };

            // Status toggle buttons (left)
            btnAll    = MakeToggleBtn("Barchasi", 82);
            btnOpen   = MakeToggleBtn("Ochiq",    66);
            btnClosed = MakeToggleBtn("Yopilgan", 78);

            btnAll.Location    = new Point(16, 10);
            btnOpen.Location   = new Point(16 + btnAll.Width + 4, 10);
            btnClosed.Location = new Point(16 + btnAll.Width + 4 + btnOpen.Width + 4, 10);

            void RefreshToggle()
            {
                btnAll.BackColor    = _statusFilter == "ALL" ? Gold : BtnNorm;
                btnAll.ForeColor    = _statusFilter == "ALL" ? Color.White : TextMuted;
                btnOpen.BackColor   = _statusFilter == "NO"  ? Gold : BtnNorm;
                btnOpen.ForeColor   = _statusFilter == "NO"  ? Color.White : TextMuted;
                btnClosed.BackColor = _statusFilter == "YES" ? Gold : BtnNorm;
                btnClosed.ForeColor = _statusFilter == "YES" ? Color.White : TextMuted;
            }

            btnAll.Click    += (s, e) => { _statusFilter = "ALL"; _lastSnapshot = null; RefreshToggle(); LoadOrders(); };
            btnOpen.Click   += (s, e) => { _statusFilter = "NO";  _lastSnapshot = null; RefreshToggle(); LoadOrders(); };
            btnClosed.Click += (s, e) => { _statusFilter = "YES"; _lastSnapshot = null; RefreshToggle(); LoadOrders(); };
            RefreshToggle();

            filterBar.Controls.AddRange(new Control[] { btnAll, btnOpen, btnClosed });

            // Date pickers (right)
            dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 100, Value = DateTime.Today, Font = new Font("Segoe UI", 9) };
            dtpTo   = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 100, Value = DateTime.Today, Font = new Font("Segoe UI", 9) };
            dtpFrom.ValueChanged += (s, e) => { if (dtpFrom.Value.Date > dtpTo.Value.Date) dtpTo.Value = dtpFrom.Value; _lastSnapshot = null; LoadOrders(); };
            dtpTo.ValueChanged   += (s, e) => { if (dtpTo.Value.Date < dtpFrom.Value.Date) dtpFrom.Value = dtpTo.Value; _lastSnapshot = null; LoadOrders(); };

            Label lblDan   = new Label { Text = "Dan:",   AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = TextMuted };
            Label lblGacha = new Label { Text = "Gacha:", AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = TextMuted };

            // Quick buttons
            Button btnToday = MakeToggleBtn("Bugun", 58);
            Button btnYest  = MakeToggleBtn("Kecha", 58);
            btnToday.Click += (s, e) => { dtpFrom.Value = dtpTo.Value = DateTime.Today; };
            btnYest.Click  += (s, e) => { dtpFrom.Value = dtpTo.Value = DateTime.Today.AddDays(-1); };

            filterBar.Controls.AddRange(new Control[] { lblDan, dtpFrom, lblGacha, dtpTo, btnToday, btnYest });

            filterBar.Resize += (s, e) =>
            {
                int fy = (filterBar.Height - dtpFrom.Height) / 2;
                int rx = filterBar.Width - 16;
                dtpTo.Location    = new Point(rx - dtpTo.Width, fy);
                lblGacha.Location = new Point(dtpTo.Left - lblGacha.Width - 4, fy + 2);
                dtpFrom.Location  = new Point(lblGacha.Left - dtpFrom.Width - 5, fy);
                lblDan.Location   = new Point(dtpFrom.Left - lblDan.Width - 4, fy + 2);
                int qy = (filterBar.Height - btnToday.Height) / 2;
                btnYest.Location  = new Point(lblDan.Left - btnYest.Width - 14, qy);
                btnToday.Location = new Point(btnYest.Left - btnToday.Width - 4, qy);
            };

            // === ORDERS GRID ===
            Panel scrollWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Padding = new Padding(16) };

            // Fill FIRST, then Top controls, LAST = topmost
            this.Controls.Add(scrollWrap);
            this.Controls.Add(filterBar);
            this.Controls.Add(statsBar);

            flpOrders = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
                BackColor = BgMain
            };
            scrollWrap.Controls.Add(flpOrders);
        }

        private Panel MakeStatCard(string title, string value, Color accent, out Label lblVal, out Label lblTitle)
        {
            Panel card = new Panel { Width = 186, Height = 62, BackColor = CardBg };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Border))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, 4, card.Height);
            };

            Label lTitle = new Label { Text = title, Font = new Font("Segoe UI", 8), ForeColor = TextMuted, AutoSize = true, Location = new Point(14, 8) };
            Label lVal   = new Label { Text = value, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = accent, AutoSize = true, Location = new Point(14, 26) };

            card.Controls.Add(lTitle);
            card.Controls.Add(lVal);
            lblVal   = lVal;
            lblTitle = lTitle;
            return card;
        }

        public void LoadOrders()
        {
            try
            {
                dbconnect db = new dbconnect();
                string sql = @"
                    SELECT
                        o.id AS order_id,
                        o.total,
                        o.created_at,
                        o.paid,
                        ISNULL(pay.name, '') AS pay_name,
                        u.name AS waiter_name,
                        pi.room_name AS table_name,
                        po.name AS zone_name,
                        (SELECT COUNT(*) FROM order_food WHERE order_id = o.id) AS items_count
                    FROM [order] o
                    JOIN [user] u ON u.id = o.user_id
                    JOIN place_in pi ON pi.id = o.place_id
                    JOIN place_out po ON po.id = pi.place_out_id
                    LEFT JOIN payment pay ON pay.id = o.payment_id
                    WHERE (@status = 'ALL' OR o.paid = @status)
                      AND CAST(o.created_at AS DATE) >= CAST(@dateFrom AS DATE)
                      AND CAST(o.created_at AS DATE) <= CAST(@dateTo   AS DATE)
                    ORDER BY o.created_at DESC";

                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                cmd.Parameters.AddWithValue("@status",   _statusFilter);
                cmd.Parameters.AddWithValue("@dateFrom", dtpFrom?.Value.Date ?? DateTime.Today);
                cmd.Parameters.AddWithValue("@dateTo",   dtpTo?.Value.Date   ?? DateTime.Today);
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(dr);
                db.CloseCon();

                // Build snapshot string for smart diff
                var sb = new System.Text.StringBuilder();
                decimal totalRev = 0;
                foreach (DataRow row in dt.Rows)
                {
                    decimal t = Convert.ToDecimal(row["total"]);
                    totalRev += t;
                    sb.Append(row["order_id"]).Append('|')
                      .Append(row["paid"]).Append('|')
                      .Append(t).Append('|')
                      .Append(row["items_count"]).Append('|')
                      .Append(row["waiter_name"]).Append(';');
                }
                string snapshot = _statusFilter + sb.ToString();

                // Always update stats labels (lightweight)
                switch (_statusFilter)
                {
                    case "YES":
                        if (lblCountTitle   != null) lblCountTitle.Text   = "Yopilgan zakaslar";
                        if (lblRevenueTitle != null) lblRevenueTitle.Text = "Jami tushum";   break;
                    case "ALL":
                        if (lblCountTitle   != null) lblCountTitle.Text   = "Jami zakaslar";
                        if (lblRevenueTitle != null) lblRevenueTitle.Text = "Jami summa";    break;
                    default:
                        if (lblCountTitle   != null) lblCountTitle.Text   = "Ochiq zakaslar";
                        if (lblRevenueTitle != null) lblRevenueTitle.Text = "Kutilayotgan summa"; break;
                }
                lblCount.Text       = dt.Rows.Count.ToString();
                lblRevenue.Text     = totalRev.ToString("N0") + " so'm";
                lblRefreshTime.Text = "Yangilandi: " + DateTime.Now.ToString("HH:mm:ss");

                // Skip full card rebuild if nothing changed
                if (snapshot == _lastSnapshot) return;
                _lastSnapshot = snapshot;

                // Rebuild cards only when data changed
                flpOrders.SuspendLayout();
                SetDoubleBuffered(flpOrders, true);
                flpOrders.Controls.Clear();

                if (dt.Rows.Count == 0)
                {
                    Panel empty = new Panel { Width = flpOrders.Width - 40, Height = 200, BackColor = Color.Transparent };
                    string msg = _statusFilter == "YES" ? "Bu kunda yopilgan zakaslar yo'q"
                               : _statusFilter == "ALL" ? "Bu kunda zakaslar yo'q"
                               : "Hozircha ochiq zakaslar yo'q";
                    empty.Controls.Add(new Label
                    {
                        Text = msg, Font = new Font("Segoe UI", 13),
                        ForeColor = TextMuted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                    });
                    flpOrders.Controls.Add(empty);
                }
                else
                {
                    foreach (DataRow row in dt.Rows)
                        flpOrders.Controls.Add(CreateOrderCard(row));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Zakaslarni yuklashda xatolik: " + ex.Message);
            }
            finally
            {
                flpOrders.ResumeLayout();
            }
        }

        private Panel CreateOrderCard(DataRow row)
        {
            int orderId    = Convert.ToInt32(row["order_id"]);
            decimal total  = Convert.ToDecimal(row["total"]);
            DateTime created = Convert.ToDateTime(row["created_at"]);
            string waiter  = row["waiter_name"].ToString();
            string table   = row["table_name"].ToString();
            string zone    = row["zone_name"].ToString();
            int items      = Convert.ToInt32(row["items_count"]);
            bool isClosed  = row["paid"].ToString() == "YES";
            string payName = row["pay_name"].ToString();

            Color stripe = isClosed ? Success : Gold;

            string timeStr;
            Color  timeColor;
            if (isClosed)
            {
                timeStr   = created.ToString("dd.MM.yyyy  HH:mm");
                timeColor = TextMuted;
            }
            else
            {
                TimeSpan el = DateTime.Now - created;
                timeStr   = (el.TotalHours >= 1 ? $"{(int)el.TotalHours}s {el.Minutes}d" : $"{el.Minutes} daqiqa") + " oldin";
                timeColor = el.TotalMinutes > 30 ? Danger : el.TotalMinutes > 15 ? Gold : Success;
            }

            Panel card = new Panel { Width = 280, Height = 210, Margin = new Padding(8), BackColor = CardBg, Cursor = Cursors.Hand };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border), path);
                }
                e.Graphics.FillRectangle(new SolidBrush(stripe), 0, 0, card.Width, 4);
            };

            // Zone — Table
            card.Controls.Add(new Label
            {
                Text = $"{zone}  —  {table}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = TextDark,
                Width = isClosed ? 176 : 252, Height = 22, Location = new Point(14, 16), AutoSize = false
            });

            // "YOPILGAN" badge
            if (isClosed)
                card.Controls.Add(new Label
                {
                    Text = "YOPILGAN",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Color.White, BackColor = Success,
                    AutoSize = false, Width = 68, Height = 18,
                    Location = new Point(198, 17), TextAlign = ContentAlignment.MiddleCenter
                });

            // Waiter
            card.Controls.Add(new Label
            {
                Text = "Ofitsiant: " + waiter, Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted, Width = 252, Height = 16, Location = new Point(14, 42), AutoSize = false
            });

            // Separator
            card.Controls.Add(new Panel { Height = 1, Width = 252, BackColor = Border, Location = new Point(14, 64) });

            // Items count
            card.Controls.Add(new Label
            {
                Text = $"{items} ta taom", Font = new Font("Segoe UI", 10),
                ForeColor = TextMuted, Location = new Point(14, 76), AutoSize = true
            });

            // Time label
            card.Controls.Add(new Label
            {
                Text = timeStr, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = timeColor, AutoSize = true, Location = new Point(14, 100)
            });

            // Total
            card.Controls.Add(new Label
            {
                Text = total.ToString("N0") + " so'm",
                Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = stripe,
                Width = 252, Height = 26, Location = new Point(14, 122),
                TextAlign = ContentAlignment.MiddleLeft, AutoSize = false
            });

            // Payment for closed orders
            if (isClosed && !string.IsNullOrEmpty(payName))
                card.Controls.Add(new Label
                {
                    Text = "To'lov: " + payName, Font = new Font("Segoe UI", 8),
                    ForeColor = TextMuted, AutoSize = true, Location = new Point(14, 152)
                });

            // Buttons
            Button btnView = MakeCardBtn("Ko'rish", Color.FromArgb(59, 130, 246));
            btnView.Location = new Point(14, 168);
            btnView.Click += (s, e) => OpenOrder(orderId);
            card.Controls.Add(btnView);

            if (!isClosed && Session.CanManageOrders)
            {
                Button btnClose = MakeCardBtn("Hisob yopish", Danger);
                btnClose.Location = new Point(132, 168);
                btnClose.Width = 134;
                btnClose.Click += (s, e) => CloseOrder(orderId, card);
                card.Controls.Add(btnClose);
            }

            Color normalBg = CardBg;
            Color hoverBg  = isClosed ? Color.FromArgb(240, 253, 244) : Color.FromArgb(252, 252, 255);
            card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
            card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };

            return card;
        }

        private Button MakeToggleBtn(string text, int width = 80)
        {
            Button b = new Button
            {
                Text = text, Width = width, Height = 30,
                FlatStyle = FlatStyle.Flat, BackColor = BtnNorm,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize  = 1;
            b.FlatAppearance.BorderColor = Border;
            return b;
        }

        private Button MakeCardBtn(string text, Color color)
        {
            Button b = new Button
            {
                Text = text, Width = 110, Height = 32,
                FlatStyle = FlatStyle.Flat, BackColor = color,
                ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void OpenOrder(int orderId)
        {
            refreshTimer?.Stop();
            AddOrder form = new AddOrder(0, orderId, Session.Login);
            form.ShowDialog();
            LoadOrders();
            refreshTimer?.Start();
        }

        private void CloseOrder(int orderId, Panel card)
        {
            if (MessageBox.Show("Bu zakasni yopishni tasdiqlaysizmi?", "Tasdiqlash",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();

                int placeId = 0;
                using (SqlCommand cmd = new SqlCommand("SELECT place_id FROM [order] WHERE id=@id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    placeId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (SqlCommand cmd = new SqlCommand("UPDATE [order] SET paid='YES' WHERE id=@id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    cmd.ExecuteNonQuery();
                }

                // Deduct ingredients from warehouse stock
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE ingredient
                    SET quantity = CASE WHEN quantity - d.total_deduct < 0 THEN 0 ELSE quantity - d.total_deduct END
                    FROM ingredient
                    JOIN (
                        SELECT ri.ingredient_id, SUM(CAST(ofd.quantity AS DECIMAL(10,4)) * ri.quantity_per_portion) AS total_deduct
                        FROM order_food ofd
                        JOIN recipe_ingredient ri ON ri.food_id = ofd.food_id
                        WHERE ofd.order_id = @oid
                        GROUP BY ri.ingredient_id
                    ) d ON d.ingredient_id = ingredient.id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@oid", orderId);
                    cmd.ExecuteNonQuery();
                }

                if (placeId > 0)
                    using (SqlCommand cmd = new SqlCommand("UPDATE place_in SET empty='YES', user_id=NULL WHERE id=@pid", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@pid", placeId);
                        cmd.ExecuteNonQuery();
                    }

                db.CloseCon();
                LoadOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, radius * 2, radius * 2, 180, 90);
            p.AddArc(r.Right - radius * 2, r.Y, radius * 2, radius * 2, 270, 90);
            p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            p.AddArc(r.X, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static void SetDoubleBuffered(Control c, bool val)
        {
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(c, val);
        }

        protected override void Dispose(bool disposing)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
