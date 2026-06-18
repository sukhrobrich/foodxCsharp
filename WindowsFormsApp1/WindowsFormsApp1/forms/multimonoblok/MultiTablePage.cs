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
        private readonly string _userRole;

        private Panel  _scrollArea;
        private Timer  _refreshTimer;
        private Label  _lblWelcome;
        private Button _btnLayoutToggle;
        private string _layoutMode; // "horizontal" | "vertical"

        // Stol holati — yangilanish kerakligini aniqlash uchun
        private readonly Dictionary<int, string> _lastState = new Dictionary<int, string>();

        public MultiTablePage(MultiMonoblokClient client, int userId, string userName, string userRole = "ofitsiant")
        {
            _client      = client;
            _userId      = userId;
            _userName    = userName;
            _userRole    = userRole;
            _layoutMode  = MultiMonoblokConfig.TableLayoutMode;
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

            // Ko'rinish toggle tugmasi
            _btnLayoutToggle = new Button
            {
                Width     = 150, Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Muted,
                Font      = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand
            };
            _btnLayoutToggle.FlatAppearance.BorderSize  = 1;
            _btnLayoutToggle.FlatAppearance.BorderColor = Border;
            _btnLayoutToggle.Click += (s, e) =>
            {
                _layoutMode = _layoutMode == "horizontal" ? "vertical" : "horizontal";
                MultiMonoblokConfig.TableLayoutMode = _layoutMode;
                UpdateLayoutToggleBtn();
                _lastState.Clear(); // majburan qayta chizish
                _refreshTimer.Stop();
                RefreshAsync().ContinueWith(_ => { });
                _refreshTimer.Start();
            };
            UpdateLayoutToggleBtn();
            legend.Controls.Add(_btnLayoutToggle);
            legend.Resize += (s, e) =>
                _btnLayoutToggle.Location = new Point(legend.Width - _btnLayoutToggle.Width - 20,
                    (legend.Height - _btnLayoutToggle.Height) / 2);

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

        private void UpdateLayoutToggleBtn()
        {
            if (_btnLayoutToggle == null) return;
            if (_layoutMode == "horizontal")
            {
                _btnLayoutToggle.Text      = "⊞ Grid (tepadan pastga)";
                _btnLayoutToggle.ForeColor = Color.FromArgb(22, 163, 74);
                _btnLayoutToggle.FlatAppearance.BorderColor = Color.FromArgb(22, 163, 74);
            }
            else
            {
                _btnLayoutToggle.Text      = "→ Bir qator (o'ngga)";
                _btnLayoutToggle.ForeColor = Color.FromArgb(37, 99, 235);
                _btnLayoutToggle.FlatAppearance.BorderColor = Color.FromArgb(37, 99, 235);
            }
        }

        private void BuildTableView(List<string> places)
        {
            if (this.IsDisposed) return;
            _scrollArea.SuspendLayout();
            _scrollArea.Controls.Clear();

            var zones = new Dictionary<string, List<string>>();
            var zoneOrder = new List<string>();

            foreach (var p in places)
            {
                string zone = MultiMonoblokClient.JsonStr(p, "zone");
                if (string.IsNullOrEmpty(zone)) zone = "Umumiy";
                if (!zones.ContainsKey(zone)) { zones[zone] = new List<string>(); zoneOrder.Add(zone); }
                zones[zone].Add(p);
            }

            // Zona va stol nomlarini tabiiy tartibda saralash
            zoneOrder.Sort(NaturalCompare);
            foreach (var zone in zones.Keys)
                zones[zone].Sort((a, b) => NaturalCompare(
                    MultiMonoblokClient.JsonStr(a, "name"),
                    MultiMonoblokClient.JsonStr(b, "name")));

            const int cardW   = 148;
            const int cardH   = 120;
            const int cardGap = 12;
            const int flowH   = cardH + 16;

            int totalWidth = Math.Max(_scrollArea.ClientSize.Width - _scrollArea.Padding.Horizontal - 8, 300);
            int yOffset    = 0;

            foreach (string zone in zoneOrder)
            {
                var tableList = zones[zone];

                // Zona sarlavhasi
                Panel zoneHeader = new Panel { Location = new Point(0, yOffset), Width = totalWidth, Height = 36, BackColor = Color.Transparent };
                zoneHeader.Controls.Add(new Label
                {
                    Text = zone, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    ForeColor = TextDark, AutoSize = true, Location = new Point(4, 8)
                });
                _scrollArea.Controls.Add(zoneHeader);
                yOffset += 36;

                _scrollArea.Controls.Add(new Panel { Location = new Point(0, yOffset), Width = totalWidth, Height = 1, BackColor = Border });
                yOffset += 6;

                if (_layoutMode == "horizontal")
                {
                    // ── GRID rejim: ekran kenligini to'ldiradi, tepadan pastga wrap ──
                    const int minCardW  = 140;
                    const int cardMarg  = 8; // Margin(4) → har yon 4px = 8 jami
                    int cols    = Math.Max((totalWidth + cardMarg) / (minCardW + cardMarg), 2);
                    int gridW   = (totalWidth - cols * cardMarg) / cols;
                    int gridH   = 120;
                    int rows    = (int)Math.Ceiling((double)tableList.Count / cols);
                    int gridFlowH = rows * (gridH + cardMarg);

                    var flow = new FlowLayoutPanel
                    {
                        Location      = new Point(0, yOffset),
                        Width         = totalWidth,
                        Height        = gridFlowH,
                        FlowDirection = FlowDirection.LeftToRight,
                        WrapContents  = true,
                        Padding       = new Padding(0),
                        BackColor     = Color.Transparent
                    };
                    foreach (var t in tableList)
                        flow.Controls.Add(MakeTableCard(t, gridW, gridH));
                    _scrollArea.Controls.Add(flow);
                    yOffset += gridFlowH + 16;
                }
                else
                {
                    // ── VERTIKAL rejim: bir qator, o'ngga scroll ──
                    int hScrollH  = SystemInformation.HorizontalScrollBarHeight;
                    int rowPanelH = flowH + hScrollH + 4;
                    int flowWidth = tableList.Count * (cardW + cardGap) + cardGap;

                    Panel rowPanel = new Panel
                    {
                        Location   = new Point(0, yOffset),
                        Width      = totalWidth,
                        Height     = rowPanelH,
                        AutoScroll = true,
                        BackColor  = Color.Transparent
                    };
                    var flow = new FlowLayoutPanel
                    {
                        Location      = new Point(0, 0),
                        Width         = Math.Max(flowWidth, totalWidth),
                        Height        = flowH,
                        FlowDirection = FlowDirection.LeftToRight,
                        WrapContents  = false,
                        AutoSize      = false,
                        BackColor     = Color.Transparent
                    };
                    foreach (var t in tableList) flow.Controls.Add(MakeTableCard(t));
                    rowPanel.Controls.Add(flow);
                    _scrollArea.Controls.Add(rowPanel);
                    yOffset += rowPanelH + 16;
                }
            }

            if (places.Count == 0)
                _scrollArea.Controls.Add(new Label
                {
                    Text = "Stollar topilmadi",
                    Font = new Font("Segoe UI", 10), ForeColor = Muted,
                    Location = new Point(20, 20), AutoSize = true
                });

            _scrollArea.ResumeLayout();
        }

        // Tabiiy (raqamli) saralash: "Stol 2" < "Stol 10"
        private static int NaturalCompare(string a, string b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int i = 0, j = 0;
            while (i < a.Length || j < b.Length)
            {
                if (i >= a.Length) return -1;
                if (j >= b.Length) return 1;
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    long na = 0, nb = 0;
                    while (i < a.Length && char.IsDigit(a[i])) na = na * 10 + (a[i++] - '0');
                    while (j < b.Length && char.IsDigit(b[j])) nb = nb * 10 + (b[j++] - '0');
                    if (na != nb) return na.CompareTo(nb);
                }
                else
                {
                    int cmp = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }
            return 0;
        }

        private Control MakeTableCard(string t, int cardW = 148, int cardH = 120)
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

            int innerW = cardW - 8; // Labellar ichki kenglik

            Panel card = new Panel
            {
                Width = cardW, Height = cardH,
                Margin = new Padding(4), BackColor = bgColor, Cursor = Cursors.Hand
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
            card.Controls.Add(new Panel { Height = 4, Width = cardW, BackColor = borderColor, Location = new Point(0, 0) });

            // Stol nomi
            card.Controls.Add(new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, Width = innerW, Height = 24,
                Location = new Point(4, 10), TextAlign = ContentAlignment.MiddleCenter
            });

            int row2Y = (int)(cardH * 0.31);
            int row3Y = (int)(cardH * 0.47);
            int row4Y = cardH - 22;

            if (isEmpty)
            {
                card.Controls.Add(new Label
                {
                    Text = "Bo'sh", Font = new Font("Segoe UI", 9),
                    ForeColor = Success, Width = innerW, Height = 20,
                    Location = new Point(4, row2Y), TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(new Label
                {
                    Text = "Yangi buyurtma", Font = new Font("Segoe UI", 8),
                    ForeColor = Muted, Width = innerW, Height = 18,
                    Location = new Point(4, row3Y), TextAlign = ContentAlignment.MiddleCenter
                });
            }
            else
            {
                card.Controls.Add(new Label
                {
                    Text = isMine ? "Mening stolim" : $"Ofitsiant: {ownerNm}",
                    Font = new Font("Segoe UI", 8), ForeColor = borderColor,
                    Width = innerW, Height = 18, Location = new Point(4, row2Y),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(new Label
                {
                    Text = FormatMoney(total),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = TextDark, Width = innerW, Height = 22,
                    Location = new Point(4, row3Y), TextAlign = ContentAlignment.MiddleCenter
                });
            }

            card.Controls.Add(new Label
            {
                Text = isEmpty ? "+ Qo'shish" : "Ochish →",
                Font = new Font("Segoe UI", 8), ForeColor = borderColor,
                Width = innerW, Height = 18, Location = new Point(4, row4Y),
                TextAlign = ContentAlignment.MiddleCenter
            });

            EventHandler onClick = (s, e) =>
            {
                _refreshTimer.Stop();
                var orderForm = new MultiOrderForm(_client, tableId, name, orderId, _userRole);
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
