using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiTablePage : Form
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color BgMain   = Color.FromArgb(248, 248, 250);
        private static readonly Color BgCard   = Color.White;
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color Muted    = Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);

        private readonly MultiMonoblokClient _client;
        private readonly int    _userId;
        private readonly string _userName;

        private Panel  _scrollArea;
        private Timer  _refreshTimer;
        private Label  _lblWelcome;

        // Stol holati — yangilanish kerakligini aniqlash uchun
        private readonly Dictionary<int, string> _lastState = new Dictionary<int, string>();

        public MultiTablePage(MultiMonoblokClient client, int userId, string userName)
        {
            _client   = client;
            _userId   = userId;
            _userName = userName;
            BuildUI();

            this.Load += async (s, e) => await RefreshAsync();

            _refreshTimer = new Timer { Interval = 3000 };
            _refreshTimer.Tick += async (s, e) => await RefreshAsync();
            _refreshTimer.Start();
        }

        private void BuildUI()
        {
            this.WindowState     = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = BgMain;
            this.Text            = "FoodX — Ofitsiant Terminali";

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgCard };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            header.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(20, 18)
            });

            _lblWelcome = new Label
            {
                Text = $"Xush kelibsiz, {_userName}!",
                Font = new Font("Segoe UI", 11), ForeColor = Muted, AutoSize = true
            };
            header.Controls.Add(_lblWelcome);
            header.Resize += (s, e) =>
                _lblWelcome.Location = new Point((header.Width - _lblWelcome.Width) / 2, (header.Height - _lblWelcome.Height) / 2);

            Panel rightBtns = new Panel { Width = 300, Height = 64, BackColor = Color.Transparent };
            header.Controls.Add(rightBtns);
            header.Resize += (s, e) => rightBtns.Location = new Point(header.Width - 310, 0);

            Button btnRefresh = MakeHeaderBtn("Yangilash", Gold);
            btnRefresh.Location = new Point(0, 14);
            btnRefresh.Click   += async (s, e) => await RefreshAsync();
            rightBtns.Controls.Add(btnRefresh);

            Button btnLogout = MakeHeaderBtn("Chiqish", Danger);
            btnLogout.Location = new Point(160, 14);
            btnLogout.Click   += (s, e) => { _refreshTimer?.Stop(); this.Close(); };
            rightBtns.Controls.Add(btnLogout);

            // === LEGEND BAR ===
            Panel legend = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = BgCard, Padding = new Padding(20, 0, 20, 0) };
            legend.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, legend.Height - 1, legend.Width, legend.Height - 1);

            AddLegend(legend, Success, "Bo'sh", 20);
            AddLegend(legend, Danger, "Band", 110);
            AddLegend(legend, Gold, "Mening stolim", 200);

            // === SCROLL AREA ===
            _scrollArea = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, AutoScroll = true, Padding = new Padding(16) };

            this.Controls.Add(_scrollArea);
            this.Controls.Add(legend);
            this.Controls.Add(header);
        }

        private async Task RefreshAsync()
        {
            if (!this.IsHandleCreated) return;
            try
            {
                string json  = await _client.GetPlacesAsync();
                var places   = MultiMonoblokClient.JsonArr(json);

                // Holat o'zgardi?
                bool changed = false;
                var newState = new Dictionary<int, string>();
                foreach (var p in places)
                {
                    int    id    = MultiMonoblokClient.JsonInt(p, "id");
                    string empty = MultiMonoblokClient.JsonStr(p, "empty");
                    string oid   = MultiMonoblokClient.JsonStr(p, "active_order_id");
                    string state = $"{empty}|{oid}";
                    newState[id] = state;
                    if (!_lastState.ContainsKey(id) || _lastState[id] != state)
                        changed = true;
                }
                if (!changed && newState.Count == _lastState.Count) return;
                _lastState.Clear();
                foreach (var kv in newState) _lastState[kv.Key] = kv.Value;

                if (this.IsDisposed) return;
                this.BeginInvoke(new Action(() => BuildTableView(places)));
            }
            catch { }
        }

        private void BuildTableView(List<string> places)
        {
            if (this.IsDisposed) return;
            _scrollArea.SuspendLayout();
            _scrollArea.Controls.Clear();

            // Zonalar bo'yicha guruhlash
            var zones = new Dictionary<string, List<string>>();
            var zoneOrder = new List<string>();

            foreach (var p in places)
            {
                string zone = MultiMonoblokClient.JsonStr(p, "zone");
                if (string.IsNullOrEmpty(zone)) zone = "Umumiy";
                if (!zones.ContainsKey(zone)) { zones[zone] = new List<string>(); zoneOrder.Add(zone); }
                zones[zone].Add(p);
            }

            int yOffset = 8;
            int totalWidth = Math.Max(_scrollArea.ClientSize.Width - 32, 200);

            foreach (string zone in zoneOrder)
            {
                var tableList = zones[zone];

                // Zona sarlavhasi
                Panel zoneHeader = new Panel
                {
                    Location  = new Point(8, yOffset),
                    Width     = totalWidth,
                    Height    = 36,
                    BackColor = Color.Transparent
                };
                zoneHeader.Controls.Add(new Label
                {
                    Text = zone,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    ForeColor = TextDark, AutoSize = true, Location = new Point(4, 8)
                });
                _scrollArea.Controls.Add(zoneHeader);
                yOffset += 40;

                // Zona ajratgichi
                Panel zoneLine = new Panel { Location = new Point(8, yOffset), Width = totalWidth, Height = 1, BackColor = Border };
                _scrollArea.Controls.Add(zoneLine);
                yOffset += 8;

                // Stollar
                FlowLayoutPanel flow = new FlowLayoutPanel
                {
                    Location      = new Point(8, yOffset),
                    Width         = totalWidth,
                    AutoSize      = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents  = true,
                    BackColor     = Color.Transparent
                };

                foreach (var t in tableList)
                    flow.Controls.Add(MakeTableCard(t));

                _scrollArea.Controls.Add(flow);
                yOffset += flow.PreferredSize.Height + 20;
            }

            _scrollArea.ResumeLayout();
        }

        private Control MakeTableCard(string t)
        {
            int    tableId  = MultiMonoblokClient.JsonInt(t, "id");
            string name     = MultiMonoblokClient.JsonStr(t, "name");
            string empty    = MultiMonoblokClient.JsonStr(t, "empty");
            string oidStr   = MultiMonoblokClient.JsonStr(t, "active_order_id");
            string ownerStr = MultiMonoblokClient.JsonStr(t, "active_order_user_id");
            decimal total   = MultiMonoblokClient.JsonDec(t, "active_order_total");
            string ownerNm  = MultiMonoblokClient.JsonStr(t, "active_order_user_name");

            int.TryParse(oidStr, out int orderId);
            int.TryParse(ownerStr, out int ownerId);

            bool isEmpty  = empty?.ToUpper() == "YES";
            bool isMine   = ownerId == _userId && !isEmpty;

            Color borderColor = isEmpty ? Success : (isMine ? Gold : Danger);
            Color bgColor     = isEmpty ? Color.FromArgb(240, 253, 244)
                              : isMine  ? Color.FromArgb(255, 248, 230)
                              :           Color.FromArgb(254, 242, 242);

            Panel card = new Panel
            {
                Width = 148, Height = 120,
                Margin = new Padding(6), BackColor = bgColor, Cursor = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    using (var pen = new Pen(borderColor, 2)) e.Graphics.DrawPath(pen, path);
                }
            };

            // Holat rangi — yuqori chiziq
            card.Controls.Add(new Panel { Height = 4, Width = card.Width, BackColor = borderColor, Location = new Point(0, 0) });

            // Stol nomi
            card.Controls.Add(new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, Width = 140, Height = 24,
                Location = new Point(4, 10), TextAlign = ContentAlignment.MiddleCenter
            });

            if (isEmpty)
            {
                card.Controls.Add(new Label
                {
                    Text = "Bo'sh", Font = new Font("Segoe UI", 9),
                    ForeColor = Success, Width = 140, Height = 20,
                    Location = new Point(4, 38), TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(new Label
                {
                    Text = "Yangi buyurtma", Font = new Font("Segoe UI", 8),
                    ForeColor = Muted, Width = 140, Height = 18,
                    Location = new Point(4, 58), TextAlign = ContentAlignment.MiddleCenter
                });
            }
            else
            {
                card.Controls.Add(new Label
                {
                    Text = isMine ? "Mening stolim" : $"Ofitsiant: {ownerNm}",
                    Font = new Font("Segoe UI", 8), ForeColor = borderColor,
                    Width = 140, Height = 18, Location = new Point(4, 38), TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(new Label
                {
                    Text = FormatMoney(total),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = TextDark, Width = 140, Height = 22,
                    Location = new Point(4, 58), TextAlign = ContentAlignment.MiddleCenter
                });
            }

            card.Controls.Add(new Label
            {
                Text = isEmpty ? "+ Qo'shish" : "Ochish →",
                Font = new Font("Segoe UI", 8), ForeColor = borderColor,
                Width = 140, Height = 18, Location = new Point(4, 95),
                TextAlign = ContentAlignment.MiddleCenter
            });

            EventHandler onClick = (s, e) =>
            {
                _refreshTimer.Stop();
                var orderForm = new MultiOrderForm(_client, tableId, name, orderId);
                orderForm.FormClosed += async (fs, fe) =>
                {
                    await RefreshAsync();
                    _refreshTimer.Start();
                };
                orderForm.ShowDialog(this);
            };
            card.Click += onClick;
            foreach (Control c in card.Controls) c.Click += onClick;

            return card;
        }

        private static string FormatMoney(decimal amount)
        {
            if (amount <= 0) return "";
            return string.Format("{0:N0}", amount).Replace(",", " ") + " so'm";
        }

        private static void AddLegend(Panel parent, Color color, string text, int x)
        {
            Panel dot = new Panel { Width = 12, Height = 12, BackColor = color, Location = new Point(x, 14) };
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(color), 0, 0, 11, 11);
            };
            parent.Controls.Add(dot);
            parent.Controls.Add(new Label
            {
                Text = text, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(107, 114, 128),
                AutoSize = true, Location = new Point(x + 16, 13)
            });
        }

        private static Button MakeHeaderBtn(string text, Color bg)
        {
            var b = new Button
            {
                Text = text, Width = 130, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.OnFormClosed(e);
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
}
