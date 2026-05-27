using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.place
{
    public partial class PlaceAllin : UserControl
    {
        private readonly int placeOutId;
        private FlowLayoutPanel flowLayoutPanel1;

        private static readonly Color Gold = Color.FromArgb(217, 119, 6);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);
        private static readonly Color BgMain = Color.FromArgb(248, 248, 250);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public PlaceAllin(int id1)
        {
            placeOutId = id1;
            InitializeComponent();
        }

        private void PlaceAllin_Load(object sender, EventArgs e)
        {
            LoadPlaces();
        }

        private void LoadPlaces()
        {
            flowLayoutPanel1.SuspendLayout();
            flowLayoutPanel1.Controls.Clear();

            dbconnect db = new dbconnect();
            string query = @"SELECT pi.id, pi.room_name, pi.empty, pi.user_id,
                                    (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS order_count
                             FROM place_in pi
                             WHERE pi.place_out_id = @pid
                             ORDER BY TRY_CAST(SUBSTRING(pi.room_name,1,PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT), pi.room_name";

            SqlCommand cmd = new SqlCommand(query, db.GetCon());
            cmd.Parameters.AddWithValue("@pid", placeOutId);
            db.OpenCon();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                int placeId = dr.GetInt32(dr.GetOrdinal("id"));
                string roomName = dr["room_name"].ToString();
                string empty = dr["empty"].ToString();
                int? ownerUserId = dr["user_id"] != DBNull.Value ? Convert.ToInt32(dr["user_id"]) : (int?)null;
                int orderCount = Convert.ToInt32(dr["order_count"]);

                flowLayoutPanel1.Controls.Add(CreateTableCard(placeId, roomName, empty, ownerUserId, orderCount));
            }

            dr.Close();
            db.CloseCon();
            flowLayoutPanel1.ResumeLayout();
        }

        private Panel CreateTableCard(int placeId, string roomName, string empty, int? ownerUserId, int orderCount)
        {
            bool isEmpty = empty?.ToUpper() == "YES";
            bool isMyTable = ownerUserId == Session.UserId;
            bool canOpen = isEmpty || isMyTable || Session.CanManageOrders;

            Color accentColor = isEmpty ? Success : (isMyTable ? Gold : Danger);
            Color cardBg = isEmpty ? Color.White : (isMyTable ? Color.FromArgb(255, 249, 235) : Color.FromArgb(254, 242, 242));

            Panel card = new Panel
            {
                Width = 150,
                Height = 130,
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

            Label lblName = new Label
            {
                Text = roomName,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                Width = 130,
                Height = 22,
                Location = new Point(10, 14),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblName);

            Label lblStatus = new Label
            {
                Text = isEmpty ? "Bo'sh" : (isMyTable ? "Mening stolim" : "Band"),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = accentColor,
                Width = 130,
                Height = 18,
                Location = new Point(10, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblStatus);

            if (!isEmpty && orderCount > 0)
            {
                Label lblOrd = new Label
                {
                    Text = $"{orderCount} ta zakaz",
                    Font = new Font("Segoe UI", 8),
                    ForeColor = TextMuted,
                    Width = 130,
                    Height = 16,
                    Location = new Point(10, 60),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(lblOrd);
            }

            Button btn = new Button
            {
                Text = canOpen ? (isEmpty ? "Zakaz qo'shish" : "Ochish") : "Band",
                Width = 126,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = canOpen ? accentColor : Color.FromArgb(200, 200, 210),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Location = new Point(12, 92),
                Cursor = canOpen ? Cursors.Hand : Cursors.Default,
                Enabled = canOpen
            };
            btn.FlatAppearance.BorderSize = 0;
            card.Controls.Add(btn);

            if (canOpen)
            {
                EventHandler onClick = (s, e) => OpenOrder(placeId);
                card.Click += onClick;
                btn.Click += onClick;
            }

            return card;
        }

        private void OpenOrder(int placeId)
        {
            dbconnect db = new dbconnect();
            db.OpenCon();
            SqlCommand cm = new SqlCommand("SELECT TOP 1 id FROM [order] WHERE place_id=@pid AND paid='NO'", db.GetCon());
            cm.Parameters.AddWithValue("@pid", placeId);
            object result = cm.ExecuteScalar();
            int orderId = result != null ? Convert.ToInt32(result) : 0;
            db.CloseCon();

            AddOrder addOrder = new AddOrder(placeId, orderId, Session.Login);
            addOrder.ShowDialog(this.FindForm());
            LoadPlaces();
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

        private void InitializeComponent()
        {
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.SuspendLayout();
            this.flowLayoutPanel1.AutoScroll = true;
            this.flowLayoutPanel1.BackColor = Color.FromArgb(248, 248, 250);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.Padding = new System.Windows.Forms.Padding(12);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.WrapContents = true;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "PlaceAllin";
            this.Size = new System.Drawing.Size(900, 600);
            this.Load += new System.EventHandler(this.PlaceAllin_Load);
            this.ResumeLayout(false);
        }
    }
}
