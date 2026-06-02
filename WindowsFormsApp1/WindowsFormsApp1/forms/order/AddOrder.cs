using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.order
{
    public partial class AddOrder : Form
    {
        private int placeId;
        private int orderId;
        private readonly string callerRole;

        private Panel panelCategories;
        private FlowLayoutPanel flpFoods;
        private FlowLayoutPanel flpCart;
        private Label lblTotal;
        private ComboBox comboPayment;
        private Label lblTableName;
        private Button btnSave, btnCloseOrder;
        private bool    _canEditItems;
        private bool    _canClose;
        private decimal _placePrice;
        private string  _placePriceType = "UZS";
        private bool    _hasServiceFee;
        private Label   lblPlaceInfo;
        private bool    _photoMode = false;
        private int     _currentCategoryId = -1;
        private TextBox _txtSearch;

        private decimal _discountPct   = 0;
        private int     _customerId    = 0;
        private string  _customerName  = "";
        private string  _deliveryPhone = "";
        private string  _deliveryAddr  = "";
        private string  _orderNote     = "";
        private bool    _isDelivery    = false;
        private decimal _customSvcFee  = 0;
        private string  _customSvcType = "%";
        private string  _debtName      = "";
        private string  _debtPhone     = "";
        private string  _debtNote      = "";
        private Button  _btnMijoz;
        private Button  _btnPayment;
        private Button  _btnDelivery;
        private Button  _btnBoshqalar;
        private List<(int id, string name, decimal amount)> _paymentEntries = new List<(int id, string name, decimal amount)>();

        private static readonly Color Sidebar  = Color.FromArgb(249, 249, 251);
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain   = Color.FromArgb(249, 249, 251);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);

        public AddOrder(int placeId, int orderId, string callerRole)
        {
            this.placeId = placeId;
            this.orderId = orderId;
            this.callerRole = callerRole?.ToLower() ?? "";
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = BgMain;
            this.Text = "FoodX — Zakaz";

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);

            Button btnBack = new Button
            {
                Text = "← Orqaga", Width = 120, Height = 36,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark, Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand, Location = new Point(16, 13)
            };
            btnBack.FlatAppearance.BorderSize = 1;
            btnBack.FlatAppearance.BorderColor = Border;
            btnBack.Click += (s, e) => this.Close();
            header.Controls.Add(btnBack);

            Label logo = new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            header.Controls.Add(logo);
            header.Resize += (s, e) =>
                logo.Location = new Point((header.Width - logo.Width) / 2, (header.Height - logo.Height) / 2);

            lblTableName = new Label
            {
                Text = "Yuklanmoqda...", Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true
            };
            header.Controls.Add(lblTableName);
            header.Resize += (s, e) =>
                lblTableName.Location = new Point(header.Width - lblTableName.Width - 24, (header.Height - lblTableName.Height) / 2);

            // === LEFT: CATEGORIES (200px) ===
            panelCategories = new Panel
            {
                Width = 200, Dock = DockStyle.Left,
                BackColor = CardBg, AutoScroll = true
            };
            panelCategories.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), panelCategories.Width - 1, 0, panelCategories.Width - 1, panelCategories.Height);

            Label lblCatTitle = new Label
            {
                Text = "KATEGORIYALAR",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(14, 14),
                Width = 172, Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panelCategories.Controls.Add(lblCatTitle);

            // === RIGHT: CART (380px) ===
            Panel cartPanel = new Panel { Width = 380, Dock = DockStyle.Right, BackColor = CardBg };
            cartPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, cartPanel.Height);

            // Cart header
            Panel cartHdr = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = CardBg };
            cartHdr.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, cartHdr.Height - 1, cartHdr.Width, cartHdr.Height - 1);
            Label lblCartTitle = new Label
            {
                Text = "Zakaz tarkibi", Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, Location = new Point(20, 0),
                Height = 56, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft
            };
            cartHdr.Controls.Add(lblCartTitle);
            cartHdr.Resize += (s, e) => lblCartTitle.Width = cartHdr.Width - 40;
            lblCartTitle.Width = 340;

            // Cart bottom (total + payment + buttons)
            bool kassirDelete     = PrintService.GetSetting("kassir_delete")     == "1";
            bool waiterDelete     = PrintService.GetSetting("waiter_delete")     == "1";
            bool orderAnyoneClose = PrintService.GetSetting("order_anyone_close") == "1";
            _canEditItems = Session.IsAdmin || (Session.IsKassir && kassirDelete) || (Session.IsWaiter && waiterDelete);
            _canClose = Session.IsAdmin || Session.IsKassir || (Session.IsWaiter && orderAnyoneClose);

            // comboPayment always created (populated by LoadPayments) so SaveOrder can use first payment ID
            comboPayment = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10), FlatStyle = FlatStyle.Flat, BackColor = BgMain
            };

            bool showActionBtns = !Session.IsWaiter;
            int  sepY           = showActionBtns ? 140 : 8;
            Panel cartBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = showActionBtns
                    ? (_canClose ? 306 : 252)
                    : (_canClose ? 174 : 118),
                BackColor = CardBg
            };
            cartBottom.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, cartBottom.Width, 0);

            // ── 2×2 action button grid (admin/kassir only) ─────────────────
            _btnMijoz     = MakeActionBtn("◎", "Mijoz");
            _btnPayment   = MakeActionBtn("◈", "To'lov turi");
            _btnDelivery  = MakeActionBtn("⊙", "Yetkazib berish");
            _btnBoshqalar = MakeActionBtn("⋯", "Boshqalar");

            _btnMijoz.Click     += (s, e) => ShowCustomerPicker();
            _btnPayment.Click   += (s, e) => ShowPaymentPicker();
            _btnDelivery.Click  += (s, e) => ShowDeliveryDialog();
            _btnBoshqalar.Click += (s, e) => ShowBoshqalarMenu(_btnBoshqalar);

            if (showActionBtns)
            {
                cartBottom.Controls.Add(_btnMijoz);
                cartBottom.Controls.Add(_btnPayment);
                cartBottom.Controls.Add(_btnDelivery);
                cartBottom.Controls.Add(_btnBoshqalar);
            }

            // ── Separator ──────────────────────────────────────────────────
            Panel sep = new Panel { BackColor = Border, Location = new Point(0, sepY), Height = 1 };
            cartBottom.Controls.Add(sep);

            // ── Total row ──────────────────────────────────────────────────
            cartBottom.Controls.Add(new Label
            {
                Text = "Jami:", Font = new Font("Segoe UI", 11),
                ForeColor = TextMuted, Location = new Point(14, sepY + 8), AutoSize = true
            });
            lblTotal = new Label
            {
                Text = "0 so'm", Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true
            };
            cartBottom.Controls.Add(lblTotal);

            lblPlaceInfo = new Label
            {
                Text = "", Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted,
                Location = new Point(14, sepY + 34), Height = 16, AutoSize = false, Visible = false
            };
            cartBottom.Controls.Add(lblPlaceInfo);

            // ── Save button ────────────────────────────────────────────────
            btnSave = new Button
            {
                Text = "Saqlash", Location = new Point(8, sepY + 54), Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(22, 163, 74),
                ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveOrder;
            cartBottom.Controls.Add(btnSave);

            // ── Yopish button (canClose only) ──────────────────────────────
            if (_canClose)
            {
                btnCloseOrder = new Button
                {
                    Text = "Yopish", Location = new Point(8, sepY + 110), Height = 48,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
                };
                btnCloseOrder.FlatAppearance.BorderSize = 0;
                btnCloseOrder.Click += CloseOrder;
                cartBottom.Controls.Add(btnCloseOrder);
            }

            // ── Responsive layout ──────────────────────────────────────────
            void LayoutCartBottom()
            {
                int W  = cartBottom.Width;
                int hw = Math.Max(60, (W - 28) / 2);
                if (showActionBtns)
                {
                    _btnMijoz.SetBounds(8, 8, hw, 58);
                    _btnPayment.SetBounds(W - hw - 8, 8, hw, 58);
                    _btnDelivery.SetBounds(8, 74, hw, 58);
                    _btnBoshqalar.SetBounds(W - hw - 8, 74, hw, 58);
                    comboPayment.SetBounds(_btnPayment.Left, _btnPayment.Bottom - 28, _btnPayment.Width, 28);
                }
                sep.Width = W;
                lblTotal.Location  = new Point(W - lblTotal.Width - 14, sepY + 6);
                lblPlaceInfo.Width = W - 28;
                btnSave.Width = W - 16;
                if (btnCloseOrder != null) btnCloseOrder.Width = W - 16;
            }
            // comboPayment — hidden, overlaid on _btnPayment and dropped down on demand
            comboPayment.Visible = false;
            comboPayment.Height  = 28;
            comboPayment.SelectionChangeCommitted += (s, e) =>
            {
                string nm = (comboPayment.SelectedItem as DataRowView)?["name"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(nm)) { _btnPayment.Text = "◈\n" + nm; _btnPayment.BackColor = GoldBg; }
                comboPayment.Visible = false;
            };
            comboPayment.Leave += (s, e) => comboPayment.Visible = false;
            cartBottom.Controls.Add(comboPayment);

            cartBottom.Resize += (s, e) => LayoutCartBottom();
            LayoutCartBottom();
            // Cart scroll list — Fill FIRST (WinForms docking: Fill must be added before Top/Bottom)
            flpCart = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true,
                BackColor = BgMain, Padding = new Padding(10, 10, 10, 10)
            };
            flpCart.HorizontalScroll.Maximum = 0;
            flpCart.HorizontalScroll.Enabled = false;
            flpCart.HorizontalScroll.Visible = false;
            flpCart.Resize += (s, e) =>
            {
                int w = Math.Max(100, flpCart.ClientSize.Width - 2);
                foreach (Control c in flpCart.Controls)
                    if (c is Panel p) p.Width = w;
            };
            cartPanel.Controls.Add(flpCart);    // Fill FIRST
            cartPanel.Controls.Add(cartBottom); // Bottom
            cartPanel.Controls.Add(cartHdr);    // Top LAST

            // === MIDDLE: FOODS (Fill) ===
            Panel foodsPanel = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };

            Panel foodsHdr = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = CardBg };
            foodsHdr.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, foodsHdr.Height - 1, foodsHdr.Width, foodsHdr.Height - 1);
            foodsHdr.Controls.Add(new Label
            {
                Text = "Menu", Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true, Location = new Point(20, 15)
            });

            Button btnToggleText = new Button
            {
                Text = "≡", Width = 36, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 16, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnToggleText.FlatAppearance.BorderSize = 0;

            Button btnTogglePhoto = new Button
            {
                Text = "⊞", Width = 36, Height = 36, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextMuted,
                Font = new Font("Segoe UI", 13), Cursor = Cursors.Hand
            };
            btnTogglePhoto.FlatAppearance.BorderSize = 0;

            foodsHdr.Controls.Add(btnToggleText);
            foodsHdr.Controls.Add(btnTogglePhoto);

            // ── Search box ───────────────────────────────────────────────────
            const string placeholder = "Taom qidirish...";
            const int    SearchW     = 340;

            Panel searchWrap = new Panel
            {
                Width = SearchW, Height = 36,
                BackColor = Color.FromArgb(243, 244, 246),
                Cursor = Cursors.IBeam
            };
            searchWrap.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var rp = RoundRect(new Rectangle(0, 0, searchWrap.Width - 1, searchWrap.Height - 1), 8))
                {
                    e.Graphics.FillPath(new SolidBrush(searchWrap.BackColor), rp);
                    e.Graphics.DrawPath(new Pen(_txtSearch != null && _txtSearch.Focused ? Gold : Border, 1.5f), rp);
                }
            };

            Label lblIcon = new Label
            {
                Text = "Q", Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextMuted, BackColor = Color.Transparent,
                Location = new Point(10, 9), AutoSize = true, Cursor = Cursors.IBeam
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor   = Color.FromArgb(243, 244, 246),
                ForeColor   = TextMuted,
                Font        = new Font("Segoe UI", 11),
                Location    = new Point(30, 8),
                Width       = SearchW - 38,
                Height      = 20,
                Text        = placeholder,
                Cursor      = Cursors.IBeam
            };

            _txtSearch.GotFocus += (s, e) =>
            {
                if (_txtSearch.Text == placeholder) { _txtSearch.Text = ""; _txtSearch.ForeColor = TextDark; }
                searchWrap.Invalidate();
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = placeholder; _txtSearch.ForeColor = TextMuted; }
                searchWrap.Invalidate();
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                string q = _txtSearch.Text.Trim();
                if (q == placeholder || q.Length == 0)
                {
                    if (_currentCategoryId >= 0) LoadFoods(_currentCategoryId);
                }
                else
                {
                    LoadFoodsBySearch(q);
                }
            };
            // Escape — clear search
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    _txtSearch.Text = "";
                    if (_currentCategoryId >= 0) LoadFoods(_currentCategoryId);
                }
            };

            searchWrap.Controls.Add(lblIcon);
            searchWrap.Controls.Add(_txtSearch);
            searchWrap.Click     += (s, e) => _txtSearch.Focus();
            lblIcon.Click        += (s, e) => _txtSearch.Focus();
            foodsHdr.Controls.Add(searchWrap);

            foodsHdr.Resize += (s, e) =>
            {
                btnTogglePhoto.Location = new Point(foodsHdr.Width - 46, 9);
                btnToggleText.Location  = new Point(foodsHdr.Width - 88, 9);
                // center search box between "Menu" label and toggle buttons
                int leftEdge  = 90;
                int rightEdge = foodsHdr.Width - 100;
                int available = rightEdge - leftEdge;
                int w = Math.Min(SearchW, Math.Max(120, available));
                searchWrap.Location = new Point(leftEdge + (available - w) / 2, 9);
                searchWrap.Width    = w;
                _txtSearch.Width    = w - 38;
            };
            btnToggleText.Click += (s, e) =>
            {
                _photoMode = false;
                btnToggleText.BackColor = Gold; btnToggleText.ForeColor = Color.White;
                btnTogglePhoto.BackColor = Color.FromArgb(243, 244, 246); btnTogglePhoto.ForeColor = TextMuted;
                if (_currentCategoryId >= 0) LoadFoods(_currentCategoryId);
            };
            btnTogglePhoto.Click += (s, e) =>
            {
                _photoMode = true;
                btnTogglePhoto.BackColor = Gold; btnTogglePhoto.ForeColor = Color.White;
                btnToggleText.BackColor = Color.FromArgb(243, 244, 246); btnToggleText.ForeColor = TextMuted;
                if (_currentCategoryId >= 0) LoadFoods(_currentCategoryId);
            };

            foodsPanel.Controls.Add(foodsHdr);

            flpFoods = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                WrapContents = true, FlowDirection = FlowDirection.LeftToRight,
                BackColor = BgMain, Padding = new Padding(16, 46, 0, 16)
            };
            foodsPanel.Controls.Add(flpFoods);

            // Content panel wraps 3 columns (below header)
            Panel content = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };
            content.Controls.Add(foodsPanel);        // Fill, index 0
            content.Controls.Add(cartPanel);         // Right, index 1
            content.Controls.Add(panelCategories);   // Left, index 2 (LAST in content = topmost)

            // Form: Fill first, Top LAST
            this.Controls.Add(content);
            this.Controls.Add(header);

            this.Load += (s, e) =>
            {
                EnsureOrderColumns();
                LoadTableName();
                LoadPayments();
                LoadCategories();
                LoadExistingOrder();
                LoadPlaceInfo();
            };
        }

        private void LoadTableName()
        {
            if (orderId > 0)
            {
                try
                {
                    dbconnect db = new dbconnect();
                    string sql = @"SELECT pi.room_name, po.name AS zone
                                   FROM [order] o
                                   JOIN place_in pi ON pi.id = o.place_id
                                   JOIN place_out po ON po.id = pi.place_out_id
                                   WHERE o.id = @oid";
                    SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                    cmd.Parameters.AddWithValue("@oid", orderId);
                    db.OpenCon();
                    var dr = cmd.ExecuteReader();
                    if (dr.Read())
                        lblTableName.Text = $"{dr["zone"]}  —  {dr["room_name"]}";
                    dr.Close();
                    db.CloseCon();
                }
                catch { }
            }
            else if (placeId > 0)
            {
                try
                {
                    dbconnect db = new dbconnect();
                    string sql = "SELECT room_name FROM place_in WHERE id=@pid";
                    SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                    cmd.Parameters.AddWithValue("@pid", placeId);
                    db.OpenCon();
                    object result = cmd.ExecuteScalar();
                    db.CloseCon();
                    if (result != null) lblTableName.Text = "Stol: " + result.ToString();
                }
                catch { }
            }
        }

        private static void EnsureOrderColumns()
        {
            try
            {
                dbconnect db = new dbconnect(); db.OpenCon();
                new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='payment_id' AND is_nullable=0)
                        ALTER TABLE [order] ALTER COLUMN payment_id INT NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='discount_amount')
                        ALTER TABLE [order] ADD discount_amount DECIMAL(18,2) NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='discount_pct')
                        ALTER TABLE [order] ADD discount_pct DECIMAL(5,2) NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='customer_id')
                        ALTER TABLE [order] ADD customer_id INT NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='customer_name')
                        ALTER TABLE [order] ADD customer_name NVARCHAR(200) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='delivery_phone')
                        ALTER TABLE [order] ADD delivery_phone NVARCHAR(50) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='delivery_address')
                        ALTER TABLE [order] ADD delivery_address NVARCHAR(500) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='is_delivery')
                        ALTER TABLE [order] ADD is_delivery BIT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='order_note')
                        ALTER TABLE [order] ADD order_note NVARCHAR(1000) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='custom_svc_fee')
                        ALTER TABLE [order] ADD custom_svc_fee DECIMAL(18,2) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[order]') AND name='custom_svc_type')
                        ALTER TABLE [order] ADD custom_svc_type NVARCHAR(3) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='customer')
                        CREATE TABLE customer (id INT IDENTITY PRIMARY KEY, name NVARCHAR(200) NOT NULL, phone NVARCHAR(50) NULL, notes NVARCHAR(500) NULL, created_at DATETIME DEFAULT GETDATE());
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='order_cancellation_log')
                        CREATE TABLE order_cancellation_log (
                            id            INT IDENTITY(1,1) PRIMARY KEY,
                            order_id      INT NOT NULL,
                            food_id       INT NULL,
                            food_name     NVARCHAR(255) NULL,
                            food_category NVARCHAR(255) NULL,
                            cancelled_qty INT NOT NULL DEFAULT 1,
                            cancelled_by  NVARCHAR(255) NULL,
                            cancelled_at  DATETIME NOT NULL DEFAULT GETDATE()
                        );
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='order_debt')
                        CREATE TABLE order_debt (
                            id           INT IDENTITY(1,1) PRIMARY KEY,
                            order_id     INT NOT NULL,
                            debtor_name  NVARCHAR(200) NOT NULL,
                            debtor_phone NVARCHAR(50)  NULL,
                            debt_note    NVARCHAR(500) NULL,
                            amount       DECIMAL(18,2) NOT NULL DEFAULT 0,
                            created_at   DATETIME NOT NULL DEFAULT GETDATE(),
                            is_paid      BIT NOT NULL DEFAULT 0,
                            paid_at      DATETIME NULL
                        );
                    IF NOT EXISTS (SELECT 1 FROM payment WHERE name='Qarz')
                        INSERT INTO payment(name) VALUES('Qarz');
                    UPDATE food SET [count]=0 WHERE ISNULL([count],0)<0 AND (is_unlimited IS NULL OR is_unlimited=0);
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('order_food') AND name='note')
                        ALTER TABLE order_food ADD note NVARCHAR(500) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='order_payments')
                        CREATE TABLE order_payments (
                            id INT IDENTITY(1,1) PRIMARY KEY,
                            order_id INT NOT NULL,
                            payment_id INT NOT NULL,
                            amount DECIMAL(18,2) NOT NULL DEFAULT 0
                        );",
                    db.GetCon()).ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        private void LoadPayments()
        {
            try
            {
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    "SELECT id, name FROM payment ORDER BY ISNULL(sort_order, id), id", db.GetCon()))
                    da.Fill(dt);
                comboPayment.DataSource = dt;
                comboPayment.DisplayMember = "name";
                comboPayment.ValueMember = "id";
                // Auto-select first item (default payment)
                if (comboPayment.Items.Count > 0)
                    comboPayment.SelectedIndex = 0;
            }
            catch { }
        }

        private void LoadCategories()
        {
            panelCategories.SuspendLayout();

            // Remove old buttons (keep only the title label)
            var toRemove = panelCategories.Controls.OfType<Button>().ToList();
            foreach (var b in toRemove) panelCategories.Controls.Remove(b);

            try
            {
                dbconnect db = new dbconnect();
                SqlCommand cmd = new SqlCommand("SELECT id, name FROM food_category ORDER BY ISNULL(sort_order,9999), name", db.GetCon());
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();

                var categories = new System.Collections.Generic.List<(int id, string name)>();
                while (dr.Read())
                    categories.Add(((int)dr["id"], dr["name"].ToString()));
                dr.Close();
                db.CloseCon();

                // Use absolute Y positioning so order matches query order (top to bottom)
                int btnY = 38; // below the title label (24px + 14px gap)
                Button firstBtn = null;
                foreach (var (catId, catName) in categories)
                {
                    Button btn = new Button
                    {
                        Text = catName,
                        Location = new Point(0, btnY),
                        Width = panelCategories.Width,
                        Height = 44,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.Transparent,
                        ForeColor = TextMuted,
                        Font = new Font("Segoe UI", 10),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(16, 0, 0, 0),
                        Cursor = Cursors.Hand,
                        Tag = catId
                    };
                    btn.FlatAppearance.BorderSize = 0;
                    btn.MouseEnter += (s, e) => { if (btn.BackColor == Color.Transparent) btn.BackColor = Color.FromArgb(243, 244, 246); };
                    btn.MouseLeave += (s, e) => { if (btn.BackColor == Color.FromArgb(243, 244, 246)) btn.BackColor = Color.Transparent; };
                    btn.Click += (s, e) =>
                    {
                        foreach (Button b in panelCategories.Controls.OfType<Button>())
                        {
                            b.BackColor = Color.Transparent;
                            b.ForeColor = TextMuted;
                            b.Font = new Font("Segoe UI", 10);
                        }
                        btn.BackColor = GoldBg;
                        btn.ForeColor = Gold;
                        btn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                        LoadFoods(catId);
                    };
                    panelCategories.Controls.Add(btn);
                    if (firstBtn == null) firstBtn = btn;
                    btnY += 44;
                }

                if (firstBtn != null)
                {
                    firstBtn.BackColor = GoldBg;
                    firstBtn.ForeColor = Gold;
                    firstBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                    LoadFoods((int)firstBtn.Tag);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kategoriya yuklashda xatolik: " + ex.Message);
            }

            panelCategories.ResumeLayout();
        }

        private void LoadFoods(int categoryId)
        {
            _currentCategoryId = categoryId;
            flpFoods.SuspendLayout();
            flpFoods.Controls.Clear();

            try
            {
                dbconnect db = new dbconnect();
                string sql = "SELECT id, name, selling_price, ISNULL([count],0) AS food_count, ISNULL(is_unlimited,0) AS is_unlimited, ISNULL(photo,'') AS photo FROM food WHERE food_category_id=@cid ORDER BY ISNULL(sort_order,9999), name";
                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                cmd.Parameters.AddWithValue("@cid", categoryId);
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    int foodId = (int)dr["id"];
                    string name = dr["name"].ToString();
                    decimal price = (decimal)dr["selling_price"];
                    int foodCount = Convert.ToInt32(dr["food_count"]);
                    bool isUnlimited = Convert.ToBoolean(dr["is_unlimited"]);
                    flpFoods.Controls.Add(CreateFoodCard(foodId, name, price, foodCount, isUnlimited, dr["photo"].ToString()));
                }

                dr.Close();
                db.CloseCon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Taomlar yuklashda xatolik: " + ex.Message);
            }

            flpFoods.ResumeLayout();
        }

        private void LoadFoodsBySearch(string query)
        {
            flpFoods.SuspendLayout();
            flpFoods.Controls.Clear();
            try
            {
                string sql = @"SELECT id, name, selling_price,
                               ISNULL([count],0) AS food_count,
                               ISNULL(is_unlimited,0) AS is_unlimited,
                               ISNULL(photo,'') AS photo
                               FROM food
                               WHERE name LIKE @q
                               ORDER BY ISNULL(sort_order,9999), name";
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand(sql, db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + query + "%");
                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            flpFoods.Controls.Add(CreateFoodCard(
                                (int)dr["id"],
                                dr["name"].ToString(),
                                (decimal)dr["selling_price"],
                                Convert.ToInt32(dr["food_count"]),
                                Convert.ToBoolean(dr["is_unlimited"]),
                                dr["photo"].ToString()));
                        }
                    }
                }

                if (flpFoods.Controls.Count == 0)
                    flpFoods.Controls.Add(new Label
                    {
                        Text = $"'{query}' bo'yicha taom topilmadi",
                        Font = new Font("Segoe UI", 12), ForeColor = TextMuted,
                        AutoSize = true, Margin = new Padding(20)
                    });
            }
            catch (Exception ex) { MessageBox.Show("Qidiruvda xatolik: " + ex.Message); }
            finally { flpFoods.ResumeLayout(); }
        }

        private Panel CreateFoodCard(int foodId, string name, decimal price, int count = 0, bool isUnlimited = true, string photoPath = "")
        {
            bool outOfStock = !isUnlimited && count <= 0;
            Color normalBg = outOfStock ? Color.FromArgb(245, 245, 247) : CardBg;
            Color hoverBg = Color.FromArgb(255, 248, 235);
            Color accentColor = outOfStock ? Color.FromArgb(200, 200, 210) : Gold;

            if (_photoMode)
            {
                // ── PHOTO MODE ───────────────────────────────────────────────
                string photoDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photo");
                string fullPhotoPath = "";
                if (!string.IsNullOrEmpty(photoPath))
                {
                    string candidate = Path.IsPathRooted(photoPath) ? photoPath : Path.Combine(photoDir, photoPath);
                    if (File.Exists(candidate)) fullPhotoPath = candidate;
                }
                string initial = name.Length > 0 ? name.Substring(0, 1).ToUpper() : "?";

                Panel card = new Panel
                {
                    Width = 170, Height = 218,
                    BackColor = normalBg, Margin = new Padding(0, 0, 14, 14),
                    Cursor = outOfStock ? Cursors.Default : Cursors.Hand
                };
                card.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = RoundRect(card.ClientRectangle, 12))
                    {
                        e.Graphics.FillPath(new SolidBrush(card.BackColor), p);
                        e.Graphics.DrawPath(new Pen(Border, 1.5f), p);
                    }
                };

                // Image / letter placeholder — drawn via Paint, no PictureBox child
                Image loadedImg = null;
                if (!string.IsNullOrEmpty(fullPhotoPath))
                    try { loadedImg = Image.FromFile(fullPhotoPath); } catch { }

                Panel imgArea = new Panel
                {
                    Width = 170, Height = 138, Location = new Point(0, 0),
                    BackColor = Color.FromArgb(237, 238, 240)
                };
                imgArea.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(237, 238, 240)), 0, 0, imgArea.Width, imgArea.Height);
                    if (loadedImg != null)
                    {
                        float scale = Math.Min((float)imgArea.Width / loadedImg.Width, (float)imgArea.Height / loadedImg.Height);
                        int iw = (int)(loadedImg.Width * scale);
                        int ih = (int)(loadedImg.Height * scale);
                        e.Graphics.DrawImage(loadedImg, (imgArea.Width - iw) / 2, (imgArea.Height - ih) / 2, iw, ih);
                    }
                    else
                    {
                        using (var f = new Font("Segoe UI", 48, FontStyle.Bold))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            e.Graphics.DrawString(initial, f, new SolidBrush(Gold), new RectangleF(0, 0, imgArea.Width, imgArea.Height), sf);
                    }
                };

                // Stock badge as a child Label — always renders on top of the Paint output
                if (!isUnlimited)
                {
                    Label lblStock = new Label
                    {
                        Text = count > 0 ? $"{count} ta" : "Tugagan",
                        Font = new Font("Segoe UI", 7, FontStyle.Bold),
                        ForeColor = count > 0 ? Color.FromArgb(55, 65, 81) : Color.White,
                        BackColor = count > 0 ? Color.FromArgb(220, 220, 225) : Danger,
                        AutoSize = false, Width = 54, Height = 20,
                        Location = new Point(108, 8), TextAlign = ContentAlignment.MiddleCenter
                    };
                    imgArea.Controls.Add(lblStock);
                }

                card.Controls.Add(imgArea);

                // Food name
                Label lblName = new Label
                {
                    Text = name, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = outOfStock ? TextMuted : TextDark, BackColor = normalBg,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Location = new Point(10, 144), Width = 148, Height = 28, AutoSize = false
                };
                card.Controls.Add(lblName);

                // Price in gold text
                Label lblPrice = new Label
                {
                    Text = price.ToString("N0") + " so'm",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = outOfStock ? TextMuted : Gold, BackColor = normalBg,
                    AutoSize = false, Width = 116, Height = 20,
                    Location = new Point(10, 176), TextAlign = ContentAlignment.MiddleLeft
                };
                card.Controls.Add(lblPrice);

                // + button bottom-right
                Panel btnAdd = new Panel
                {
                    Width = 34, Height = 34, Location = new Point(128, 176),
                    BackColor = Color.Transparent, Cursor = Cursors.Hand
                };
                btnAdd.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(new SolidBrush(accentColor), 1, 1, 31, 31);
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var f = new Font("Segoe UI", 16, FontStyle.Bold))
                        e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, 34, 34), sf);
                };
                card.Controls.Add(btnAdd);

                if (!outOfStock)
                {
                    EventHandler onClick = (s, e) => AddToCart(foodId, name, price, count, isUnlimited);
                    card.Click += onClick; btnAdd.Click += onClick;
                    imgArea.Click += onClick; lblName.Click += onClick; lblPrice.Click += onClick;

                    Action<bool> setHover = (on) =>
                    {
                        Color bg = on ? hoverBg : normalBg;
                        card.BackColor = bg; lblName.BackColor = bg; lblPrice.BackColor = bg;
                        card.Invalidate();
                    };
                    foreach (Control c in new Control[] { card, imgArea, lblName, lblPrice })
                    {
                        c.MouseEnter += (s, e) => setHover(true);
                        c.MouseLeave += (s, e) => setHover(false);
                    }
                }
                else { btnAdd.Visible = false; }

                return card;
            }
            else
            {
                // ── TEXT MODE ────────────────────────────────────────────────
                Panel card = new Panel
                {
                    Width = 200, Height = 106,
                    BackColor = normalBg, Margin = new Padding(0, 0, 14, 14),
                    Cursor = outOfStock ? Cursors.Default : Cursors.Hand
                };
                card.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = RoundRect(card.ClientRectangle, 12))
                    {
                        e.Graphics.FillPath(new SolidBrush(card.BackColor), p);
                        e.Graphics.DrawPath(new Pen(Border, 1.5f), p);
                    }
                    // Gold accent bar at top
                    e.Graphics.FillRectangle(new SolidBrush(accentColor), 0, 0, card.Width, 4);
                };

                // Name — added FIRST so price badge renders on top
                Label lblName = new Label
                {
                    Text = name, Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = outOfStock ? TextMuted : TextDark, BackColor = normalBg,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(6, 12), Width = 188, Height = 46, AutoSize = false
                };
                card.Controls.Add(lblName);

                // Price badge — added SECOND so it renders over name background
                Label lblPrice = new Label
                {
                    Text = price.ToString("N0") + " so'm",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Color.White, BackColor = accentColor,
                    AutoSize = false, Width = 86, Height = 22,
                    Location = new Point(108, 8), TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(lblPrice);

                // Stock badge bottom-left (if limited)
                if (!isUnlimited)
                {
                    Label lblStock = new Label
                    {
                        Text = count > 0 ? $"Qoldi: {count}" : "Tugagan",
                        Font = new Font("Segoe UI", 7, FontStyle.Bold),
                        ForeColor = count > 5 ? Success : (count > 0 ? Gold : Danger),
                        BackColor = count > 5 ? Color.FromArgb(220, 252, 231)
                                  : count > 0 ? Color.FromArgb(255, 248, 230)
                                              : Color.FromArgb(254, 226, 226),
                        AutoSize = false, Width = 78, Height = 18,
                        Location = new Point(8, 80), TextAlign = ContentAlignment.MiddleCenter
                    };
                    card.Controls.Add(lblStock);
                }

                // + button bottom-right
                Panel btnAdd = new Panel
                {
                    Width = 36, Height = 36, Location = new Point(157, 64),
                    BackColor = Color.Transparent, Cursor = Cursors.Hand
                };
                btnAdd.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.FillEllipse(new SolidBrush(accentColor), 1, 1, 33, 33);
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var f = new Font("Segoe UI", 17, FontStyle.Bold))
                        e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, 36, 36), sf);
                };
                card.Controls.Add(btnAdd);

                if (!outOfStock)
                {
                    EventHandler onClick = (s, e) => AddToCart(foodId, name, price, count, isUnlimited);
                    card.Click += onClick; btnAdd.Click += onClick;
                    lblName.Click += onClick; lblPrice.Click += onClick;

                    Action<bool> setHover = (on) =>
                    {
                        Color bg = on ? hoverBg : normalBg;
                        card.BackColor = bg; lblName.BackColor = bg;
                        card.Invalidate();
                    };
                    card.MouseEnter += (s, e) => setHover(true);
                    card.MouseLeave += (s, e) => setHover(false);
                    lblName.MouseEnter += (s, e) => setHover(true);
                    lblName.MouseLeave += (s, e) => setHover(false);
                    lblPrice.MouseEnter += (s, e) => setHover(true);
                    lblPrice.MouseLeave += (s, e) => setHover(false);
                }
                else { btnAdd.Visible = false; }

                return card;
            }
        }

        private void AddToCart(int foodId, string name, decimal price, int foodCount = 0, bool isUnlimited = true)
        {
            foreach (Control ctrl in flpCart.Controls)
            {
                if (ctrl.Tag is CartItem ci && ci.FoodId == foodId)
                {
                    // Refresh the max from the card data
                    if (!isUnlimited) ci.MaxCount = foodCount;
                    ci.IsUnlimited = isUnlimited;

                    if (!ci.IsUnlimited && ci.Quantity >= ci.MaxCount)
                    {
                        MessageBox.Show($"'{name}': omborda faqat {ci.MaxCount} ta bor!",
                            "Yetarli emas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    ci.Quantity++;
                    UpdateCartRow(ctrl as Panel);
                    RecalcTotal();
                    return;
                }
            }

            if (!isUnlimited && foodCount <= 0)
            {
                MessageBox.Show($"'{name}' omborda tugagan!", "Yetarli emas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CartItem item = new CartItem
            {
                FoodId = foodId, FoodName = name, Price = price, Quantity = 1,
                MaxCount = isUnlimited ? int.MaxValue : foodCount,
                IsUnlimited = isUnlimited
            };
            Panel row = CreateCartRow(item, name, _canEditItems);
            row.Width = Math.Max(100, flpCart.ClientSize.Width - 2);
            flpCart.Controls.Add(row);
            flpCart.ScrollControlIntoView(row);
            RecalcTotal();
        }

        private Panel CreateCartRow(CartItem item, string name, bool canEdit = true)
        {
            int w = Math.Max(200, flpCart.ClientSize.Width - 4);
            Panel row = new Panel
            {
                Width = w, Height = 72,
                BackColor = CardBg, Margin = new Padding(0, 0, 0, 8), Tag = item
            };

            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(row.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(row.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border, 1f), path);
                }
            };

            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextDark, BackColor = CardBg,
                Location = new Point(14, 8),
                Height = 20, AutoSize = false
            };
            row.Controls.Add(lblName);

            Label lblNote = new Label
            {
                Text = string.IsNullOrEmpty(item.Note) ? "" : "✎ " + item.Note,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                ForeColor = Color.FromArgb(124, 58, 237), BackColor = CardBg,
                Location = new Point(14, 28), Height = 16, AutoSize = false,
                Name = "lblNote", Cursor = Cursors.Hand
            };
            row.Controls.Add(lblNote);

            Label lblPrice = new Label
            {
                Text = (item.Quantity * item.Price).ToString("N0") + " so'm",
                Font = new Font("Segoe UI", 9), ForeColor = Gold, BackColor = CardBg,
                Location = new Point(14, 46), Height = 18, AutoSize = false,
                Name = "lblRowPrice"
            };
            row.Controls.Add(lblPrice);

            // Double-click on name or note = izoh dialog
            Action openNoteDialog = () =>
            {
                Form dlg = new Form
                {
                    FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterParent,
                    Size = new Size(340, 180), BackColor = Color.White
                };
                dlg.Controls.Add(new Panel { Height = 4, Dock = DockStyle.Top, BackColor = Color.FromArgb(124, 58, 237) });
                dlg.Controls.Add(new Label { Text = "Izoh — " + item.FoodName, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark, AutoSize = true, Location = new Point(12, 14) });
                var txt = new TextBox { Text = item.Note, Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(12, 42), Width = 312, Height = 80, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(249, 249, 251) };
                dlg.Controls.Add(txt);
                Button btnOk = new Button { Text = "Saqlash", Location = new Point(12, 132), Width = 150, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(124, 58, 237), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Click += (bs, be) => { item.Note = txt.Text.Trim(); dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                Button btnClear = new Button { Text = "O'chirish", Location = new Point(174, 132), Width = 150, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextDark, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand };
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.Click += (bs, be) => { item.Note = ""; dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                dlg.Controls.Add(btnOk); dlg.Controls.Add(btnClear);
                dlg.KeyPreview = true; dlg.KeyDown += (ks, ke) => { if (ke.KeyCode == Keys.Escape) dlg.Close(); };
                if (dlg.ShowDialog(row.FindForm()) == DialogResult.OK)
                {
                    lblNote.Text = string.IsNullOrEmpty(item.Note) ? "" : "✎ " + item.Note;
                    row.BackColor = string.IsNullOrEmpty(item.Note) ? CardBg : Color.FromArgb(245, 243, 255);
                }
            };
            row.DoubleClick     += (s, e) => openNoteDialog();
            lblName.DoubleClick += (s, e) => openNoteDialog();
            lblNote.Click       += (s, e) => openNoteDialog();
            lblPrice.DoubleClick+= (s, e) => openNoteDialog();

            // Qty controls — spacious
            Button btnMinus = MakeQtyBtn("−");
            Label lblQty = new Label
            {
                Text = item.Quantity.ToString(),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, BackColor = CardBg,
                Width = 32, Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Name = "lblQty"
            };
            Button btnPlus = MakeQtyBtn("+");

            btnMinus.Click += (s, e) =>
            {
                if (item.Quantity > 1) { item.Quantity--; UpdateCartRow(row); RecalcTotal(); }
            };
            btnPlus.Click += (s, e) =>
            {
                if (!item.IsUnlimited && item.Quantity >= item.MaxCount)
                {
                    MessageBox.Show($"'{item.FoodName}': omborda faqat {item.MaxCount} ta bor!",
                        "Yetarli emas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                item.Quantity++;
                UpdateCartRow(row);
                RecalcTotal();
            };

            btnMinus.Visible = canEdit;
            row.Controls.Add(btnMinus);
            row.Controls.Add(lblQty);
            row.Controls.Add(btnPlus);

            // Delete X
            Panel btnDel = new Panel
            {
                Width = 26, Height = 26,
                BackColor = Color.Transparent, Cursor = Cursors.Hand,
                Visible = canEdit
            };
            btnDel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(200, 60, 60), 2f))
                {
                    e.Graphics.DrawLine(pen, 6, 6, 20, 20);
                    e.Graphics.DrawLine(pen, 20, 6, 6, 20);
                }
            };
            btnDel.Click += (s, e) => { flpCart.Controls.Remove(row); RecalcTotal(); };
            row.Controls.Add(btnDel);

            // right-side layout: [−][qty][+] on bottom row, [X] on top-right
            // When canEdit=false: minus and del are hidden — only [qty][+] shown
            Action positionRight = () =>
            {
                int rx = row.Width - 14;
                if (canEdit)
                {
                    btnDel.Location   = new Point(rx - 26, 8);
                    btnPlus.Location  = new Point(rx - 32, 36);
                    lblQty.Location   = new Point(rx - 64, 36);
                    btnMinus.Location = new Point(rx - 96, 36);
                    lblName.Width     = rx - 96 - 16;
                }
                else
                {
                    btnPlus.Location = new Point(rx - 32, 36);
                    lblQty.Location  = new Point(rx - 64, 36);
                    lblName.Width    = rx - 64 - 16;
                }
                lblPrice.Width = lblName.Width;
            };
            row.Resize += (s, e) => positionRight();
            positionRight();

            return row;
        }

        private Button MakeQtyBtn(string text)
        {
            Button b = new Button
            {
                Text = text, Width = 32, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            return b;
        }

        private Button MakeActionBtn(string icon, string label)
        {
            var b = new Button
            {
                Text = icon + "\n" + label,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = TextDark,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Border;
            b.MouseEnter += (s, e) => { if (b.BackColor == Color.FromArgb(243, 244, 246)) b.BackColor = Color.FromArgb(229, 231, 235); };
            b.MouseLeave += (s, e) => { if (b.BackColor == Color.FromArgb(229, 231, 235)) b.BackColor = Color.FromArgb(243, 244, 246); };
            return b;
        }

        private void ShowCustomerPicker()
        {
            var customers = new List<(int id, string name, string phone)>();
            try
            {
                var db = new dbconnect(); db.OpenCon();
                using (var dr = new SqlCommand(
                    "SELECT id, name, ISNULL(phone,'') AS phone FROM customer ORDER BY name", db.GetCon()).ExecuteReader())
                    while (dr.Read())
                        customers.Add(((int)dr["id"], dr["name"].ToString(), dr["phone"].ToString()));
                db.CloseCon();
            }
            catch { }

            Form dlg = new Form
            {
                Text = "Mijozni tanlang", Width = 360, Height = 440,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };
            var txtSearch = new TextBox
            {
                Location = new Point(12, 12), Width = 316, Height = 30,
                Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle
            };
            var lbl = new Label { Text = "Qidirish:", Location = new Point(12, -2), AutoSize = true, Font = new Font("Segoe UI", 7.5f), ForeColor = TextMuted };
            var lst = new ListBox
            {
                Location = new Point(12, 50), Width = 316, Height = 300,
                Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            void RefreshList(string f)
            {
                lst.Items.Clear();
                lst.Items.Add("(Mijoz tanlanmagan)");
                foreach (var c in customers)
                    if (string.IsNullOrEmpty(f) || c.name.ToLower().Contains(f.ToLower()) || c.phone.Contains(f))
                        lst.Items.Add(c.name + (string.IsNullOrEmpty(c.phone) ? "" : "  •  " + c.phone));
            }
            RefreshList("");
            txtSearch.TextChanged += (s, e) => RefreshList(txtSearch.Text);

            var btnOk = new Button
            {
                Text = "Tanlash", Location = new Point(12, 360), Width = 316, Height = 40,
                FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (lst.SelectedIndex == 0 || lst.SelectedIndex < 0)
                {
                    _customerId = 0; _customerName = "";
                    _btnMijoz.Text = "◎\nMijoz";
                    _btnMijoz.BackColor = Color.FromArgb(243, 244, 246);
                }
                else
                {
                    int ri = lst.SelectedIndex - 1;
                    _customerId = customers[ri].id; _customerName = customers[ri].name;
                    _btnMijoz.Text = "◎\n" + _customerName;
                    _btnMijoz.BackColor = GoldBg;
                }
                dlg.DialogResult = DialogResult.OK;
            };
            lst.DoubleClick += (s, e) => btnOk.PerformClick();
            dlg.Controls.AddRange(new Control[] { lbl, txtSearch, lst, btnOk });
            dlg.ShowDialog(this);
        }

        private void ShowPaymentPicker()
        {
            if (comboPayment.Items.Count == 0) return;

            decimal grandTotal = GetGrandTotal();

            var payList = new List<(int id, string name)>();
            foreach (DataRowView drv in comboPayment.Items)
                payList.Add((Convert.ToInt32(drv["id"]), drv["name"].ToString()));
            if (payList.Count == 0) return;

            // Working copy of current entries
            var entries = new List<(int id, string name, decimal amount)>(_paymentEntries);

            // ── Style constants ────────────────────────────────────────────
            Color bgPage  = Color.White;
            Color bgRow   = Color.FromArgb(245, 247, 250);
            Color clGreen = Color.FromArgb(22, 163, 74);
            Color clRed   = Color.FromArgb(220, 38, 38);
            Color clLine  = Color.FromArgb(229, 231, 235);

            Form dlg = new Form
            {
                Text = "To'lov", Width = 420, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = bgPage
            };

            // ── Header ────────────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = bgPage };
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(clLine), 0, header.Height - 1, header.Width, header.Height - 1);
            var lblSum = new Label
            {
                Text = $"Jami summa: {grandTotal:N0} UZS",
                Font = new Font("Segoe UI", 13, FontStyle.Bold), ForeColor = TextDark,
                Location = new Point(16, 12), AutoSize = true
            };
            header.Controls.Add(lblSum);
            dlg.Controls.Add(header);

            // ── Rows container (grows dynamically) ────────────────────────
            Panel rowsWrap = new Panel { BackColor = bgPage, Height = 0 };
            dlg.Controls.Add(rowsWrap);

            // ── Add-entry section ─────────────────────────────────────────
            int addH = 148;
            Panel addSection = new Panel { BackColor = bgPage, Height = addH };
            addSection.Paint += (s, e) => e.Graphics.DrawLine(new Pen(clLine), 0, 0, addSection.Width, 0);

            int pw = 374;

            var cmbType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10), BackColor = bgRow,
                Location = new Point(14, 12), Width = pw, Height = 32
            };
            foreach (var p in payList) cmbType.Items.Add(p.name);
            cmbType.SelectedIndex = 0;
            addSection.Controls.Add(cmbType);

            var txtAmt = new TextBox
            {
                Font = new Font("Segoe UI", 12), Text = "0",
                Location = new Point(14, 52), Width = pw - 110, Height = 32,
                BorderStyle = BorderStyle.FixedSingle
            };
            addSection.Controls.Add(txtAmt);

            var btnAdd = new Button
            {
                Text = "Qo'shish", Location = new Point(pw - 90, 50), Width = 104, Height = 36,
                FlatStyle = FlatStyle.Flat, BackColor = clGreen, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            addSection.Controls.Add(btnAdd);

            dlg.Controls.Add(addSection);

            // ── Save button (always at bottom) ────────────────────────────
            var btnOk = new Button
            {
                Text = "Saqlash", Dock = DockStyle.Bottom, Height = 48,
                FlatStyle = FlatStyle.Flat, BackColor = clGreen, ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnOk);

            // ── Remaining calculator ─────────────────────────────────────
            Func<decimal> calcRem = () => grandTotal - entries.Sum(en => en.amount);

            Action updateRem = null;
            Action refreshLayout = null;

            updateRem = () =>
            {
                decimal rem = calcRem();
                txtAmt.Text = rem > 0 ? ((long)rem).ToString() : "0";
                bool canAdd = rem > 0;
                btnAdd.Enabled    = canAdd;
                btnAdd.BackColor  = canAdd ? clGreen : Color.FromArgb(156, 163, 175);
            };

            // ── Layout recalculator ───────────────────────────────────────
            refreshLayout = () =>
            {
                rowsWrap.Controls.Clear();
                int ry = 0;
                foreach (var en in entries.ToList())
                {
                    var cap = en;
                    Panel row = new Panel { Location = new Point(0, ry), Width = rowsWrap.Width, Height = 48, BackColor = bgRow };
                    row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(clLine), 0, row.Height - 1, row.Width, row.Height - 1);
                    row.Controls.Add(new Label { Text = cap.name, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark, Location = new Point(14, 13), AutoSize = true });
                    row.Controls.Add(new Label { Text = $"{cap.amount:N0} UZS", Font = new Font("Segoe UI", 10), ForeColor = TextDark, TextAlign = ContentAlignment.MiddleRight, Size = new Size(160, 22), Location = new Point(180, 13) });
                    var btnDel = new Button { Text = "✕", Location = new Point(rowsWrap.Width - 44, 10), Width = 30, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = clRed, Font = new Font("Segoe UI", 11), Cursor = Cursors.Hand };
                    btnDel.FlatAppearance.BorderSize = 0;
                    btnDel.Click += (s, e) => { entries.Remove(cap); refreshLayout(); updateRem(); };
                    row.Controls.Add(btnDel);
                    rowsWrap.Controls.Add(row);
                    ry += 49;
                }
                rowsWrap.Height = ry;
                int topY = header.Height;
                rowsWrap.Location   = new Point(0, topY);
                addSection.Location = new Point(0, topY + rowsWrap.Height);
                dlg.ClientSize = new Size(dlg.ClientSize.Width, topY + rowsWrap.Height + addH + btnOk.Height);
            };

            dlg.Load += (s, e) =>
            {
                rowsWrap.Width   = dlg.ClientSize.Width;
                addSection.Width = dlg.ClientSize.Width;
                refreshLayout();
                updateRem();
            };

            // ── Add button click ──────────────────────────────────────────
            btnAdd.Click += (s, e) =>
            {
                if (!decimal.TryParse(txtAmt.Text.Replace(" ", ""), out decimal amt) || amt <= 0)
                {
                    MessageBox.Show("Summa 0 dan katta bo'lishi kerak!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                decimal rem = calcRem();
                if (amt > rem) amt = rem; // jami summadan ortiq qabul qilinmaydi
                int idx = cmbType.SelectedIndex >= 0 ? cmbType.SelectedIndex : 0;
                entries.Add((payList[idx].id, payList[idx].name, Math.Round(amt, 0)));
                refreshLayout();
                updateRem();
            };

            // ── Save button click ─────────────────────────────────────────
            btnOk.Click += (s, e) =>
            {
                if (entries.Count == 0) { MessageBox.Show("Kamida 1 ta to'lov usuli qo'shing!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                decimal rem = calcRem();
                if (rem != 0) { MessageBox.Show($"To'lovlar buyurtmaning summasi bilan teng emas.\nQoldiq: {rem:N0} UZS", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                _paymentEntries = new List<(int id, string name, decimal amount)>(entries);

                // Sync comboPayment to first entry
                foreach (DataRowView drv in comboPayment.Items)
                    if (Convert.ToInt32(drv["id"]) == _paymentEntries[0].id) { comboPayment.SelectedItem = drv; break; }

                _btnPayment.Text = _paymentEntries.Count == 1
                    ? "◈\n" + _paymentEntries[0].name
                    : $"◈\n{_paymentEntries[0].name}+{_paymentEntries.Count - 1}";
                _btnPayment.BackColor = GoldBg;
                dlg.DialogResult = DialogResult.OK;
            };

            dlg.ShowDialog(this);
        }

        private void ShowDeliveryDialog()
        {
            Form dlg = new Form
            {
                Text = "Yetkazib berish", Width = 360, Height = 268,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };
            var chk   = new CheckBox { Text = "Yetkazib berish", Checked = _isDelivery, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark, Location = new Point(14, 14), AutoSize = true };
            dlg.Controls.Add(new Label { Text = "Telefon raqami:", Location = new Point(14, 46),  AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            var txtPh  = new TextBox { Text = _deliveryPhone, Location = new Point(14, 63),  Width = 308, Height = 30, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            dlg.Controls.Add(new Label { Text = "Manzil:",       Location = new Point(14, 102), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            var txtAdr = new TextBox { Text = _deliveryAddr,  Location = new Point(14, 119), Width = 308, Height = 64, Font = new Font("Segoe UI", 10), Multiline = true, BorderStyle = BorderStyle.FixedSingle };
            var btnOk  = new Button  { Text = "Saqlash", Location = new Point(14, 196), Width = 308, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                _isDelivery    = chk.Checked;
                _deliveryPhone = txtPh.Text.Trim();
                _deliveryAddr  = txtAdr.Text.Trim();
                if (_isDelivery)
                {
                    _btnDelivery.Text = "⊙\n" + (_deliveryPhone.Length > 0 ? _deliveryPhone : "Manzil");
                    _btnDelivery.BackColor = Color.FromArgb(219, 234, 254);
                }
                else
                {
                    _btnDelivery.Text = "⊙\nYetkazib berish";
                    _btnDelivery.BackColor = Color.FromArgb(243, 244, 246);
                }
                dlg.DialogResult = DialogResult.OK;
            };
            dlg.Controls.AddRange(new Control[] { chk, txtPh, txtAdr, btnOk });
            dlg.ShowDialog(this);
        }

        private void ShowBoshqalarMenu(Button anchor)
        {
            var cms = new ContextMenuStrip { Font = new Font("Segoe UI", 10) };
            cms.Items.Add("Buyurtma eslatmasi",  null, (s, e) => ShowOrderNoteDialog());
            cms.Items.Add("Xizmat haqi",         null, (s, e) => ShowServiceFeeDialog());
            cms.Items.Add("Chegirma qo'shish",   null, (s, e) => ShowDiscountDialog());
            string debtLabel = string.IsNullOrEmpty(_debtName) ? "Qarz" : "Qarz: " + _debtName;
            cms.Items.Add(debtLabel, null, (s, e) => ShowDebtDialog());
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("Chek chiqarish", null, (s, e) =>
            {
                if (orderId > 0) try { PrintService.PrintReceipt(orderId); }
                    catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                else MessageBox.Show("Avval zakazni saqlang.", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
            cms.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowOrderNoteDialog()
        {
            Form dlg = new Form
            {
                Text = "Buyurtma eslatmasi", Width = 360, Height = 230,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };
            var txt = new TextBox { Text = _orderNote, Multiline = true, ScrollBars = ScrollBars.Vertical, Location = new Point(12, 12), Width = 316, Height = 130, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            var btnOk = new Button { Text = "Saqlash", Location = new Point(12, 152), Width = 316, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                _orderNote = txt.Text.Trim();
                _btnBoshqalar.BackColor = _orderNote.Length > 0 ? GoldBg : Color.FromArgb(243, 244, 246);
                dlg.DialogResult = DialogResult.OK;
            };
            dlg.Controls.Add(txt); dlg.Controls.Add(btnOk);
            dlg.ShowDialog(this);
        }

        private void ShowServiceFeeDialog()
        {
            decimal food = GetFoodTotal();

            // Saqlangan qiymat UZS mi yoki foiz mi?
            bool savedAsUzs = (_customSvcType == "UZS" || _customSvcType == "pct");

            // Boshlang'ich radio holati: UZS saqlangan bo'lsa → So'm, aks holda Foiz
            bool startPct = !savedAsUzs;

            var rbPct = new RadioButton { Text = "Foiz (%)",   Location = new Point(14, 14),  Checked = startPct,  Font = new Font("Segoe UI", 10), AutoSize = true };
            var rbSum = new RadioButton { Text = "So'm (UZS)", Location = new Point(130, 14), Checked = !startPct, Font = new Font("Segoe UI", 10), AutoSize = true };

            // Boshlang'ich qiymat: hozirgi rejimga to'g'ri keluvchi format
            string InitialText()
            {
                if (_customSvcFee <= 0) return "";
                if (savedAsUzs)
                    // DB da UZS → So'm rejimida: shu qiymat; Foiz rejimida: %ga aylantir
                    return startPct && food > 0
                        ? Math.Round(_customSvcFee / food * 100, 2)
                              .ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
                        : _customSvcFee.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
                else
                    // DB da foiz → Foiz rejimida: shu qiymat; So'm rejimida: UZS ga aylantir
                    return !startPct && food > 0
                        ? Math.Round(food * _customSvcFee / 100, 0)
                              .ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
                        : _customSvcFee.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            }

            Form dlg = new Form
            {
                Text = "Xizmat haqi", Width = 320, Height = 220,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };

            dlg.Controls.Add(new Label { Text = "Miqdor:", Location = new Point(14, 46), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });

            var txtAmt = new TextBox
            {
                Text = InitialText(),
                Location = new Point(14, 63), Width = 272, Height = 30,
                Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle
            };

            dlg.Controls.Add(new Label { Text = "Bo'sh qoldirsa, sozlamalardagi standart xizmat haqi ishlatiladi.", Location = new Point(14, 100), Width = 272, Height = 32, Font = new Font("Segoe UI", 8), ForeColor = TextMuted, AutoSize = false });

            // Rejim almashganda qiymatni avtomatik konvertatsiya qilish
            // (CheckedChanged handlerlar Checked=true bo'lganda ishga tushadi)
            rbPct.CheckedChanged += (s, e) =>
            {
                if (!rbPct.Checked) return; // Unchecked eventini o'tkazib yuboramiz
                // So'm → Foiz: kiritilgan UZS ni foizga aylantir
                string raw = txtAmt.Text.Trim().Replace(',', '.');
                if (food > 0 && decimal.TryParse(raw,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal uzs) && uzs > 0)
                    txtAmt.Text = Math.Round(uzs / food * 100, 2)
                        .ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            };
            rbSum.CheckedChanged += (s, e) =>
            {
                if (!rbSum.Checked) return; // Unchecked eventini o'tkazib yuboramiz
                // Foiz → So'm: kiritilgan foizni UZS ga aylantir
                string raw = txtAmt.Text.Trim().Replace(',', '.');
                if (food > 0 && decimal.TryParse(raw,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal pct) && pct > 0)
                    txtAmt.Text = Math.Round(food * pct / 100, 0)
                        .ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
            };

            var btnOk = new Button { Text = "Qo'llash", Location = new Point(14, 138), Width = 272, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                string raw = txtAmt.Text.Trim().Replace(',', '.');
                decimal entered;
                decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out entered);
                if (rbPct.Checked && entered > 100)
                {
                    MessageBox.Show(
                        $"Foiz qiymati 100 dan oshib ketdi ({entered:G29}%).\n\nIltimos, foiz uchun 0–100 oralig'idagi qiymat kiriting.\nMasalan: 10 (ya'ni 10%).\n\nKatta summa (UZS) kiritmoqchi bo'lsangiz, \"So'm (UZS)\" rejimini tanlang.",
                        "Noto'g'ri qiymat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _customSvcFee  = entered;
                _customSvcType = rbPct.Checked ? "%" : "UZS";
                RecalcTotal();
                dlg.DialogResult = DialogResult.OK;
            };

            dlg.Controls.AddRange(new Control[] { rbPct, rbSum, txtAmt, btnOk });
            dlg.ShowDialog(this);
        }

        private void ShowDiscountDialog()
        {
            Form dlg = new Form
            {
                Text = "Chegirma", Width = 300, Height = 180,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };
            dlg.Controls.Add(new Label { Text = "Chegirma (%):", Location = new Point(12, 14), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            var txt = new TextBox
            {
                Text = _discountPct > 0 ? _discountPct.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) : "0",
                Location = new Point(12, 34), Width = 254, Height = 30,
                Font = new Font("Segoe UI", 11), BorderStyle = BorderStyle.FixedSingle
            };
            var btnOk = new Button { Text = "Qo'llash", Location = new Point(12, 76), Width = 254, Height = 42, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                string raw = txt.Text.Replace(',', '.');
                decimal newPct;
                decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out newPct);
                newPct = Math.Max(0, Math.Min(100, newPct));

                // Chegirma o'zgarsa — eski to'lov summalari yangi jami bilan mos kelmaydi,
                // shuning uchun ularni tozalaymiz (foydalanuvchi qayta kiritadi).
                if (newPct != _discountPct)
                {
                    _discountPct = newPct;
                    if (_paymentEntries.Count > 0)
                    {
                        _paymentEntries.Clear();
                        _btnPayment.Text      = "◈\nTo'lov turi";
                        _btnPayment.BackColor = Color.FromArgb(243, 244, 246);
                    }
                }
                else
                {
                    _discountPct = newPct;
                }

                RecalcTotal();
                dlg.DialogResult = DialogResult.OK;
            };
            dlg.Controls.Add(txt); dlg.Controls.Add(btnOk);
            dlg.ShowDialog(this);
        }

        private void ShowDebtDialog()
        {
            Form dlg = new Form
            {
                Text = "Qarz", Width = 340, Height = 290,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(249, 249, 251)
            };
            int y = 12;
            dlg.Controls.Add(new Label { Text = "Ism Familiya *:", Location = new Point(12, y), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            y += 18;
            var txtName = new TextBox { Text = _debtName, Location = new Point(12, y), Width = 296, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            dlg.Controls.Add(txtName); y += 30;
            dlg.Controls.Add(new Label { Text = "Telefon raqami:", Location = new Point(12, y), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            y += 18;
            var txtPhone = new TextBox { Text = _debtPhone, Location = new Point(12, y), Width = 296, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            dlg.Controls.Add(txtPhone); y += 30;
            dlg.Controls.Add(new Label { Text = "Izoh:", Location = new Point(12, y), AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = TextMuted });
            y += 18;
            var txtNote = new TextBox { Text = _debtNote, Location = new Point(12, y), Width = 296, Height = 52, Multiline = true, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
            dlg.Controls.Add(txtNote); y += 60;
            var btnOk = new Button { Text = "Saqlash", Location = new Point(12, y), Width = 296, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Ism familiyani kiriting!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                _debtName  = txtName.Text.Trim();
                _debtPhone = txtPhone.Text.Trim();
                _debtNote  = txtNote.Text.Trim();
                _btnBoshqalar.BackColor = Color.FromArgb(254, 226, 226);
                // Auto-select "Qarz" payment type
                SelectDebtPayment();
                dlg.DialogResult = DialogResult.OK;
            };
            if (!string.IsNullOrEmpty(_debtName))
            {
                var btnClear = new Button { Text = "Qarzni olib tashlash", Location = new Point(12, y + 46), Width = 296, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(229, 231, 235), ForeColor = TextMuted, Font = new Font("Segoe UI", 9) };
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.Click += (s, e) => { _debtName = _debtPhone = _debtNote = ""; _btnBoshqalar.BackColor = Color.FromArgb(243, 244, 246); dlg.DialogResult = DialogResult.Cancel; };
                dlg.Controls.Add(btnClear);
                dlg.Height += 46;
            }
            dlg.Controls.Add(btnOk);
            dlg.ShowDialog(this);
        }

        private void SelectDebtPayment()
        {
            try
            {
                if (comboPayment == null || comboPayment.Items.Count == 0) return;
                for (int i = 0; i < comboPayment.Items.Count; i++)
                {
                    var row = comboPayment.Items[i] as System.Data.DataRowView;
                    if (row != null && row["name"].ToString().Equals("Qarz", StringComparison.OrdinalIgnoreCase))
                    {
                        comboPayment.SelectedIndex = i;
                        _btnPayment.Text = "◈\nQarz";
                        _btnPayment.BackColor = Color.FromArgb(254, 226, 226);
                        return;
                    }
                }
            }
            catch { }
        }

        private void UpdateCartRow(Panel row)
        {
            CartItem item = (CartItem)row.Tag;
            foreach (Control c in row.Controls)
            {
                if (c.Name == "lblQty")      c.Text = item.Quantity.ToString();
                if (c.Name == "lblRowPrice") c.Text = (item.Quantity * item.Price).ToString("N0") + " so'm";
                if (c.Name == "lblNote")     c.Text = string.IsNullOrEmpty(item.Note) ? "" : "✎ " + item.Note;
            }
        }

        private decimal GetFoodTotal()
        {
            decimal total = 0;
            foreach (Control c in flpCart.Controls)
                if (c.Tag is CartItem ci)
                    total += ci.Quantity * ci.Price;
            return total;
        }

        private decimal CalcPlaceAmount(decimal food)
        {
            if (_placePrice <= 0) return 0;
            return _placePriceType == "%"
                ? Math.Round(food * _placePrice / 100, 0)
                : _placePrice;
        }

        private decimal GetServiceChargePct()
        {
            string s = PrintService.GetSetting("service_charge", "0");
            return decimal.TryParse(s, out decimal pct) ? pct : 0;
        }

        private decimal CalcServiceFee(decimal foodTotal)
        {
            if (_customSvcFee > 0)
                return (_customSvcType == "UZS" || _customSvcType == "pct")
                    ? _customSvcFee
                    : Math.Round(foodTotal * _customSvcFee / 100, 0);
            if (!_hasServiceFee) return 0;
            return Math.Round(foodTotal * GetServiceChargePct() / 100, 0);
        }

        private decimal GetGrandTotal()
        {
            decimal food    = GetFoodTotal();
            decimal svc     = CalcServiceFee(food);
            decimal grand   = food + CalcPlaceAmount(food) + svc;
            decimal discAmt = Math.Round(grand * _discountPct / 100, 0);
            return grand - discAmt;
        }

        private void RecalcTotal()
        {
            decimal food      = GetFoodTotal();
            decimal svcAmt    = CalcServiceFee(food);
            decimal placeAmt  = CalcPlaceAmount(food);
            decimal grand     = food + placeAmt + svcAmt;
            decimal discAmt   = Math.Round(grand * _discountPct / 100, 0);
            decimal final     = grand - discAmt;

            lblTotal.Text = final.ToString("N0") + " so'm";
            if (lblTotal.Parent != null)
                lblTotal.Location = new Point(lblTotal.Parent.Width - lblTotal.Width - 14, lblTotal.Top);

            if (lblPlaceInfo != null)
            {
                var parts = new List<string>();
                if (placeAmt > 0)
                {
                    string placeLabel = _placePriceType == "%" ? $"Joy: {_placePrice:G29}%" : $"Joy: {placeAmt:N0}";
                    parts.Add(placeLabel);
                }
                if (svcAmt > 0)     parts.Add($"Xizmat: {svcAmt:N0}");
                if (discAmt > 0)    parts.Add($"Chegirma: −{discAmt:N0}");
                lblPlaceInfo.Text    = string.Join("   ", parts);
                lblPlaceInfo.Visible = parts.Count > 0;
            }
        }

        private void LoadPlaceInfo()
        {
            try
            {
                int pid = placeId;
                dbconnect db = new dbconnect();
                if (pid == 0 && orderId > 0)
                {
                    db.OpenCon();
                    using (SqlCommand c = new SqlCommand("SELECT place_id FROM [order] WHERE id=@oid", db.GetCon()))
                    {
                        c.Parameters.AddWithValue("@oid", orderId);
                        object r = c.ExecuteScalar();
                        if (r != null) pid = Convert.ToInt32(r);
                    }
                    db.CloseCon();
                }
                if (pid == 0) return;

                db.OpenCon();
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT ISNULL(po.price,0) AS price,
                             ISNULL(po.price_type,'UZS') AS price_type,
                             ISNULL(po.serviceFee,'YES') AS svc_fee
                      FROM place_in pi JOIN place_out po ON po.id=pi.place_out_id
                      WHERE pi.id=@pid", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@pid", pid);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            _placePrice     = Convert.ToDecimal(dr["price"]);
                            _placePriceType = dr["price_type"].ToString();
                            if (string.IsNullOrEmpty(_placePriceType)) _placePriceType = "UZS";
                            _hasServiceFee  = dr["svc_fee"].ToString().ToUpper() == "YES";
                        }
                    }
                }
                db.CloseCon();
                RecalcTotal();
            }
            catch { }
        }

        private void LoadExistingOrder()
        {
            if (orderId == 0 && placeId == 0) return;

            try
            {
                dbconnect db = new dbconnect();

                // Find existing unpaid order for this place
                if (orderId == 0 && placeId > 0)
                {
                    string findSql = "SELECT TOP 1 id FROM [order] WHERE place_id=@pid AND paid='NO' ORDER BY id DESC";
                    SqlCommand findCmd = new SqlCommand(findSql, db.GetCon());
                    findCmd.Parameters.AddWithValue("@pid", placeId);
                    db.OpenCon();
                    object res = findCmd.ExecuteScalar();
                    db.CloseCon();
                    if (res != null) orderId = Convert.ToInt32(res);
                }

                if (orderId == 0) return;

                // Concurrent access check: warn if another user already has this table open
                try
                {
                    dbconnect chkDb = new dbconnect();
                    chkDb.OpenCon();
                    using (SqlCommand chk = new SqlCommand(
                        "SELECT u.name FROM place_in pi JOIN [user] u ON u.id=pi.user_id WHERE pi.id=@pid AND pi.user_id<>@me AND pi.user_id IS NOT NULL AND pi.empty='NO'",
                        chkDb.GetCon()))
                    {
                        chk.Parameters.AddWithValue("@pid", placeId > 0 ? placeId : -1);
                        chk.Parameters.AddWithValue("@me", Session.UserId);
                        object ownerName = chk.ExecuteScalar();
                        if (ownerName != null && ownerName != DBNull.Value)
                            MessageBox.Show($"Diqqat! Bu stol hozir '{ownerName}' tomonidan ochiq.\nIkkala foydalanuvchi o'zgartirsа, birinchisi yo'qoladi.", "Bir vaqtda kirish", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    chkDb.CloseCon();
                }
                catch { }

                db.OpenCon();

                // Restore saved order state
                int _loadedPayId = 0;
                string _loadedPayName = "";
                try
                {
                    using (SqlCommand pc = new SqlCommand(@"
                        SELECT ISNULL(payment_id,0) AS payment_id,
                               ISNULL(customer_id,0) AS customer_id,
                               ISNULL(customer_name,'') AS customer_name,
                               ISNULL(delivery_phone,'') AS delivery_phone,
                               ISNULL(delivery_address,'') AS delivery_address,
                               ISNULL(is_delivery,0) AS is_delivery,
                               ISNULL(order_note,'') AS order_note,
                               ISNULL(custom_svc_fee,0) AS custom_svc_fee,
                               ISNULL(custom_svc_type,'%') AS custom_svc_type,
                               ISNULL(discount_pct,0) AS discount_pct
                        FROM [order] WHERE id=@oid", db.GetCon()))
                    {
                        pc.Parameters.AddWithValue("@oid", orderId);
                        using (var dr2 = pc.ExecuteReader())
                        {
                            if (dr2.Read())
                            {
                                _loadedPayId = Convert.ToInt32(dr2["payment_id"]);
                                if (_loadedPayId > 0)
                                    foreach (System.Data.DataRowView drv in comboPayment.Items)
                                        if (Convert.ToInt32(drv["id"]) == _loadedPayId) { comboPayment.SelectedItem = drv; _loadedPayName = drv["name"].ToString(); break; }

                                _customerId   = Convert.ToInt32(dr2["customer_id"]);
                                _customerName = dr2["customer_name"].ToString();
                                if (_customerId > 0) { _btnMijoz.Text = "◎\n" + _customerName; _btnMijoz.BackColor = GoldBg; }

                                _deliveryPhone = dr2["delivery_phone"].ToString();
                                _deliveryAddr  = dr2["delivery_address"].ToString();
                                _isDelivery    = Convert.ToBoolean(dr2["is_delivery"]);
                                if (_isDelivery) { _btnDelivery.Text = "⊙\n" + (_deliveryPhone.Length > 0 ? _deliveryPhone : "Manzil"); _btnDelivery.BackColor = Color.FromArgb(219, 234, 254); }

                                _orderNote = dr2["order_note"].ToString();
                                if (_orderNote.Length > 0) _btnBoshqalar.BackColor = GoldBg;

                                // Bevosita decimal ga o'giramiz — .ToString() orqali o'tkazsa
                                // rus/o'zbek lokali "10,00" ni InvariantCulture 1000 deb o'qiydi.
                                _customSvcFee  = dr2["custom_svc_fee"]  == DBNull.Value ? 0m : Convert.ToDecimal(dr2["custom_svc_fee"]);
                                _customSvcType = dr2["custom_svc_type"].ToString();
                                if (string.IsNullOrEmpty(_customSvcType)) _customSvcType = "%";

                                _discountPct   = dr2["discount_pct"] == DBNull.Value ? 0m : Convert.ToDecimal(dr2["discount_pct"]);
                            }
                        }
                    }
                }
                catch { }

                // Load payment entries from order_payments
                _paymentEntries.Clear();
                try
                {
                    using (SqlCommand p3 = new SqlCommand(
                        "SELECT op.payment_id, p.name, op.amount FROM order_payments op JOIN payment p ON p.id=op.payment_id WHERE op.order_id=@oid ORDER BY op.id",
                        db.GetCon()))
                    {
                        p3.Parameters.AddWithValue("@oid", orderId);
                        using (var dr3 = p3.ExecuteReader())
                            while (dr3.Read())
                                _paymentEntries.Add((Convert.ToInt32(dr3["payment_id"]), dr3["name"].ToString(), Convert.ToDecimal(dr3["amount"])));
                    }
                }
                catch { }

                // Fallback for old orders without order_payments rows
                if (_paymentEntries.Count == 0 && _loadedPayId > 0 && !string.IsNullOrEmpty(_loadedPayName))
                    _paymentEntries.Add((_loadedPayId, _loadedPayName, 0));

                // Update payment button
                if (_paymentEntries.Count == 1)
                    _btnPayment.Text = "◈\n" + _paymentEntries[0].name;
                else if (_paymentEntries.Count > 1)
                    _btnPayment.Text = $"◈\n{_paymentEntries[0].name}+{_paymentEntries.Count - 1}";
                if (_paymentEntries.Count > 0)
                    _btnPayment.BackColor = GoldBg;

                // Load debt info from order_debt (agar to'lanmagan qarz bo'lsa)
                try
                {
                    using (SqlCommand dLoad = new SqlCommand(
                        "SELECT TOP 1 debtor_name, ISNULL(debtor_phone,'') AS debtor_phone, ISNULL(debt_note,'') AS debt_note " +
                        "FROM order_debt WHERE order_id=@oid AND ISNULL(is_paid,0)=0",
                        db.GetCon()))
                    {
                        dLoad.Parameters.AddWithValue("@oid", orderId);
                        using (var dDr = dLoad.ExecuteReader())
                        {
                            if (dDr.Read())
                            {
                                _debtName  = dDr["debtor_name"].ToString();
                                _debtPhone = dDr["debtor_phone"].ToString();
                                _debtNote  = dDr["debt_note"].ToString();
                                if (!string.IsNullOrEmpty(_debtName))
                                    _btnBoshqalar.BackColor = GoldBg;
                            }
                        }
                    }
                }
                catch { }

                string sql = @"SELECT f.id, f.name, f.selling_price, ofd.quantity,
                               ISNULL(f.[count],0) AS food_count,
                               ISNULL(f.is_unlimited,0) AS is_unlimited,
                               ISNULL(ofd.note,'') AS note
                               FROM order_food ofd
                               JOIN food f ON f.id = ofd.food_id
                               WHERE ofd.order_id = @oid";
                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                cmd.Parameters.AddWithValue("@oid", orderId);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    bool unl = Convert.ToBoolean(dr["is_unlimited"]);
                    int qty  = (int)dr["quantity"];
                    int rem  = Convert.ToInt32(dr["food_count"]);
                    CartItem ci = new CartItem
                    {
                        FoodId      = (int)dr["id"],
                        FoodName    = dr["name"].ToString(),
                        Price       = (decimal)dr["selling_price"],
                        Quantity    = qty,
                        IsUnlimited = unl,
                        MaxCount    = unl ? int.MaxValue : qty + rem,
                        Note        = dr["note"].ToString()
                    };
                    Panel row = CreateCartRow(ci, dr["name"].ToString(), _canEditItems);
                    row.Width = Math.Max(100, flpCart.ClientSize.Width - 2);
                    flpCart.Controls.Add(row);
                }

                dr.Close();
                db.CloseCon();
                RecalcTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Zakas yuklashda xatolik: " + ex.Message);
            }
        }

        private void SaveOrder(object sender, EventArgs e)
        {
            if (flpCart.Controls.Count == 0)
            {
                MessageBox.Show("Hech qanday taom tanlanmagan!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (placeId == 0 && orderId == 0)
            {
                MessageBox.Show("Stol tanlanmagan!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Pre-save stock check
            try
            {
                var dbChk = new dbconnect(); dbChk.OpenCon();
                foreach (Panel row in flpCart.Controls.OfType<Panel>())
                {
                    CartItem ci = (CartItem)row.Tag;
                    if (ci.IsUnlimited) continue;

                    // Remaining stock = DB count + what's already committed in this order
                    int dbCount = 0, oldQtyInOrder = 0;
                    using (var c = new SqlCommand("SELECT ISNULL([count],0) FROM food WHERE id=@fid", dbChk.GetCon()))
                    { c.Parameters.AddWithValue("@fid", ci.FoodId); dbCount = Convert.ToInt32(c.ExecuteScalar()); }
                    if (orderId > 0)
                        using (var c = new SqlCommand("SELECT ISNULL(quantity,0) FROM order_food WHERE order_id=@oid AND food_id=@fid", dbChk.GetCon()))
                        { c.Parameters.AddWithValue("@oid", orderId); c.Parameters.AddWithValue("@fid", ci.FoodId); object r = c.ExecuteScalar(); oldQtyInOrder = r != null ? Convert.ToInt32(r) : 0; }

                    int available = dbCount + oldQtyInOrder;
                    if (ci.Quantity > available)
                    {
                        dbChk.CloseCon();
                        MessageBox.Show($"'{ci.FoodName}': so'ralgan {ci.Quantity} ta, omborda faqat {available} ta bor.",
                            "Ombor yetarli emas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                dbChk.CloseCon();
            }
            catch { }

            bool isNewOrder = orderId == 0;

            dbconnect db = new dbconnect();
            db.OpenCon();
            SqlTransaction tr = db.GetCon().BeginTransaction();

            try
            {
                decimal total = GetGrandTotal();

                if (orderId == 0)
                {
                    string insOrder = @"INSERT INTO [order](user_id,place_id,payment_id,created_at,total,paid,
                                        customer_id,customer_name,delivery_phone,delivery_address,is_delivery,
                                        order_note,custom_svc_fee,custom_svc_type,discount_pct)
                                        OUTPUT INSERTED.id
                                        VALUES(@uid,@pid,NULL,GETDATE(),@total,'NO',
                                        @cid,@cname,@dph,@dadr,@isdeliv,@note,@sfee,@stype,@dpct)";
                    SqlCommand cmd = new SqlCommand(insOrder, db.GetCon(), tr);
                    cmd.Parameters.AddWithValue("@uid",    Session.UserId);
                    cmd.Parameters.AddWithValue("@pid",    placeId);
                    cmd.Parameters.AddWithValue("@total",  total);
                    cmd.Parameters.AddWithValue("@cid",    _customerId > 0 ? (object)_customerId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cname",  string.IsNullOrEmpty(_customerName) ? (object)DBNull.Value : _customerName);
                    cmd.Parameters.AddWithValue("@dph",    string.IsNullOrEmpty(_deliveryPhone) ? (object)DBNull.Value : _deliveryPhone);
                    cmd.Parameters.AddWithValue("@dadr",   string.IsNullOrEmpty(_deliveryAddr)  ? (object)DBNull.Value : _deliveryAddr);
                    cmd.Parameters.AddWithValue("@isdeliv", _isDelivery ? 1 : 0);
                    cmd.Parameters.AddWithValue("@note",   string.IsNullOrEmpty(_orderNote) ? (object)DBNull.Value : _orderNote);
                    decimal _sfeeIns = CalcServiceFee(GetFoodTotal());
                    cmd.Parameters.AddWithValue("@sfee",   _sfeeIns > 0 ? (object)_sfeeIns : DBNull.Value);
                    cmd.Parameters.AddWithValue("@stype",  _sfeeIns > 0 ? (object)"UZS" : DBNull.Value);
                    cmd.Parameters.AddWithValue("@dpct",   _discountPct);
                    orderId = (int)cmd.ExecuteScalar();

                    // Mark table as occupied
                    SqlCommand cmdPlace = new SqlCommand("UPDATE place_in SET empty='NO', user_id=@uid WHERE id=@pid", db.GetCon(), tr);
                    cmdPlace.Parameters.AddWithValue("@uid", Session.UserId);
                    cmdPlace.Parameters.AddWithValue("@pid", placeId);
                    cmdPlace.ExecuteNonQuery();
                }

                // Get old food list and quantities
                List<int> oldFoods = new List<int>();
                Dictionary<int, int> oldQtys = new Dictionary<int, int>();
                Dictionary<int, string> oldNames = new Dictionary<int, string>();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT ofd.food_id, ofd.quantity, ISNULL(f.name,'') AS fname FROM order_food ofd LEFT JOIN food f ON f.id=ofd.food_id WHERE ofd.order_id=@oid",
                    db.GetCon(), tr))
                {
                    cmd.Parameters.AddWithValue("@oid", orderId);
                    using (SqlDataReader dr = cmd.ExecuteReader())
                        while (dr.Read())
                        {
                            int fid = dr.GetInt32(0);
                            oldFoods.Add(fid);
                            oldQtys[fid]  = dr.GetInt32(1);
                            oldNames[fid] = dr["fname"].ToString();
                        }
                }

                // New food list from cart
                List<int> newFoods = flpCart.Controls.OfType<Panel>()
                    .Select(p => ((CartItem)p.Tag).FoodId).ToList();

                // Delete removed items
                foreach (int fid in oldFoods.Except(newFoods))
                {
                    SqlCommand del = new SqlCommand("DELETE FROM order_food WHERE order_id=@oid AND food_id=@fid", db.GetCon(), tr);
                    del.Parameters.AddWithValue("@oid", orderId);
                    del.Parameters.AddWithValue("@fid", fid);
                    del.ExecuteNonQuery();
                }

                // Insert or update cart items
                foreach (Panel row in flpCart.Controls.OfType<Panel>())
                {
                    CartItem ci = (CartItem)row.Tag;
                    int exists = 0;
                    using (SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM order_food WHERE order_id=@oid AND food_id=@fid", db.GetCon(), tr))
                    {
                        chk.Parameters.AddWithValue("@oid", orderId);
                        chk.Parameters.AddWithValue("@fid", ci.FoodId);
                        exists = (int)chk.ExecuteScalar();
                    }

                    if (exists == 0)
                    {
                        SqlCommand ins = new SqlCommand("INSERT INTO order_food(order_id,food_id,quantity,note) VALUES(@oid,@fid,@qty,@note)", db.GetCon(), tr);
                        ins.Parameters.AddWithValue("@oid", orderId);
                        ins.Parameters.AddWithValue("@fid", ci.FoodId);
                        ins.Parameters.AddWithValue("@qty", ci.Quantity);
                        ins.Parameters.AddWithValue("@note", string.IsNullOrEmpty(ci.Note) ? (object)DBNull.Value : ci.Note);
                        ins.ExecuteNonQuery();
                    }
                    else
                    {
                        SqlCommand upd = new SqlCommand("UPDATE order_food SET quantity=@qty, note=@note WHERE order_id=@oid AND food_id=@fid", db.GetCon(), tr);
                        upd.Parameters.AddWithValue("@qty", ci.Quantity);
                        upd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(ci.Note) ? (object)DBNull.Value : ci.Note);
                        upd.Parameters.AddWithValue("@oid", orderId);
                        upd.Parameters.AddWithValue("@fid", ci.FoodId);
                        upd.ExecuteNonQuery();
                    }
                }

                // Update total + extra fields
                SqlCommand updTotal = new SqlCommand(@"UPDATE [order] SET total=@total,
                    customer_id=@cid, customer_name=@cname, delivery_phone=@dph, delivery_address=@dadr,
                    is_delivery=@isdeliv, order_note=@note, custom_svc_fee=@sfee, custom_svc_type=@stype,
                    discount_pct=@dpct, is_synced=0 WHERE id=@oid", db.GetCon(), tr);
                updTotal.Parameters.AddWithValue("@total",  total);
                updTotal.Parameters.AddWithValue("@oid",    orderId);
                updTotal.Parameters.AddWithValue("@cid",    _customerId > 0 ? (object)_customerId : DBNull.Value);
                updTotal.Parameters.AddWithValue("@cname",  string.IsNullOrEmpty(_customerName) ? (object)DBNull.Value : _customerName);
                updTotal.Parameters.AddWithValue("@dph",    string.IsNullOrEmpty(_deliveryPhone) ? (object)DBNull.Value : _deliveryPhone);
                updTotal.Parameters.AddWithValue("@dadr",   string.IsNullOrEmpty(_deliveryAddr)  ? (object)DBNull.Value : _deliveryAddr);
                updTotal.Parameters.AddWithValue("@isdeliv", _isDelivery ? 1 : 0);
                updTotal.Parameters.AddWithValue("@note",   string.IsNullOrEmpty(_orderNote) ? (object)DBNull.Value : _orderNote);
                decimal _sfeeUpd = CalcServiceFee(GetFoodTotal());
                updTotal.Parameters.AddWithValue("@sfee",  _sfeeUpd > 0 ? (object)_sfeeUpd : DBNull.Value);
                updTotal.Parameters.AddWithValue("@stype", _sfeeUpd > 0 ? (object)"UZS" : DBNull.Value);
                updTotal.Parameters.AddWithValue("@dpct",   _discountPct);
                updTotal.ExecuteNonQuery();

                // Save payment entries to order_payments
                using (var del2 = new SqlCommand("DELETE FROM order_payments WHERE order_id=@oid", db.GetCon(), tr))
                { del2.Parameters.AddWithValue("@oid", orderId); del2.ExecuteNonQuery(); }
                foreach (var en in _paymentEntries)
                {
                    using (var ins2 = new SqlCommand("INSERT INTO order_payments(order_id,payment_id,amount,sync_token,is_synced) VALUES(@oid,@pid,@amt,NEWID(),0)", db.GetCon(), tr))
                    { ins2.Parameters.AddWithValue("@oid", orderId); ins2.Parameters.AddWithValue("@pid", en.id); ins2.Parameters.AddWithValue("@amt", en.amount); ins2.ExecuteNonQuery(); }
                }

                // Adjust food stock: restore count for removed items
                foreach (int fid in oldFoods.Except(newFoods))
                {
                    int qty = oldQtys.ContainsKey(fid) ? oldQtys[fid] : 0;
                    if (qty > 0)
                    {
                        SqlCommand restoreCmd = new SqlCommand(
                            "UPDATE food SET [count]=[count]+@qty WHERE id=@fid AND (is_unlimited IS NULL OR is_unlimited=0)",
                            db.GetCon(), tr);
                        restoreCmd.Parameters.AddWithValue("@fid", fid);
                        restoreCmd.Parameters.AddWithValue("@qty", qty);
                        restoreCmd.ExecuteNonQuery();
                    }
                }
                // Adjust food stock: deduct for new or changed quantities
                foreach (Panel rowPanel in flpCart.Controls.OfType<Panel>())
                {
                    CartItem ci = (CartItem)rowPanel.Tag;
                    int delta = oldFoods.Contains(ci.FoodId)
                        ? ci.Quantity - (oldQtys.ContainsKey(ci.FoodId) ? oldQtys[ci.FoodId] : 0)
                        : ci.Quantity;
                    if (delta != 0)
                    {
                        SqlCommand adjCmd = new SqlCommand(
                            "UPDATE food SET [count]=CASE WHEN ISNULL([count],0)-@delta < 0 THEN 0 ELSE ISNULL([count],0)-@delta END WHERE id=@fid AND (is_unlimited IS NULL OR is_unlimited=0)",
                            db.GetCon(), tr);
                        adjCmd.Parameters.AddWithValue("@fid", ci.FoodId);
                        adjCmd.Parameters.AddWithValue("@delta", delta);
                        adjCmd.ExecuteNonQuery();
                    }
                }

                // Qarz ma'lumotlarini saqlash (hali yopilmagan order uchun vaqtinchalik)
                if (!string.IsNullOrEmpty(_debtName))
                {
                    SqlCommand dSave = new SqlCommand(@"
                        DELETE FROM order_debt WHERE order_id=@oid AND ISNULL(is_paid,0)=0;
                        INSERT INTO order_debt(order_id,debtor_name,debtor_phone,debt_note,amount,created_at,is_paid)
                        VALUES(@oid,@name,@phone,@note,0,GETDATE(),0)", db.GetCon(), tr);
                    dSave.Parameters.AddWithValue("@oid",   orderId);
                    dSave.Parameters.AddWithValue("@name",  _debtName);
                    dSave.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(_debtPhone) ? (object)DBNull.Value : _debtPhone);
                    dSave.Parameters.AddWithValue("@note",  string.IsNullOrEmpty(_debtNote)  ? (object)DBNull.Value : _debtNote);
                    dSave.ExecuteNonQuery();
                }
                else
                {
                    SqlCommand dDel = new SqlCommand(
                        "DELETE FROM order_debt WHERE order_id=@oid AND ISNULL(is_paid,0)=0",
                        db.GetCon(), tr);
                    dDel.Parameters.AddWithValue("@oid", orderId);
                    dDel.ExecuteNonQuery();
                }

                tr.Commit();
                db.CloseCon();

                // SyncQueue ga qo'shish — background da central ga yuboriladi
                SyncQueueHelper.AddBatch(
                    (SyncQueueHelper.Orders,      orderId, isNewOrder ? "Insert" : "Update"),
                    (SyncQueueHelper.OrderFood,   orderId, "Update"),
                    (SyncQueueHelper.OrderPayments, orderId, "Update")
                );

                // Print kitchen tickets for newly added items
                List<int> newlyAdded = isNewOrder ? newFoods : new List<int>(newFoods);
                if (!isNewOrder)
                {
                    newlyAdded = new List<int>();
                    foreach (int fid in newFoods)
                        if (!oldFoods.Contains(fid)) newlyAdded.Add(fid);
                }
                if (newlyAdded.Count > 0)
                {
                    System.Collections.Generic.Dictionary<int, int> qtys = new System.Collections.Generic.Dictionary<int, int>();
                    System.Collections.Generic.Dictionary<int, string> names2 = new System.Collections.Generic.Dictionary<int, string>();
                    System.Collections.Generic.Dictionary<int, string> notes2 = new System.Collections.Generic.Dictionary<int, string>();
                    foreach (Panel row in flpCart.Controls.OfType<Panel>())
                    {
                        CartItem ci = (CartItem)row.Tag;
                        if (newlyAdded.Contains(ci.FoodId))
                        {
                            qtys[ci.FoodId]   = ci.Quantity;
                            names2[ci.FoodId] = ci.FoodName ?? "";
                            if (!string.IsNullOrEmpty(ci.Note)) notes2[ci.FoodId] = ci.Note;
                        }
                    }
                    List<KitchenItem> kitchenItems = PrintService.GetKitchenItemsFromFoods(newlyAdded, qtys, names2, notes2);
                    PrintService.PrintKitchenTickets(orderId, kitchenItems, Session.UserName, DateTime.Now);
                }

                // Print kitchen cancellation for removed or quantity-reduced items
                if (!isNewOrder)
                {
                    var cancelFoodIds = new List<int>();
                    var cancelQtys    = new Dictionary<int, int>();
                    var cancelNames   = new Dictionary<int, string>();

                    // Fully removed items
                    foreach (int fid in oldFoods.Except(newFoods))
                    {
                        int qty = oldQtys.ContainsKey(fid) ? oldQtys[fid] : 0;
                        if (qty > 0) { cancelFoodIds.Add(fid); cancelQtys[fid] = qty; cancelNames[fid] = oldNames.ContainsKey(fid) ? oldNames[fid] : ""; }
                    }

                    // Partially reduced items
                    foreach (Panel rowPanel in flpCart.Controls.OfType<Panel>())
                    {
                        CartItem ci = (CartItem)rowPanel.Tag;
                        if (oldFoods.Contains(ci.FoodId))
                        {
                            int oldQty = oldQtys.ContainsKey(ci.FoodId) ? oldQtys[ci.FoodId] : 0;
                            int delta  = oldQty - ci.Quantity;
                            if (delta > 0) { cancelFoodIds.Add(ci.FoodId); cancelQtys[ci.FoodId] = delta; cancelNames[ci.FoodId] = ci.FoodName ?? ""; }
                        }
                    }

                    if (cancelFoodIds.Count > 0)
                    {
                        List<KitchenItem> cancelItems = PrintService.GetKitchenItemsFromFoods(cancelFoodIds, cancelQtys, cancelNames);
                        PrintService.PrintKitchenCancellation(orderId, cancelItems, Session.UserName, DateTime.Now);

                        // Log each cancelled item to order_cancellation_log
                        try
                        {
                            var dbLog = new dbconnect(); dbLog.OpenCon();
                            foreach (KitchenItem ki in cancelItems)
                            {
                                var logCmd = new SqlCommand(@"
                                    INSERT INTO order_cancellation_log(order_id,food_id,food_name,food_category,cancelled_qty,cancelled_by,cancelled_at)
                                    VALUES(@oid,@fid,@fname,@fcat,@qty,@by,GETDATE())", dbLog.GetCon());
                                logCmd.Parameters.AddWithValue("@oid",   orderId);
                                logCmd.Parameters.AddWithValue("@fid",   ki.FoodId);
                                logCmd.Parameters.AddWithValue("@fname", ki.FoodName ?? "");
                                logCmd.Parameters.AddWithValue("@fcat",  ki.CategoryName ?? "");
                                logCmd.Parameters.AddWithValue("@qty",   ki.Quantity);
                                logCmd.Parameters.AddWithValue("@by",    Session.UserName ?? "");
                                logCmd.ExecuteNonQuery();
                            }
                            dbLog.CloseCon();
                        }
                        catch { }
                    }
                }

                MessageBox.Show("Buyurtma saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                tr.Rollback();
                db.CloseCon();
                MessageBox.Show("Xatolik: " + ex.Message);
            }
        }

        private void CloseOrder(object sender, EventArgs e)
        {
            if (orderId == 0)
            {
                MessageBox.Show("Aktiv zakas topilmadi!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("Hisobni yopib, stolni bo'shatishni tasdiqlaysizmi?", "Tasdiqlash",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();

                // Get place_id
                int pid = 0;
                using (SqlCommand cmd = new SqlCommand("SELECT place_id FROM [order] WHERE id=@id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    pid = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int payId = 0;
                try { payId = Convert.ToInt32(comboPayment.SelectedValue); } catch { }
                if (payId <= 0 && comboPayment != null && comboPayment.Items.Count > 0)
                    try { payId = Convert.ToInt32(((DataRowView)comboPayment.Items[0])["id"]); } catch { }

                decimal food     = GetFoodTotal();
                decimal svcAmt   = CalcServiceFee(food);
                decimal grand    = food + CalcPlaceAmount(food) + svcAmt;
                decimal discAmt  = Math.Round(grand * _discountPct / 100, 0);
                decimal finalTotal = grand - discAmt;

                // To'lov yozuvlari mavjud bo'lsa, ularning jami buyurtma summasiga teng ekanligini tekshiramiz.
                // Agar mos kelmasa (masalan, chegirma qo'shilgandan keyin to'lov yangilanmagan bo'lsa),
                // foydalanuvchini ogohlantirish kerak.
                if (_paymentEntries.Count > 0)
                {
                    decimal paySum = 0;
                    foreach (var pe in _paymentEntries) paySum += pe.amount;
                    if (paySum != finalTotal)
                    {
                        db.CloseCon();
                        MessageBox.Show(
                            $"To'lov summasi ({paySum:N0} UZS) buyurtma summasiga ({finalTotal:N0} UZS) mos kelmaydi.\n\nChegirma o'zgartirilgandan keyin to'lov ma'lumotlarini qayta kiriting.",
                            "To'lov xatosi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE [order] SET paid='YES', total=@total, payment_id=@pay,
                        discount_amount=@disc, discount_pct=@dpct,
                        customer_id=@cid, customer_name=@cname,
                        delivery_phone=@dph, delivery_address=@dadr, is_delivery=@isdeliv,
                        order_note=@note, custom_svc_fee=@sfee, custom_svc_type=@stype,
                        is_synced=0
                    WHERE id=@id", db.GetCon()))
                {
                    cmd.Parameters.AddWithValue("@id",     orderId);
                    cmd.Parameters.AddWithValue("@total",  finalTotal);
                    cmd.Parameters.AddWithValue("@pay",    payId > 0 ? (object)payId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@disc",   discAmt);
                    cmd.Parameters.AddWithValue("@dpct",   _discountPct);
                    cmd.Parameters.AddWithValue("@cid",    _customerId > 0 ? (object)_customerId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@cname",  string.IsNullOrEmpty(_customerName) ? (object)DBNull.Value : _customerName);
                    cmd.Parameters.AddWithValue("@dph",    string.IsNullOrEmpty(_deliveryPhone) ? (object)DBNull.Value : _deliveryPhone);
                    cmd.Parameters.AddWithValue("@dadr",   string.IsNullOrEmpty(_deliveryAddr)  ? (object)DBNull.Value : _deliveryAddr);
                    cmd.Parameters.AddWithValue("@isdeliv", _isDelivery ? 1 : 0);
                    cmd.Parameters.AddWithValue("@note",   string.IsNullOrEmpty(_orderNote) ? (object)DBNull.Value : _orderNote);
                    cmd.Parameters.AddWithValue("@sfee",   svcAmt > 0 ? (object)svcAmt : DBNull.Value);
                    cmd.Parameters.AddWithValue("@stype",  svcAmt > 0 ? (object)"UZS" : DBNull.Value);
                    cmd.ExecuteNonQuery();
                }

                // Save payment entries
                using (var del3 = new SqlCommand("DELETE FROM order_payments WHERE order_id=@oid", db.GetCon()))
                { del3.Parameters.AddWithValue("@oid", orderId); del3.ExecuteNonQuery(); }
                foreach (var en in _paymentEntries)
                {
                    using (var ins3 = new SqlCommand("INSERT INTO order_payments(order_id,payment_id,amount,sync_token,is_synced) VALUES(@oid,@pid,@amt,NEWID(),0)", db.GetCon()))
                    { ins3.Parameters.AddWithValue("@oid", orderId); ins3.Parameters.AddWithValue("@pid", en.id); ins3.Parameters.AddWithValue("@amt", en.amount); ins3.ExecuteNonQuery(); }
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

                if (pid > 0)
                {
                    using (SqlCommand cmd = new SqlCommand("UPDATE place_in SET empty='YES', user_id=NULL WHERE id=@pid", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@pid", pid);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Calculate Qarz portion from payment entries
                decimal qarzAmt = 0;
                foreach (var en in _paymentEntries)
                    if (en.name.Equals("Qarz", StringComparison.OrdinalIgnoreCase))
                        qarzAmt += en.amount;

                // Prompt for debtor info if Qarz payment exists but no debtor info filled
                if (qarzAmt > 0 && string.IsNullOrEmpty(_debtName))
                {
                    if (MessageBox.Show(
                        $"\"Qarz\" to'lov summasi: {qarzAmt:N0} UZS\nQarzdor ma'lumotlarini kiritasizmi?",
                        "Qarz ma'lumotlari", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        ShowDebtDialog();
                }

                // Save debt record if debtor name is set
                if (!string.IsNullOrEmpty(_debtName))
                {
                    decimal debtAmt = qarzAmt > 0 ? qarzAmt : finalTotal;
                    using (SqlCommand cmd = new SqlCommand(@"
                        DELETE FROM order_debt WHERE order_id=@oid;
                        INSERT INTO order_debt(order_id,debtor_name,debtor_phone,debt_note,amount,created_at,is_paid)
                        VALUES(@oid,@name,@phone,@note,@amt,GETDATE(),0)", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@oid",   orderId);
                        cmd.Parameters.AddWithValue("@name",  _debtName);
                        cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(_debtPhone) ? (object)DBNull.Value : _debtPhone);
                        cmd.Parameters.AddWithValue("@note",  string.IsNullOrEmpty(_debtNote)  ? (object)DBNull.Value : _debtNote);
                        cmd.Parameters.AddWithValue("@amt",   debtAmt);
                        cmd.ExecuteNonQuery();
                    }
                }

                db.CloseCon();

                // To'lov SyncQueue ga — orders + payments
                SyncQueueHelper.AddBatch(
                    (SyncQueueHelper.Orders,      orderId, "Update"),
                    (SyncQueueHelper.OrderPayments, orderId, "Update")
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message);
                return;
            }

            MessageBox.Show("Hisob yopildi. Stol bo'shatildi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            try { PrintService.PrintReceipt(orderId); }
            catch (Exception px) { MessageBox.Show("Chop etishda xatolik:\n" + px.Message, "Printer", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            this.Close();
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
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 800);
            this.Name = "AddOrder";
            this.ResumeLayout(false);
        }
    }
}

public class CartItem
{
    public int FoodId { get; set; }
    public string FoodName { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int MaxCount { get; set; } = int.MaxValue;
    public bool IsUnlimited { get; set; } = true;
    public string Note { get; set; } = "";
}
