using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.place
{
    public partial class PlaceOut : UserControl
    {
        private readonly int categoryId;
        private FlowLayoutPanel flowLayoutPanel1;

        private static readonly Color Gold = Color.FromArgb(217, 119, 6);
        private static readonly Color BgMain = Color.FromArgb(248, 248, 250);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public PlaceOut(int id1)
        {
            categoryId = id1;
            InitializeComponent();
        }

        private void PlaceOut_Load(object sender, EventArgs e)
        {
            LoadPlace();
        }

        private void LoadPlace()
        {
            flowLayoutPanel1.Controls.Clear();
            dbconnect db = new dbconnect();

            // Fixed: use parameterized query (was string concatenation before - SQL injection risk)
            SqlCommand cmd = new SqlCommand(
                "SELECT id, name FROM place_out WHERE place_category_id=@cid ORDER BY name",
                db.GetCon());
            cmd.Parameters.AddWithValue("@cid", categoryId);

            db.OpenCon();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                int zoneId = dr.GetInt32(dr.GetOrdinal("id"));
                string zoneName = dr["name"].ToString();

                Panel card = new Panel
                {
                    Width = 200,
                    Height = 70,
                    Margin = new Padding(8),
                    BackColor = Color.White,
                    Cursor = Cursors.Hand
                };

                card.Paint += (s, ev) =>
                {
                    ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = RoundRect(card.ClientRectangle, 8))
                    {
                        ev.Graphics.FillPath(Brushes.White, path);
                        ev.Graphics.DrawPath(new Pen(Color.FromArgb(229, 231, 235)), path);
                    }
                    ev.Graphics.FillRectangle(new SolidBrush(Gold), 0, 0, card.Width, 4);
                };

                Label lbl = new Label
                {
                    Text = zoneName,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = TextDark,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(lbl);

                card.MouseEnter += (s, ev) => { card.BackColor = Color.FromArgb(252, 249, 240); card.Refresh(); };
                card.MouseLeave += (s, ev) => { card.BackColor = Color.White; card.Refresh(); };

                EventHandler clickHandler = (s, ev) =>
                {
                    PlaceAllin placeAllin = new PlaceAllin(zoneId);
                    placeAllin.Dock = DockStyle.Fill;
                    this.Parent?.Controls.Clear();
                    this.Parent?.Controls.Add(placeAllin);
                };
                card.Click += clickHandler;
                lbl.Click += clickHandler;

                flowLayoutPanel1.Controls.Add(card);
            }

            dr.Close();
            db.CloseCon();
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
            this.flowLayoutPanel1.WrapContents = true;
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "PlaceOut";
            this.Size = new System.Drawing.Size(900, 600);
            this.Load += new System.EventHandler(this.PlaceOut_Load);
            this.ResumeLayout(false);
        }
    }
}
