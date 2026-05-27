using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    public class WaiterPage : Form
    {
        private FlowLayoutPanel flpTables;
        private Label lblWelcome;
        private Timer refreshTimer;
        private Panel _myOrdersOverlay;
        private System.Collections.Generic.Dictionary<int, string> _lastTableState
            = new System.Collections.Generic.Dictionary<int, string>();

        private static readonly Color BgCard = Color.White;
        private static readonly Color Gold = Color.FromArgb(217, 119, 6);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);
        private static readonly Color BgMain = Color.FromArgb(248, 248, 250);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public WaiterPage()
        {
            BuildUI();

            // Defer first load until layout is complete
            this.Load += (s, e) => BeginInvoke((Action)LoadTables);

            refreshTimer = new Timer { Interval = 2000 };
            refreshTimer.Tick += (s, e) => LoadTables();
            refreshTimer.Start();
        }

        private void BuildUI()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BgMain;
            this.Text = "FoodX — Ofitsiant";

            // === TOP HEADER ===
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = BgCard
            };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, header.Height - 1, header.Width, header.Height - 1);

            // Logo
            Label logo = new Label
            {
                Text = "FoodX",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Gold,
                AutoSize = true,
                Location = new Point(20, 18)
            };
            header.Controls.Add(logo);

            // Welcome
            lblWelcome = new Label
            {
                Text = $"Xush kelibsiz, {Session.UserName}!",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true
            };
            header.Controls.Add(lblWelcome);
            header.Resize += (s, e) =>
                lblWelcome.Location = new Point((header.Width - lblWelcome.Width) / 2, (header.Height - lblWelcome.Height) / 2);

            // Refresh + MyOrders + Logout buttons
            Panel rightBtns = new Panel
            {
                Width = 390,
                Height = 64,
                BackColor = Color.Transparent
            };
            header.Controls.Add(rightBtns);
            header.Resize += (s, e) =>
                rightBtns.Location = new Point(header.Width - 400, 0);

            Button btnMyOrders = MakeHeaderBtn("Buyurtmalarim", Color.FromArgb(59, 130, 246));
            btnMyOrders.Location = new Point(0, 14);
            btnMyOrders.Click += (s, e) => ShowMyOrders();
            rightBtns.Controls.Add(btnMyOrders);

            Button btnRefresh = MakeHeaderBtn("Yangilash", Color.FromArgb(217, 119, 6));
            btnRefresh.Location = new Point(130, 14);
            btnRefresh.Click += (s, e) => LoadTables();
            rightBtns.Controls.Add(btnRefresh);

            Button btnLogout = MakeHeaderBtn("Chiqish", Color.FromArgb(220, 38, 38));
            btnLogout.Location = new Point(260, 14);
            btnLogout.Click += (s, e) =>
            {
                refreshTimer?.Stop();
                Session.Clear();
                this.Hide();
                new Form1().Show();
            };
            rightBtns.Controls.Add(btnLogout);

            // === LEGEND BAR ===
            Panel legendBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.White,
                Padding = new Padding(20, 0, 20, 0)
            };

            legendBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, legendBar.Height - 1, legendBar.Width, legendBar.Height - 1);

            AddLegend(legendBar, Success, "Bo'sh stol", 20);
            AddLegend(legendBar, Danger, "Band stol", 140);
            AddLegend(legendBar, Gold, "Mening stolim", 260);


            // === SCROLL AREA ===
            Panel scrollArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgMain,
                AutoScroll = true,
                Padding = new Padding(20, 16, 20, 16)
            };

            flpTables = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgMain
            };
            scrollArea.Controls.Add(flpTables);

            // Add in correct order: Fill first, then Top controls, LAST Top = topmost
            this.Controls.Add(scrollArea);
            this.Controls.Add(legendBar);
            this.Controls.Add(header);
        }

        private void AddLegend(Panel parent, Color color, string text, int x)
        {
            Panel dot = new Panel
            {
                Width = 14,
                Height = 14,
                BackColor = color,
                Location = new Point(x, 15)
            };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, 13, 13);
            };
            parent.Controls.Add(dot);

            Label lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(x + 18, 14)
            };
            parent.Controls.Add(lbl);
        }

        private Button MakeHeaderBtn(string text, Color bg)
        {
            Button b = new Button
            {
                Text = text,
                Width = 110,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        public void LoadTables()
        {
            try
            {
                dbconnect db = new dbconnect();
                string sql = @"
                    SELECT
                        po.name AS zone_name,
                        po.id AS zone_id,
                        pi.id AS table_id,
                        pi.room_name,
                        pi.empty,
                        pi.user_id AS owner_id,
                        (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS order_count
                    FROM place_out po
                    JOIN place_category pc ON pc.id = po.place_category_id
                    JOIN place_in pi ON pi.place_out_id = po.id
                    ORDER BY ISNULL(po.sort_order,9999), po.name, pi.room_name";

                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(dr);
                db.CloseCon();

                // Build new state snapshot
                var newState = new System.Collections.Generic.Dictionary<int, string>();
                foreach (DataRow row in dt.Rows)
                {
                    int tid   = Convert.ToInt32(row["table_id"]);
                    string em = row["empty"].ToString();
                    string ow = row["owner_id"] == DBNull.Value ? "" : row["owner_id"].ToString();
                    string oc = row["order_count"].ToString();
                    newState[tid] = $"{em}|{ow}|{oc}";
                }

                // Skip full redraw if nothing changed
                bool changed = newState.Count != _lastTableState.Count;
                if (!changed)
                {
                    foreach (var kv in newState)
                    {
                        if (!_lastTableState.TryGetValue(kv.Key, out string prev) || prev != kv.Value)
                        { changed = true; break; }
                    }
                }
                if (!changed) return;

                _lastTableState = newState;

                // Only rebuild UI when something actually changed
                flpTables.SuspendLayout();
                SetDoubleBuffered(flpTables, true);
                flpTables.Controls.Clear();

                string currentSection = "";
                foreach (DataRow row in dt.Rows)
                {
                    string section = row["zone_name"].ToString();
                    if (section != currentSection)
                    {
                        currentSection = section;
                        flpTables.Controls.Add(MakeSectionHeader(section));
                    }

                    int tableId     = Convert.ToInt32(row["table_id"]);
                    string roomName = row["room_name"].ToString();
                    string empty    = row["empty"].ToString();
                    int? ownerUserId = row["owner_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["owner_id"]);
                    int orderCount  = Convert.ToInt32(row["order_count"]);

                    flpTables.Controls.Add(CreateTableCard(tableId, roomName, empty, ownerUserId, orderCount));
                }

                if (dt.Rows.Count == 0)
                    flpTables.Controls.Add(new Label
                    {
                        Text = "Hali hech qanday stol qo'shilmagan.\nAdmin panel orqali joy va stollar qo'shing.",
                        Font = new Font("Segoe UI", 13), ForeColor = TextMuted,
                        Width = 500, Height = 60, TextAlign = ContentAlignment.MiddleCenter
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Stollarni yuklashda xatolik: " + ex.Message);
            }
            finally
            {
                flpTables.ResumeLayout();
            }
        }

        private static void SetDoubleBuffered(Control c, bool val)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(c, val, null);
        }

        private Panel MakeSectionHeader(string title)
        {
            Panel p = new Panel
            {
                Width = flpTables.Width - 40,
                Height = 38,
                Margin = new Padding(0, 14, 0, 4),
                BackColor = Color.Transparent
            };

            Label lbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(0, 8)
            };
            p.Controls.Add(lbl);

            Panel line = new Panel
            {
                Height = 2,
                BackColor = Gold,
                Dock = DockStyle.Bottom
            };
            p.Controls.Add(line);

            return p;
        }

        private Panel CreateTableCard(int tableId, string roomName, string empty, int? ownerUserId, int orderCount)
        {
            bool isEmpty = empty?.ToUpper() == "YES";
            bool isMyTable = ownerUserId == Session.UserId;

            Color cardBg = isEmpty ? Color.White : Color.FromArgb(254, 242, 242);
            Color accentColor = isEmpty ? Success : (isMyTable ? Gold : Danger);
            Color statusColor = isEmpty ? Success : (isMyTable ? Gold : Danger);
            string statusText = isEmpty ? "Bo'sh" : (isMyTable ? "Mening stolim" : "Band");

            Panel card = new Panel
            {
                Width = 160,
                Height = 150,
                Margin = new Padding(8),
                BackColor = cardBg,
                Cursor = isEmpty || isMyTable ? Cursors.Hand : Cursors.Default
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(accentColor, 2f), path);
                }
                // Top stripe
                using (var gp = new GraphicsPath())
                {
                    gp.AddArc(0, 0, 20, 20, 180, 90);
                    gp.AddArc(card.Width - 20, 0, 20, 20, 270, 90);
                    gp.AddLine(card.Width, 6, 0, 6);
                    e.Graphics.FillPath(new SolidBrush(accentColor), gp);
                }
                e.Graphics.FillRectangle(new SolidBrush(accentColor), 0, 0, card.Width, 6);
            };

            // Room name
            Label lblRoom = new Label
            {
                Text = roomName,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                Width = 140,
                Height = 24,
                Location = new Point(10, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            card.Controls.Add(lblRoom);

            // Status badge
            Label lblStatus = new Label
            {
                Text = statusText,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = statusColor,
                Width = 140,
                Height = 18,
                Location = new Point(10, 48),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            card.Controls.Add(lblStatus);

            // Order count (if occupied)
            if (!isEmpty && orderCount > 0)
            {
                Label lblOrder = new Label
                {
                    Text = $"{orderCount} ta zakaz",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = TextMuted,
                    Width = 140,
                    Height = 16,
                    Location = new Point(10, 68),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false
                };
                card.Controls.Add(lblOrder);
            }

            // Action button
            Button btnAction;
            if (isEmpty)
            {
                btnAction = MakeTableBtn("Zakaz qo'shish", Success);
            }
            else if (isMyTable)
            {
                btnAction = MakeTableBtn("Ko'rish / O'zgartirish", Gold);
            }
            else
            {
                btnAction = MakeTableBtn("Band", Color.FromArgb(180, 180, 190));
                btnAction.Cursor = Cursors.Default;
                btnAction.Enabled = false;
            }

            btnAction.Location = new Point(14, 104);
            btnAction.Width = 132;
            card.Controls.Add(btnAction);

            // Click handler
            if (isEmpty || isMyTable)
            {
                EventHandler clickHandler = (s, e) => OpenOrderForTable(tableId, isEmpty);
                card.Click += clickHandler;
                btnAction.Click += clickHandler;

                Color normalBg = cardBg;
                Color hoverBg = isEmpty ? Color.FromArgb(240, 252, 244) : Color.FromArgb(255, 248, 230);
                card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
                card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };
            }

            return card;
        }

        private Button MakeTableBtn(string text, Color color)
        {
            Button b = new Button
            {
                Text = text,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void OpenOrderForTable(int tableId, bool isEmpty)
        {
            try
            {
                refreshTimer?.Stop();

                // Find existing unpaid order
                int existingOrderId = 0;
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 id FROM [order] WHERE place_id=@pid AND paid='NO'", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@pid", tableId);
                    object res = cmd.ExecuteScalar();
                    if (res != null) existingOrderId = Convert.ToInt32(res);
                }
                db.CloseCon();

                AddOrder orderForm = new AddOrder(tableId, existingOrderId, Session.Login);
                orderForm.ShowDialog(this);

                LoadTables();
                refreshTimer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
                refreshTimer?.Start();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  MENING BUYURTMALARIM OVERLAY
        // ══════════════════════════════════════════════════════════
        private void ShowMyOrders()
        {
            // Close if already open
            if (_myOrdersOverlay != null && Controls.Contains(_myOrdersOverlay))
            {
                Controls.Remove(_myOrdersOverlay);
                _myOrdersOverlay.Dispose();
                _myOrdersOverlay = null;
                return;
            }

            _myOrdersOverlay = new Panel
            {
                BackColor = Color.FromArgb(248, 248, 250),
                Location = Point.Empty,
                Size = ClientSize
            };
            this.Resize += SyncMyOrdersOverlay;

            // ── HEADER ──
            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.White };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);

            Button btnBack = new Button
            {
                Text = "←", Width = 44, Height = 40, Location = new Point(14, 11),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 14), Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnBack.Click += (s, e) => CloseMyOrders();
            hdr.Controls.Add(btnBack);

            hdr.Controls.Add(new Label
            {
                Text = "Mening buyurtmalarim",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(70, 16)
            });

            // ── FILTER BAR ──
            Panel filterBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.White };
            filterBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, filterBar.Height - 1, filterBar.Width, filterBar.Height - 1);

            // Status toggle
            string[] statuses = { "Hammasi", "Ochiq", "Yopilgan" };
            int[] statusX = { 16, 106, 196 };
            Button[] statusBtns = new Button[3];
            int selectedStatus = 0; // 0=all, 1=open, 2=closed

            // Date pickers
            var dtFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Value = DateTime.Today, Width = 120, Location = new Point(348, 10) };
            var dtTo   = new DateTimePicker { Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10), Value = DateTime.Today, Width = 120, Location = new Point(484, 10) };
            filterBar.Controls.Add(new Label { Text = "Sana:", Font = new Font("Segoe UI", 9), ForeColor = TextMuted, AutoSize = true, Location = new Point(296, 16) });
            filterBar.Controls.Add(dtFrom);
            filterBar.Controls.Add(new Label { Text = "—", AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = TextMuted, Location = new Point(472, 14) });
            filterBar.Controls.Add(dtTo);

            // Scroll content
            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 248, 250), AutoScroll = true };

            Action loadOrders = () =>
            {
                content.SuspendLayout();
                content.Controls.Clear();
                LoadMyOrdersContent(content, dtFrom.Value.Date, dtTo.Value.Date, selectedStatus);
                content.ResumeLayout();
            };

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                Button sb = new Button
                {
                    Text = statuses[i], Width = 84, Height = 32, Location = new Point(statusX[i], 10),
                    FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    BackColor = i == 0 ? Color.FromArgb(59, 130, 246) : Color.FromArgb(229, 231, 235),
                    ForeColor = i == 0 ? Color.White : TextDark, Cursor = Cursors.Hand
                };
                sb.FlatAppearance.BorderSize = 0;
                sb.Click += (s, e) =>
                {
                    selectedStatus = idx;
                    foreach (Button b in statusBtns) { b.BackColor = Color.FromArgb(229, 231, 235); b.ForeColor = TextDark; }
                    sb.BackColor = Color.FromArgb(59, 130, 246); sb.ForeColor = Color.White;
                    loadOrders();
                };
                statusBtns[i] = sb;
                filterBar.Controls.Add(sb);
            }

            dtFrom.ValueChanged += (s, e) => loadOrders();
            dtTo.ValueChanged   += (s, e) => loadOrders();

            _myOrdersOverlay.Controls.Add(content);
            _myOrdersOverlay.Controls.Add(filterBar);
            _myOrdersOverlay.Controls.Add(hdr);

            Controls.Add(_myOrdersOverlay);
            _myOrdersOverlay.BringToFront();

            BeginInvoke(new Action(loadOrders));
        }

        private void LoadMyOrdersContent(Panel content, DateTime from, DateTime to, int statusFilter)
        {
            var orders = new List<(int id, DateTime dt, string place, decimal total, bool paid)>();
            try
            {
                dbconnect db = new dbconnect();
                string statusSql = statusFilter == 1 ? "AND o.paid='NO'" : statusFilter == 2 ? "AND o.paid='YES'" : "";
                SqlCommand cmd = new SqlCommand($@"
                    SELECT o.id, o.created_at, o.total, o.paid,
                           ISNULL(pi.room_name, '—') AS place
                    FROM [order] o
                    LEFT JOIN place_in pi ON pi.id = o.place_id
                    WHERE o.user_id = @uid
                      AND CAST(o.created_at AS DATE) BETWEEN @d1 AND @d2
                      {statusSql}
                    ORDER BY o.created_at DESC", db.GetCon());
                cmd.Parameters.AddWithValue("@uid", Session.UserId);
                cmd.Parameters.AddWithValue("@d1", from);
                cmd.Parameters.AddWithValue("@d2", to);
                db.OpenCon();
                using (SqlDataReader dr = cmd.ExecuteReader())
                    while (dr.Read())
                        orders.Add((
                            Convert.ToInt32(dr["id"]),
                            Convert.ToDateTime(dr["created_at"]),
                            dr["place"].ToString(),
                            dr["total"] == DBNull.Value ? 0 : Convert.ToDecimal(dr["total"]),
                            dr["paid"].ToString().ToUpper() == "YES"
                        ));
                db.CloseCon();
            }
            catch { }

            if (orders.Count == 0)
            {
                content.Controls.Add(new Label
                {
                    Text = "Bu sana oralig'ida buyurtmalar topilmadi.",
                    Font = new Font("Segoe UI", 12), ForeColor = TextMuted,
                    AutoSize = true, Location = new Point(32, 40)
                });
                return;
            }

            // Column header
            Panel colHdr = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 44 };
            string[] cols = { "#", "Sana & Vaqt", "Stol", "Holat", "Summa", "" };
            float[] pct  = { .06f, .26f, .18f, .14f, .22f, .14f };
            Action<int> buildColHdr = (w) =>
            {
                colHdr.Controls.Clear();
                int x = 16;
                for (int i = 0; i < cols.Length; i++)
                {
                    int cw = (int)(w * pct[i]);
                    colHdr.Controls.Add(new Label { Text = cols[i], Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), Location = new Point(x, 0), Width = cw, Height = 44, TextAlign = ContentAlignment.MiddleLeft });
                    x += cw;
                }
            };
            colHdr.SetBounds(0, 0, content.Width, 44);
            colHdr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(colHdr);
            content.Resize += (s, e) => { colHdr.Width = content.Width; buildColHdr(content.Width); };
            buildColHdr(content.Width);

            int y = 44;
            bool alt = false;
            foreach (var (oid, dt, place, total, isPaid) in orders)
            {
                int _oid = oid; DateTime _dt = dt; string _pl = place; decimal _tot = total; bool _paid = isPaid; bool _alt = alt;

                Color statusColor = _paid ? Color.FromArgb(16, 185, 129) : Color.FromArgb(249, 115, 22);
                string statusText = _paid ? "✓ Yopilgan" : "● Ochiq";

                Panel row = new Panel { BackColor = _alt ? Color.FromArgb(249, 250, 251) : Color.White, Height = 56 };
                row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, row.Height - 1, row.Width, row.Height - 1);

                Action<int> buildRow = (w) =>
                {
                    row.Controls.Clear();
                    int x = 16;
                    int[] ws = { (int)(w*pct[0]),(int)(w*pct[1]),(int)(w*pct[2]),(int)(w*pct[3]),(int)(w*pct[4]),(int)(w*pct[5]) };

                    row.Controls.Add(RowLabel("#" + _oid, x, ws[0], TextDark, FontStyle.Bold, row.Height)); x += ws[0];
                    row.Controls.Add(RowLabel("📅 " + _dt.ToString("dd.MM.yyyy  HH:mm"), x, ws[1], TextDark, FontStyle.Regular, row.Height)); x += ws[1];
                    row.Controls.Add(RowLabel("🪑 " + _pl, x, ws[2], TextMuted, FontStyle.Regular, row.Height)); x += ws[2];
                    row.Controls.Add(RowLabel(statusText, x, ws[3], statusColor, FontStyle.Bold, row.Height)); x += ws[3];
                    row.Controls.Add(RowLabel(_tot.ToString("N0") + " UZS", x, ws[4], Color.FromArgb(16, 185, 129), FontStyle.Bold, row.Height)); x += ws[4];

                    // "Batafsil" button
                    Button btnDetail = new Button
                    {
                        Text = "Batafsil →", Width = ws[5] - 16, Height = 32,
                        Location = new Point(x + 4, (row.Height - 32) / 2),
                        FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(59, 130, 246),
                        ForeColor = Color.White, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand
                    };
                    btnDetail.FlatAppearance.BorderSize = 0;
                    btnDetail.Click += (s, e) => ShowOrderDetail(_oid);
                    row.Controls.Add(btnDetail);
                };

                row.SetBounds(0, y, content.Width, 56);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                content.Controls.Add(row);
                content.Resize += (s, e) => { row.Width = content.Width; buildRow(content.Width); };
                buildRow(content.Width);
                y += 56; alt = !alt;
            }
        }

        private void ShowOrderDetail(int orderId)
        {
            Panel detailOverlay = new Panel
            {
                BackColor = Color.FromArgb(248, 248, 250),
                Location = Point.Empty,
                Size = ClientSize
            };
            EventHandler syncSize = (s, e) => detailOverlay.Size = ClientSize;
            this.Resize += syncSize;

            Panel hdr = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Color.White };
            hdr.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);

            Button btnBack = new Button
            {
                Text = "←", Width = 44, Height = 40, Location = new Point(14, 11),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 14), Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnBack.Click += (s, e) =>
            {
                this.Resize -= syncSize;
                Controls.Remove(detailOverlay);
                detailOverlay.Dispose();
            };
            hdr.Controls.Add(btnBack);
            hdr.Controls.Add(new Label
            {
                Text = $"Buyurtma #{orderId} — Tarkibi",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(70, 16)
            });

            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 248, 250), AutoScroll = true };
            detailOverlay.Controls.Add(content);
            detailOverlay.Controls.Add(hdr);
            Controls.Add(detailOverlay);
            detailOverlay.BringToFront();

            BeginInvoke(new Action(() =>
            {
                try
                {
                    dbconnect db = new dbconnect();
                    SqlCommand cmd = new SqlCommand(@"
                        SELECT f.name, ofd.quantity, f.selling_price,
                               ofd.quantity * f.selling_price AS subtotal
                        FROM order_food ofd
                        JOIN food f ON f.id = ofd.food_id
                        WHERE ofd.order_id = @oid
                        ORDER BY f.name", db.GetCon());
                    cmd.Parameters.AddWithValue("@oid", orderId);
                    db.OpenCon();

                    // Header row
                    Panel colHdr = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 44 };
                    string[] cols = { "Taom nomi", "Miqdor", "Narx", "Jami" };
                    float[] pct  = { .44f, .16f, .20f, .20f };
                    Action<int> buildHdr = (w) =>
                    {
                        colHdr.Controls.Clear(); int x = 16;
                        for (int i = 0; i < cols.Length; i++)
                        {
                            int cw = (int)(w * pct[i]);
                            colHdr.Controls.Add(new Label { Text = cols[i], Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), Location = new Point(x, 0), Width = cw, Height = 44, TextAlign = ContentAlignment.MiddleLeft });
                            x += cw;
                        }
                    };
                    colHdr.SetBounds(0, 0, content.Width, 44);
                    colHdr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(colHdr);
                    content.Resize += (s, e) => { colHdr.Width = content.Width; buildHdr(content.Width); };
                    buildHdr(content.Width);

                    int y = 44; bool alt = false; decimal grandTotal = 0;
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            string fn = dr["name"].ToString();
                            int qty = Convert.ToInt32(dr["quantity"]);
                            decimal price = Convert.ToDecimal(dr["selling_price"]);
                            decimal sub = Convert.ToDecimal(dr["subtotal"]);
                            grandTotal += sub;
                            bool a = alt;
                            string _fn = fn; int _q = qty; decimal _p = price, _s = sub;

                            Panel row = new Panel { BackColor = a ? Color.FromArgb(249, 250, 251) : Color.White, Height = 52 };
                            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, row.Height - 1, row.Width, row.Height - 1);
                            Action<int> br = (w) =>
                            {
                                row.Controls.Clear(); int x = 16;
                                int[] ws = { (int)(w*pct[0]),(int)(w*pct[1]),(int)(w*pct[2]),(int)(w*pct[3]) };
                                row.Controls.Add(RowLabel(_fn, x, ws[0], TextDark, FontStyle.Bold, row.Height)); x += ws[0];
                                row.Controls.Add(RowLabel(_q + " dona", x, ws[1], TextMuted, FontStyle.Regular, row.Height)); x += ws[1];
                                row.Controls.Add(RowLabel(_p.ToString("N0") + " UZS", x, ws[2], TextMuted, FontStyle.Regular, row.Height)); x += ws[2];
                                row.Controls.Add(RowLabel(_s.ToString("N0") + " UZS", x, ws[3], Color.FromArgb(16, 185, 129), FontStyle.Bold, row.Height));
                            };
                            row.SetBounds(0, y, content.Width, 52);
                            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                            content.Controls.Add(row);
                            content.Resize += (s, e) => { row.Width = content.Width; br(content.Width); };
                            br(content.Width);
                            y += 52; alt = !alt;
                        }
                    }

                    // Total row
                    decimal _gt = grandTotal;
                    Panel totalRow = new Panel { BackColor = Color.FromArgb(240, 253, 244), Height = 56 };
                    Action<int> buildTotal = (w) =>
                    {
                        totalRow.Controls.Clear();
                        totalRow.Controls.Add(new Label { Text = "JAMI SUMMA:", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextDark, Location = new Point(16, 0), Width = w / 2, Height = 56, TextAlign = ContentAlignment.MiddleLeft });
                        totalRow.Controls.Add(new Label { Text = _gt.ToString("N0") + " UZS", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), Location = new Point(w / 2, 0), Width = w / 2 - 16, Height = 56, TextAlign = ContentAlignment.MiddleRight });
                    };
                    totalRow.SetBounds(0, y, content.Width, 56);
                    totalRow.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    content.Controls.Add(totalRow);
                    content.Resize += (s, e) => { totalRow.Width = content.Width; buildTotal(content.Width); };
                    buildTotal(content.Width);

                    db.CloseCon();
                }
                catch { }
            }));
        }

        private void CloseMyOrders()
        {
            this.Resize -= SyncMyOrdersOverlay;
            if (_myOrdersOverlay != null && Controls.Contains(_myOrdersOverlay))
            {
                Controls.Remove(_myOrdersOverlay);
                _myOrdersOverlay.Dispose();
                _myOrdersOverlay = null;
            }
        }

        private void SyncMyOrdersOverlay(object s, EventArgs e)
        {
            if (_myOrdersOverlay != null) _myOrdersOverlay.Size = ClientSize;
        }

        private static Label RowLabel(string text, int x, int w, Color fc, FontStyle fs, int h)
            => new Label { Text = text, Font = new Font("Segoe UI", 10, fs), ForeColor = fc, Location = new Point(x, 0), Width = w, Height = h, TextAlign = ContentAlignment.MiddleLeft };

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

        protected override void Dispose(bool disposing)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}
