using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using WindowsFormsApp1.forms.settings;
using WindowsFormsApp1.forms.user;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private FlowLayoutPanel flpUsers;

        // Light theme palette
        private static readonly Color BgPage   = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard   = Color.White;
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);

        // true bo'lsa bu monoblok faqat ofitsiant terminali
        public static bool IsWaiterTerminal =>
            File.Exists(ConnectionSetupForm.CfgFile);

        public Form1()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BgPage;

            // === FOOTER ===
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = BgCard };
            footer.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, footer.Width, 0);
            var lblStatus = new Label
            {
                Text = $"FoodX v2.0  •  {(Session.IsOnline ? "Online ✓" : "Offline ✗")}  •  TenantId:{Session.TenantId}",
                Font = new Font("Segoe UI", 9),
                ForeColor = Session.IsOnline ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            footer.Controls.Add(lblStatus);

            Button btnDbConn = new Button
            {
                Text = "⚙ Ulanish",
                AutoSize = true, Height = 26,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent,
                ForeColor = TextMuted, Font = new Font("Segoe UI", 8),
                Cursor = Cursors.Hand
            };
            btnDbConn.FlatAppearance.BorderSize = 0;
            btnDbConn.MouseEnter += (s, e) => btnDbConn.ForeColor = Gold;
            btnDbConn.MouseLeave += (s, e) => btnDbConn.ForeColor = TextMuted;
            btnDbConn.Click += (s, e) =>
            {
                new ConnectionSetupForm().ShowDialog();
                LoadEmployees(); // reload in case terminal type changed
            };
            footer.Controls.Add(btnDbConn);
            footer.Resize += (s, e) =>
                btnDbConn.Location = new Point(footer.Width - btnDbConn.Width - 12, (footer.Height - btnDbConn.Height) / 2);

            this.Controls.Add(footer);

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 170, BackColor = BgCard };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            Label logo = new Label
            {
                Text = "FoodX",
                Font = new Font("Segoe UI", 52, FontStyle.Bold),
                ForeColor = Gold,
                AutoSize = true
            };
            Label subtitle = new Label
            {
                Text = IsWaiterTerminal
                    ? "Ofitsiant Terminali"
                    : "Kafe va Restoran Boshqaruv Tizimi",
                Font = new Font("Segoe UI", 12),
                ForeColor = TextMuted,
                AutoSize = true
            };
            header.Controls.Add(logo);
            header.Controls.Add(subtitle);
            header.Resize += (s, e) =>
            {
                logo.Location = new Point((header.Width - logo.Width) / 2, 24);
                subtitle.Location = new Point((header.Width - subtitle.Width) / 2, 110);
            };
            // === SELECT LABEL ===
            Panel lblPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = BgPage };
            new Label
            {
                Text = IsWaiterTerminal
                    ? "Ofitsiantni tanlang"
                    : "Foydalanuvchini tanlang",
                Font = new Font("Segoe UI", 11),
                ForeColor = TextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            }.Also(l => lblPanel.Controls.Add(l));

            // === USERS GRID ===
            flpUsers = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(40, 20, 40, 20),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            // Fill must be added before Top controls; Top controls added last = topmost
            this.Controls.Add(flpUsers);
            this.Controls.Add(lblPanel);
            this.Controls.Add(header);

            this.Load += (s, e) => { ReorderForm.EnsureAllOrderColumns(); LoadEmployees(); };
        }

        private void LoadEmployees()
        {
            flpUsers.Controls.Clear();
            userCategoryAdd.EnsureColorColumn();
            dbconnect db = new dbconnect();
            string query = @"SELECT u.id, u.name, u.login, uc.name AS category, ISNULL(uc.color, '') AS color
                    FROM [user] u
                    JOIN user_category uc ON u.user_category_id = uc.id
                    ORDER BY ISNULL(u.sort_order, 9999), u.name";
            try
            {
                SqlCommand cmd = new SqlCommand(query, db.GetCon());
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();
                int count = 0;
                while (dr.Read())
                {
                    count++;
                    flpUsers.Controls.Add(CreateUserCard(
                        Convert.ToInt32(dr["id"]),
                        dr["name"].ToString(),
                        dr["login"].ToString(),
                        dr["category"].ToString(),
                        dr["color"].ToString()));
                }
                dr.Close();
                if (count == 0)
                {
                    MessageBox.Show(
                        $"Foydalanuvchilar topilmadi!\n\n" +
                        $"IsOnline: {Session.IsOnline}\n" +
                        $"TenantId: {Session.TenantId}\n" +
                        $"ForceOffline: {Session.ForceOffline}\n\n" +
                        "Agar Online=True va TenantId=0 bo'lsa — litsenziya qayta tekshirilsin.\n" +
                        "Agar Online=False bo'lsa — server bilan ulanish tekshirilsin.",
                        "FoodX — Diagnostika", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Xatolik: {ex.Message}\n\nIsOnline:{Session.IsOnline}  TenantId:{Session.TenantId}",
                    "FoodX — Xatolik", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { db.CloseCon(); }
        }

        private Control CreateUserCard(int userId, string name, string login, string category, string colorHex)
        {
            Color roleColor = ParseColor(colorHex);
            string initials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();

            Panel card = new Panel
            {
                Width = 192,
                Height = 208,
                Margin = new Padding(12),
                BackColor = BgCard,
                Cursor = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 12))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    using (var pen = new Pen(Border, 1.5f))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            // Colored top accent bar
            Panel accent = new Panel { Height = 4, Width = card.Width, BackColor = roleColor, Location = new Point(0, 0) };
            card.Controls.Add(accent);

            // Avatar
            Panel avatar = new Panel
            {
                Width = 76,
                Height = 76,
                BackColor = Color.Transparent,
                Location = new Point((card.Width - 76) / 2, 20)
            };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(243, 244, 246)), 0, 0, 75, 75);
                e.Graphics.DrawEllipse(new Pen(roleColor, 2.5f), 1, 1, 72, 72);
                using (Font f = new Font("Segoe UI", 22, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(initials, f, new SolidBrush(roleColor), new RectangleF(0, 0, 75, 75), sf);
            };
            card.Controls.Add(avatar);

            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                Width = 172,
                Height = 24,
                Location = new Point(10, 108),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblName);

            Label lblRole = new Label
            {
                Text = category,
                Font = new Font("Segoe UI", 9),
                ForeColor = roleColor,
                Width = 172,
                Height = 20,
                Location = new Point(10, 133),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblRole);

            Panel divider = new Panel
            {
                Height = 1,
                Width = 120,
                BackColor = Border,
                Location = new Point(36, 160)
            };
            card.Controls.Add(divider);

            Label lblHint = new Label
            {
                Text = "Kirish uchun bosing",
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted,
                Width = 172,
                Height = 18,
                Location = new Point(10, 170),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblHint);

            // Hover effect
            Color normalBg = BgCard;
            Color hoverBg = GoldBg;
            Action<bool> setHover = (on) =>
            {
                card.BackColor = on ? hoverBg : normalBg;
                card.Refresh();
            };
            card.MouseEnter += (s, e) => setHover(true);
            card.MouseLeave += (s, e) => setHover(false);
            foreach (Control c in card.Controls)
            {
                c.MouseEnter += (s, e) => setHover(true);
                c.MouseLeave += (s, e) => setHover(false);
            }

            EventHandler onClick = (s, e) => { new Password(login, true).Show(); this.Hide(); };
            card.Click += onClick;
            foreach (Control c in card.Controls)
                c.Click += onClick;

            return card;
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex))
                try { return ColorTranslator.FromHtml(hex); } catch { }
            return Color.FromArgb(22, 163, 74);
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
    }

    internal static class ControlExtensions
    {
        public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}
