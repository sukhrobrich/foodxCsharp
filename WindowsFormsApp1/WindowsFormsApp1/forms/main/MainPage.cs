using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.forms.food;
using WindowsFormsApp1.forms.order;
using WindowsFormsApp1.forms.payment;
using WindowsFormsApp1.forms.place;
using WindowsFormsApp1.forms.report;
using WindowsFormsApp1.forms.settings;
using WindowsFormsApp1.forms.user;
using WindowsFormsApp1.forms.cashflow;
using WindowsFormsApp1.forms.customer;
using WindowsFormsApp1.forms.warehouse;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.main
{
    public partial class MainPage : Form
    {
        private readonly int userID;
        private Panel panelContent;
        private Panel menuScrollPanel;
        private List<Action<bool>> menuSetters = new List<Action<bool>>();
        private int activeMenuIndex = -1;
        private Action<StockAlertService.LowStockInfo> _lowStockToastHandler;
        private Action<SyncEngine.NewOrderInfo> _newOrderToastHandler;

        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldLight = Color.FromArgb(255, 248, 230);
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextBody  = Color.FromArgb(55, 65, 81);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);

        public MainPage(int userId)
        {
            userID = userId;
            BuildUI();
        }

        private void BuildUI()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BgPage;
            this.Text = "FoodX — Dashboard";

            // ======= SIDEBAR =======
            Panel sidebar = new Panel { Width = 240, Dock = DockStyle.Left, BackColor = BgCard };
            sidebar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            this.Controls.Add(sidebar);

            // Logout (Bottom) — add first so it's positioned at bottom
            Panel logoutPanel = new Panel { Height = 56, Dock = DockStyle.Bottom, BackColor = BgCard };
            logoutPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, logoutPanel.Width, 0);
            Button btnLogout = new Button
            {
                Text = "Chiqish",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.MouseEnter += (s, e) => { btnLogout.ForeColor = Color.White; btnLogout.BackColor = Color.FromArgb(220, 38, 38); };
            btnLogout.MouseLeave += (s, e) => { btnLogout.ForeColor = TextMuted; btnLogout.BackColor = Color.Transparent; };
            btnLogout.Click += (s, e) =>
            {
                try { PrintService.SetSetting("last_logged_in_user", ""); } catch { }
                Session.Clear();
                this.Hide();
                new Form1().Show();
            };
            logoutPanel.Controls.Add(btnLogout);
            sidebar.Controls.Add(logoutPanel);

            // Menu scroll panel (Fill) — add before Top controls
            menuScrollPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgCard, AutoScroll = true };
            sidebar.Controls.Add(menuScrollPanel);

            // User info box (Top) — add before logo so logo goes above it
            Panel userBox = new Panel { Height = 60, Dock = DockStyle.Top, BackColor = BgCard };
            userBox.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, userBox.Height - 1, userBox.Width, userBox.Height - 1);
            Color roleColor = Color.FromArgb(220, 38, 38);
            string roleName = "Administrator";
            userBox.Controls.Add(new Label
            {
                Text = Session.UserName,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(16, 8),
                AutoSize = false, Width = 208, Height = 22
            });
            userBox.Controls.Add(new Label
            {
                Text = roleName,
                Font = new Font("Segoe UI", 9),
                ForeColor = roleColor,
                Location = new Point(16, 32),
                AutoSize = false, Width = 208, Height = 16
            });
            sidebar.Controls.Add(userBox);

            // Logo area (Top) — add LAST so it appears at very top
            Panel logoPanel = new Panel { Height = 72, Dock = DockStyle.Top, BackColor = BgCard };
            logoPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, logoPanel.Height - 1, logoPanel.Width, logoPanel.Height - 1);
            logoPanel.Controls.Add(new Label
            {
                Text = "FoodX",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Gold,
                Location = new Point(0, 8),
                Height = 36, Width = 240,
                TextAlign = ContentAlignment.MiddleCenter
            });
            Label lblClock = new Label
            {
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = TextMuted,
                Location = new Point(0, 48),
                Height = 18, Width = 240,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm")
            };
            logoPanel.Controls.Add(lblClock);
            Timer clock = new Timer { Interval = 1000 };
            clock.Tick += (s, e) => lblClock.Text = DateTime.Now.ToString("dd.MM.yyyy  HH:mm");
            clock.Start();
            sidebar.Controls.Add(logoPanel);

            // Build menu items with explicit Y positions
            BuildMenuItems();

            // ======= RIGHT SIDE =======
            Panel rightSide = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            this.Controls.Add(rightSide);
            rightSide.BringToFront();

            panelContent = new Panel { Dock = DockStyle.Fill, BackColor = BgPage };
            rightSide.Controls.Add(panelContent);

            this.Load += (s, e) => NavigateTo(1, "Stollar");

            // ── Yangi buyurtma bildirishnomasi (toast + ovoz) ───────────────────
            _newOrderToastHandler = info =>
            {
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && PrintService.GetSetting("new_order_notify", "1") == "1")
                            NewOrderToast.Show(info, this);
                    }));
                }
                catch { }
            };
            SyncEngine.NewOrderCreated += _newOrderToastHandler;

            // ── Kam qolgan ingredient bildirishnomasi (toast + ovoz) ────────────
            _lowStockToastHandler = info =>
            {
                if (IsDisposed) return;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && PrintService.GetSetting("low_stock_notify", "1") == "1")
                            NewOrderToast.Show("⚠ Kam qolgan mahsulot",
                                string.Format("{0}  •  {1:N1} {2} qoldi", info.Name, info.Quantity, info.Unit),
                                NewOrderToast.C_AccentWarning, this);
                    }));
                }
                catch { }
            };
            StockAlertService.LowStockDetected += _lowStockToastHandler;
            this.FormClosed += (s, e) =>
            {
                if (_newOrderToastHandler != null)
                {
                    SyncEngine.NewOrderCreated -= _newOrderToastHandler;
                    _newOrderToastHandler = null;
                }
                if (_lowStockToastHandler != null)
                {
                    StockAlertService.LowStockDetected -= _lowStockToastHandler;
                    _lowStockToastHandler = null;
                }
            };
        }

        private void BuildMenuItems()
        {
            menuSetters.Clear();
            menuScrollPanel.Controls.Clear();

            int y = 8;

            AddSectionLabel("ASOSIY", ref y);
            AddMenuItem("Zakaslar",     "≡", ref y);
            AddMenuItem("Stollar",      "⊞", ref y);
            AddMenuItem("Hisobotlar",   "▤", ref y);

            if (Session.IsAdmin)
            {
                Panel div = new Panel
                {
                    Left = 16, Width = 208, Height = 1,
                    Top = y + 6, BackColor = Border
                };
                menuScrollPanel.Controls.Add(div);
                y += 16;

                AddSectionLabel("BOSHQARUV", ref y);
                AddMenuItem("Taomlar",       "✦", ref y);
                AddMenuItem("Kategoriyalar", "◈", ref y);
                AddMenuItem("Joylar",        "⊡", ref y);
                AddMenuItem("To'lovlar",     "₴", ref y);
                AddMenuItem("Ombor",         "⊠", ref y);
                AddMenuItem("Xarajatlar",    "₿", ref y);
                AddMenuItem("Mijozlar",       "◎", ref y);
                AddMenuItem("Xodimlar",      "◉", ref y);
                AddMenuItem("Sozlamalar",    "⚙", ref y);
            }
        }

        private void AddSectionLabel(string title, ref int y)
        {
            menuScrollPanel.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(16, y + 6),
                Width = 208, Height = 20,
                AutoSize = false,
                TextAlign = ContentAlignment.BottomLeft
            });
            y += 32;
        }

        private void AddMenuItem(string text, string icon, ref int y)
        {
            int index = menuSetters.Count;
            int itemY = y;

            Panel item = new Panel
            {
                Location = new Point(0, itemY),
                Width = 236, Height = 44,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            Panel accent = new Panel
            {
                Location = new Point(0, 0),
                Width = 3, Height = 44,
                BackColor = Color.Transparent
            };

            Label lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 13),
                ForeColor = TextMuted,
                Location = new Point(14, 5),
                Size = new Size(32, 34),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblText = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10),
                ForeColor = TextBody,
                Location = new Point(52, 13),
                Size = new Size(172, 20),
                AutoSize = false
            };

            item.Controls.Add(accent);
            item.Controls.Add(lblIcon);
            item.Controls.Add(lblText);
            menuScrollPanel.Controls.Add(item);

            Action<bool> setter = (active) =>
            {
                if (active)
                {
                    item.BackColor = GoldLight;
                    accent.BackColor = Gold;
                    lblIcon.ForeColor = Gold;
                    lblText.ForeColor = Gold;
                    lblText.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                }
                else
                {
                    item.BackColor = Color.Transparent;
                    accent.BackColor = Color.Transparent;
                    lblIcon.ForeColor = TextMuted;
                    lblText.ForeColor = TextBody;
                    lblText.Font = new Font("Segoe UI", 10);
                }
            };
            menuSetters.Add(setter);

            Action<bool> hover = (on) =>
            {
                if (activeMenuIndex == index) return;
                item.BackColor = on ? Color.FromArgb(243, 244, 246) : Color.Transparent;
                lblIcon.ForeColor = on ? TextBody : TextMuted;
                lblText.ForeColor = TextBody;
            };

            EventHandler onClick = (s, e) => NavigateTo(index, text);
            item.Click += onClick;
            foreach (Control c in item.Controls)
            {
                c.Click += onClick;
                c.MouseEnter += (s, e) => hover(true);
                c.MouseLeave += (s, e) => hover(false);
            }
            item.MouseEnter += (s, e) => hover(true);
            item.MouseLeave += (s, e) => hover(false);

            y += 44;
        }

        private void NavigateTo(int index, string page)
        {
            if (activeMenuIndex >= 0 && activeMenuIndex < menuSetters.Count)
                menuSetters[activeMenuIndex](false);
            activeMenuIndex = index;
            if (index >= 0 && index < menuSetters.Count)
                menuSetters[index](true);

            panelContent.Controls.Clear();

            UserControl uc = null;
            switch (page)
            {
                case "Zakaslar":      uc = new OrdersPanel();     break;
                case "Stollar":       uc = new PlaceAll();        break;
                case "Kategoriyalar": uc = new AddFoodCategory(); break;
                case "Taomlar":       uc = new AddFood();         break;
                case "Joylar":        uc = new Place();           break;
                case "To'lovlar":    uc = new PaymentPanel();    break;
                case "Hisobotlar":    uc = new ReportPage();      break;
                case "Ombor":         uc = new WarehousePanel();  break;
                case "Xarajatlar":    uc = new CashFlowPanel();   break;
                case "Mijozlar":      uc = new CustomerPanel();   break;
                case "Xodimlar":      uc = new UserAdd();         break;
                case "Sozlamalar":    uc = new SettingsPanel();   break;
                default: return;
            }
            if (uc != null)
            {
                uc.Dock = DockStyle.Fill;
                panelContent.Controls.Add(uc);
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(1400, 800);
            this.Name = "MainPage";
            this.ResumeLayout(false);
        }
    }
}
