using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.place
{
    public partial class PlaceAll : UserControl
    {
        private FlowLayoutPanel flpTables;
        private Timer refreshTimer;
        private string _lastSnapshot = null;

        private static readonly Color Gold = Color.FromArgb(217, 119, 6);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);
        private static readonly Color BgMain = Color.FromArgb(248, 248, 250);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public PlaceAll()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.BackColor = BgMain;

            // Top bar
            Panel topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Color.White,
                Padding = new Padding(20, 0, 20, 0)
            };
            this.Controls.Add(topBar);
            topBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(229, 231, 235)), 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);

            // Legend
            AddLegend(topBar, Success, "Bo'sh", 0);
            AddLegend(topBar, Danger, "Band", 90);
            AddLegend(topBar, Gold, "Mening stolim", 175);

            Button btnRefresh = new Button
            {
                Text = "Yangilash",
                Width = 100,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                BackColor = Gold,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => { _lastSnapshot = null; LoadTables(); };
            topBar.Controls.Add(btnRefresh);
            topBar.Resize += (s, e) =>
            {
                btnRefresh.Location = new Point(topBar.Width - 120, 11);
            };

            // Tables grid
            flpTables = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgMain,
                Padding = new Padding(16, 12, 16, 12)
            };
            this.Controls.Add(flpTables);

            flpTables.Resize += (s, e) =>
            {
                int w = flpTables.ClientSize.Width - flpTables.Padding.Horizontal;
                if (w <= 0) return;
                foreach (Control c in flpTables.Controls)
                    if (c.Tag is string t && t == "section")
                        c.Width = w;
            };

            this.Load += (s, e) =>
            {
                refreshTimer = new Timer { Interval = 2000 };
                refreshTimer.Tick += (_, __) => LoadTables();
                refreshTimer.Start();
                BeginInvoke((Action)LoadTables);
            };
        }

        private void AddLegend(Panel parent, Color color, string text, int x)
        {
            Panel dot = new Panel { Width = 12, Height = 12, BackColor = color, Location = new Point(x, 21) };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, 11, 11);
            };
            parent.Controls.Add(dot);

            Label lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(x + 16, 19)
            };
            parent.Controls.Add(lbl);
        }

        public void LoadTables()
        {
            try
            {
                dbconnect db = new dbconnect();
                string sql = @"
                    SELECT pc.name AS cat_name, po.name AS zone_name,
                           pi.id AS table_id, pi.room_name, pi.empty, pi.user_id AS owner_id,
                           (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS order_count
                    FROM place_category pc
                    JOIN place_out po ON po.place_category_id = pc.id
                    JOIN place_in pi ON pi.place_out_id = po.id
                    ORDER BY ISNULL(po.sort_order,9999), po.name, TRY_CAST(SUBSTRING(pi.room_name,1,PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT), pi.room_name";

                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(dr);
                db.CloseCon();

                // Build snapshot for smart diff
                var sb = new System.Text.StringBuilder();
                foreach (DataRow row in dt.Rows)
                    sb.Append(row["table_id"]).Append('|')
                      .Append(row["empty"]).Append('|')
                      .Append(row["owner_id"] == DBNull.Value ? "" : row["owner_id"].ToString()).Append('|')
                      .Append(row["order_count"]).Append(';');
                string snapshot = sb.ToString();

                if (snapshot == _lastSnapshot) return;
                _lastSnapshot = snapshot;

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

                    int tableId = Convert.ToInt32(row["table_id"]);
                    string name = row["room_name"].ToString();
                    string empty = row["empty"].ToString();
                    int? ownerUserId = row["owner_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["owner_id"]);
                    int orderCount = Convert.ToInt32(row["order_count"]);

                    flpTables.Controls.Add(CreateTableCard(tableId, name, empty, ownerUserId, orderCount));
                }

                if (dt.Rows.Count == 0)
                {
                    Label lbl = new Label
                    {
                        Text = "Hali stol qo'shilmagan. \"Joylar\" bo'limidan stol qo'shing.",
                        Font = new Font("Segoe UI", 12),
                        ForeColor = TextMuted,
                        Width = 500,
                        Height = 40,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    flpTables.Controls.Add(lbl);
                }
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
            typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(c, val);
        }

        private Panel MakeSectionHeader(string title)
        {
            int w = flpTables.ClientSize.Width > 0
                ? flpTables.ClientSize.Width - flpTables.Padding.Horizontal
                : 2000;

            Panel p = new Panel
            {
                Width = w,
                Height = 36,
                Margin = new Padding(0, 12, 0, 4),
                BackColor = Color.Transparent,
                Tag = "section"
            };

            Label lbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(0, 6)
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
            bool canOpen = isEmpty || isMyTable || Session.CanManageOrders;

            Color accentColor = isEmpty ? Success : (isMyTable ? Gold : Danger);
            Color cardBg = isEmpty ? Color.White : (isMyTable ? Color.FromArgb(255, 249, 235) : Color.FromArgb(254, 242, 242));
            string statusText = isEmpty ? "Bo'sh" : (isMyTable ? "Mening stolim" : "Band");

            Panel card = new Panel
            {
                Width = 155,
                Height = 140,
                Margin = new Padding(7),
                BackColor = cardBg,
                Cursor = canOpen ? Cursors.Hand : Cursors.Default
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(accentColor, 2f), path);
                }
                e.Graphics.FillRectangle(new SolidBrush(accentColor), 0, 0, card.Width, 5);
            };

            Label lblRoom = new Label
            {
                Text = roomName,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                Width = 135,
                Height = 22,
                Location = new Point(10, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            card.Controls.Add(lblRoom);

            Label lblStatus = new Label
            {
                Text = statusText,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = accentColor,
                Width = 135,
                Height = 18,
                Location = new Point(10, 42),
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            card.Controls.Add(lblStatus);

            if (!isEmpty && orderCount > 0)
            {
                Label lblOrders = new Label
                {
                    Text = $"{orderCount} ta zakaz",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = TextMuted,
                    Width = 135,
                    Height = 16,
                    Location = new Point(10, 62),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false
                };
                card.Controls.Add(lblOrders);
            }

            Button btnOpen = new Button
            {
                Text = isEmpty ? "Zakaz qo'shish" : (canOpen ? "Ochish" : "Band"),
                Width = 131,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = canOpen ? accentColor : Color.FromArgb(200, 200, 210),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Location = new Point(12, 100),
                Cursor = canOpen ? Cursors.Hand : Cursors.Default,
                Enabled = canOpen
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btnOpen);

            if (canOpen)
            {
                EventHandler onClick = (s, e) => OpenOrderForTable(tableId);
                card.Click += onClick;
                btnOpen.Click += onClick;

                Color normalBg = cardBg;
                Color hoverBg = isEmpty ? Color.FromArgb(240, 252, 244)
                              : (isMyTable ? Color.FromArgb(255, 244, 210)
                              : Color.FromArgb(255, 235, 235));
                card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
                card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };
            }

            return card;
        }

        private void OpenOrderForTable(int tableId)
        {
            try
            {
                refreshTimer?.Stop();
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
                orderForm.ShowDialog(this.FindForm());
                LoadTables();
                refreshTimer?.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
                refreshTimer?.Start();
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

        protected override void Dispose(bool disposing)
        {
            refreshTimer?.Stop();
            refreshTimer?.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "PlaceAll";
            this.Size = new System.Drawing.Size(900, 600);
            this.ResumeLayout(false);
        }
    }
}
