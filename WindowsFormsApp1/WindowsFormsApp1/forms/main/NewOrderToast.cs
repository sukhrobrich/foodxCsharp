using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.main
{
    // Markaziydan yangi buyurtma (mobil/web) kelganda ekranning pastki o'ng
    // burchagida chiqadigan bildirishnoma (toast) + ovoz signali.
    internal class NewOrderToast : Form
    {
        const int W = 340, H = 92, MARGIN = 16, GAP = 10, LIFE_MS = 6000;

        static readonly List<NewOrderToast> _open = new List<NewOrderToast>();

        static readonly Color C_Bg     = Color.FromArgb(28, 32, 44);
        static readonly Color C_Title  = Color.White;
        static readonly Color C_Body   = Color.FromArgb(203, 213, 225);
        static readonly Color C_Close  = Color.FromArgb(148, 163, 184);
        static readonly Color C_Accent = Color.FromArgb(16, 185, 129);
        static readonly Color C_AccentDelivery = Color.FromArgb(245, 158, 11);
        public static readonly Color C_AccentWarning = Color.FromArgb(220, 38, 38);

        Timer _life;

        public static void Show(SyncEngine.NewOrderInfo info, Form owner = null)
        {
            string title = info.IsDelivery ? "🛵 Yangi yetkazib berish" : "🆕 Yangi buyurtma";
            string place = info.IsDelivery
                ? (string.IsNullOrWhiteSpace(info.CustomerName) ? "Yetkazib berish" : info.CustomerName)
                : (string.IsNullOrWhiteSpace(info.PlaceName) ? ("Buyurtma #" + info.OrderId) : info.PlaceName);
            string body = string.Format("{0}  •  {1:N0} so'm", place, info.Total);

            Show(title, body, info.IsDelivery ? C_AccentDelivery : C_Accent, owner);
        }

        // Har qanday toast (masalan: kam qolgan ingredient ogohlantirishi) uchun umumiy metod.
        public static void Show(string title, string body, Color accent, Form owner = null, SystemSound sound = null)
        {
            try { (sound ?? SystemSounds.Asterisk).Play(); } catch { }

            var toast = new NewOrderToast(title, body, accent);

            Screen screen = null;
            try { if (owner != null && !owner.IsDisposed) screen = Screen.FromControl(owner); } catch { }
            if (screen == null) screen = Screen.PrimaryScreen;

            _open.Add(toast);
            Reposition(screen);
            toast.Show();
        }

        NewOrderToast(string title, string body, Color accentColor)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            StartPosition   = FormStartPosition.Manual;
            Size            = new Size(W, H);
            BackColor       = C_Bg;

            Region = new Region(RoundRect(new Rectangle(0, 0, Width, Height), 12));

            Panel accent = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 6,
                BackColor = accentColor
            };
            Controls.Add(accent);

            Label lblTitle = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = C_Title,
                AutoSize  = false,
                Location  = new Point(22, 16),
                Size      = new Size(W - 60, 22)
            };
            Controls.Add(lblTitle);

            Label lblBody = new Label
            {
                Text      = body,
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = C_Body,
                AutoSize  = false,
                Location  = new Point(22, 42),
                Size      = new Size(W - 44, 36)
            };
            Controls.Add(lblBody);

            Label btnClose = new Label
            {
                Text      = "✕",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = C_Close,
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor    = Cursors.Hand,
                Location  = new Point(W - 30, 8),
                Size      = new Size(22, 22)
            };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);

            _life = new Timer { Interval = LIFE_MS };
            _life.Tick += (s, e) => Close();
            _life.Start();
        }

        static void Reposition(Screen screen)
        {
            var wa = screen.WorkingArea;
            int y = wa.Bottom - MARGIN;
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var t = _open[i];
                y -= t.Height;
                t.Location = new Point(wa.Right - MARGIN - t.Width, y);
                y -= GAP;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _life?.Stop();
            _life?.Dispose();
            _open.Remove(this);
            try { Reposition(Screen.PrimaryScreen); } catch { }
            base.OnFormClosed(e);
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
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
}
