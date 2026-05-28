using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.settings
{
    public enum BlockType { Text, Image, Fixed, Number }

    public class ReceiptBlock
    {
        public string    Id, DisplayName, Value;
        public BlockType Type;
        public bool      Visible, Required;
        public Control   InputCtrl;
        public CheckBox  ToggleCtrl;

        public ReceiptBlock(string id, string name, string val, BlockType type, bool visible, bool required = false)
        { Id = id; DisplayName = name; Value = val; Type = type; Visible = visible; Required = required; }
    }

    public class SettingsPanel : UserControl
    {
        static readonly Color Gold    = Color.FromArgb(217, 119, 6);
        static readonly Color BgMain  = Color.FromArgb(248, 248, 250);
        static readonly Color BgCard  = Color.White;
        static readonly Color TextD   = Color.FromArgb(17, 24, 39);
        static readonly Color TextM   = Color.FromArgb(107, 114, 128);
        static readonly Color Border  = Color.FromArgb(229, 231, 235);
        static readonly Color Success = Color.FromArgb(22, 163, 74);
        static readonly Color Danger  = Color.FromArgb(220, 38, 38);

        // Views
        private Panel mainView;
        private Panel printerSubView;
        private Panel asosiyPrinterView;
        private Panel qoshimchaPrinterView;
        private Panel orderSettingsView;

        // Receipt (Asosiy printer)
        private List<ReceiptBlock> blocks;
        private Panel blocksListPanel;
        private Panel previewPanel;
        private ComboBox _cmbAsosiyPrinter;

        // Kitchen ticket (Qo'shimcha printer)
        private List<ReceiptBlock> kitchenBlocks;
        private Panel kitchenBlocksListPanel;
        private Panel kitchenPreviewPanel;

        // Shared
        private string logoPath   = "";
        private Image  logoImage  = null;
        private int    logoWidth  = 64;
        private int    logoHeight = 32;
        private Dictionary<string, bool>  _orderToggles = new Dictionary<string, bool>();
        private Dictionary<string, Panel> _togglePanels = new Dictionary<string, Panel>();
        private TextBox txtMaxDiscount;

        // Ulanish rejimi
        private Panel _connectionTogglePanel;
        private Label _lblConnStatus;
        private Panel _cardConn;

        // Chek shrift sozlamalari
        private Panel chekFontView;
        private TextBox txtKNorm, txtKBold;
        private TextBox txtRNorm, txtRBold, txtRTitle, txtRSmall, txtRTotal, txtROrderNum, txtRLineSpacing, txtRWidth;

        public SettingsPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = BgMain;
            InitBlocks();
            InitKitchenBlocks();
            BuildUI();
            this.Load += (s, e) => LoadSettings();
        }

        // ── Receipt blocks ────────────────────────────────────────────────────
        void InitBlocks()
        {
            blocks = new List<ReceiptBlock>
            {
                new ReceiptBlock("logo",           "Logotip",             "",    BlockType.Image,  true),
                new ReceiptBlock("cafe_name",      "Kafe nomi",           "FoodX Cafe", BlockType.Text, true, required:true),
                new ReceiptBlock("cafe_address",   "Manzil",              "",    BlockType.Text,   true),
                new ReceiptBlock("cafe_phone",     "Telefon",             "",    BlockType.Text,   true),
                new ReceiptBlock("order_number",   "Buyurtma raqami",     "",    BlockType.Fixed,  true),
                new ReceiptBlock("items_table",    "Mahsulotlar jadvali", "",    BlockType.Fixed,  true, required:true),
                new ReceiptBlock("subtotal",       "Jami (subtotal)",     "",    BlockType.Fixed,  true, required:true),
                new ReceiptBlock("service_charge", "Xizmat haqqi (%)",   "0",   BlockType.Number, false),
                new ReceiptBlock("admin_info",     "Admin ma'lumotlari",  "",    BlockType.Fixed,  true),
                new ReceiptBlock("grand_total",    "Umumiy jami",         "",    BlockType.Fixed,  true, required:true),
                new ReceiptBlock("payment",        "To'lov turi",         "",    BlockType.Fixed,  true),
                new ReceiptBlock("thank_msg",      "Minnatdorlik matni",  "Buyurtmangiz uchun rahmat", BlockType.Text, true),
            };
        }

        // ── Kitchen ticket blocks ─────────────────────────────────────────────
        void InitKitchenBlocks()
        {
            kitchenBlocks = new List<ReceiptBlock>
            {
                new ReceiptBlock("kt_category",  "Kategoriya nomi (sarlavha)", "", BlockType.Fixed, true,  required:true),
                new ReceiptBlock("kt_items",     "Mahsulotlar ro'yxati",       "", BlockType.Fixed, true,  required:true),
                new ReceiptBlock("kt_separator", "Ajratuvchi chiziq",           "", BlockType.Fixed, true),
                new ReceiptBlock("kt_admin",     "Admin",                       "", BlockType.Fixed, true),
                new ReceiptBlock("kt_order_num", "Buyurtma raqami",             "", BlockType.Fixed, true),
                new ReceiptBlock("kt_place",     "Joy",                         "", BlockType.Fixed, true),
                new ReceiptBlock("kt_time",      "Vaqt",                        "", BlockType.Fixed, true),
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUILD UI
        // ══════════════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = BgCard };
            header.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, header.Height-1, header.Width, header.Height-1);
            header.Controls.Add(new Label
            {
                Text = "Sozlamalar", Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextD, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(24, 0, 0, 0)
            });
            this.Controls.Add(header);

            mainView = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };
            BuildMainView();
            this.Controls.Add(mainView);

            printerSubView = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Visible = false };
            BuildPrinterSubView();
            this.Controls.Add(printerSubView);
            printerSubView.BringToFront();

            asosiyPrinterView = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Visible = false };
            BuildAsosiyPrinterView();
            this.Controls.Add(asosiyPrinterView);
            asosiyPrinterView.BringToFront();

            qoshimchaPrinterView = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Visible = false };
            BuildQoshimchaPrinterView();
            this.Controls.Add(qoshimchaPrinterView);
            qoshimchaPrinterView.BringToFront();

            orderSettingsView = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 250), Visible = false };
            BuildOrderSettingsView();
            this.Controls.Add(orderSettingsView);
            orderSettingsView.BringToFront();

            chekFontView = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Visible = false };
            BuildChekFontView();
            this.Controls.Add(chekFontView);
            chekFontView.BringToFront();
        }

        // ── Main nav page ─────────────────────────────────────────────────────
        void BuildMainView()
        {
            Panel wrap = new Panel { AutoScroll = true, Dock = DockStyle.Fill, BackColor = BgMain };
            mainView.Controls.Add(wrap);

            Label lblSection = new Label
            {
                Text = "Sozlamalar bo'limlari", AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextM, Location = new Point(24, 20)
            };
            wrap.Controls.Add(lblSection);

            Panel cardPrinter = MakeNavCard(
                "Printer sozlamalari",
                "Asosiy kassa printeri va qo'shimcha (oshxona) printeri\nChek va oshxona chipta sozlamalari",
                "🖨", Success, new Point(24, 52));
            cardPrinter.Click += (s, e) => OpenPrinterSubView();
            foreach (Control c in cardPrinter.Controls) c.Click += (s, e) => OpenPrinterSubView();
            wrap.Controls.Add(cardPrinter);

            Panel cardOrder = MakeNavCard(
                "Buyurtma sozlamalari",
                "Buyurtma bilan bog'liq asosiy sozlamalar",
                "📋", Color.FromArgb(99, 102, 241), new Point(24, 164));
            cardOrder.Click += (s, e) => OpenOrderSettings();
            foreach (Control c in cardOrder.Controls) c.Click += (s, e) => OpenOrderSettings();
            wrap.Controls.Add(cardOrder);

            Panel cardFont = MakeNavCard(
                "Chek shrift sozlamalari",
                "Oshxona cheki va mijoz cheki uchun\nshrift o'lchamlarini sozlash",
                "🖋", Color.FromArgb(139, 92, 246), new Point(24, 276));
            cardFont.Click += (s, e) => OpenChekFontView();
            foreach (Control c in cardFont.Controls) c.Click += (s, e) => OpenChekFontView();
            wrap.Controls.Add(cardFont);

            _cardConn = MakeConnectionCard(new Point(24, 388));
            wrap.Controls.Add(_cardConn);

            wrap.Resize += (s, e) =>
            {
                int cw = Math.Max(300, wrap.ClientSize.Width - 48);
                cardPrinter.Width = cw;
                cardOrder.Width   = cw;
                cardFont.Width    = cw;
                if (_cardConn != null) _cardConn.Width = cw;
                foreach (Control c in cardPrinter.Controls)
                    if (c is Label lbl && !lbl.AutoSize) lbl.Width = cw - 80;
                foreach (Control c in cardOrder.Controls)
                    if (c is Label lbl && !lbl.AutoSize) lbl.Width = cw - 80;
                foreach (Control c in cardFont.Controls)
                    if (c is Label lbl && !lbl.AutoSize) lbl.Width = cw - 80;
            };
        }

        // ── Printer sub-view ──────────────────────────────────────────────────
        void BuildPrinterSubView()
        {
            printerSubView.Controls.Add(MakeBackBar("Printer sozlamalari",
                (s, e) => { printerSubView.Visible = false; mainView.Visible = true; }));

            Panel wrap = new Panel { AutoScroll = true, Dock = DockStyle.Fill, BackColor = BgMain };
            printerSubView.Controls.Add(wrap);

            wrap.Controls.Add(new Label
            {
                Text = "Printer turini tanlang", AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextM, Location = new Point(24, 20)
            });

            Panel cardAsosiy = MakeNavCard(
                "Asosiy printer",
                "Kassada chek (hisob) chiqaradigan printer\nChekdagi maydonlarni sozlash va tartibini o'zgartirish",
                "🖨", Gold, new Point(24, 52));
            cardAsosiy.Click += (s, e) => OpenAsosiyPrinter();
            foreach (Control c in cardAsosiy.Controls) c.Click += (s, e) => OpenAsosiyPrinter();
            wrap.Controls.Add(cardAsosiy);

            Panel cardQosh = MakeNavCard(
                "Qo'shimcha printer",
                "Oshxona — kategoriyalarga biriktirilgan printer\nOshxona chipta (oshxona buyurtma) sozlamalari",
                "🔧", Success, new Point(24, 164));
            cardQosh.Click += (s, e) => OpenQoshimchaPrinter();
            foreach (Control c in cardQosh.Controls) c.Click += (s, e) => OpenQoshimchaPrinter();
            wrap.Controls.Add(cardQosh);

            wrap.Resize += (s, e) =>
            {
                int cw = Math.Max(300, wrap.ClientSize.Width - 48);
                cardAsosiy.Width = cw;
                cardQosh.Width   = cw;
                foreach (Control c in cardAsosiy.Controls)
                    if (c is Label lbl && !lbl.AutoSize) lbl.Width = cw - 80;
                foreach (Control c in cardQosh.Controls)
                    if (c is Label lbl && !lbl.AutoSize) lbl.Width = cw - 80;
            };
        }

        void OpenPrinterSubView()
        {
            mainView.Visible = false;
            printerSubView.Visible = true;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ASOSIY PRINTER VIEW (receipt editor)
        // ══════════════════════════════════════════════════════════════════════
        void BuildAsosiyPrinterView()
        {
            // Back bar
            Panel backBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            backBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, backBar.Height-1, backBar.Width, backBar.Height-1);
            Button btnBack = new Button { Text = "← Orqaga", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = TextM, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand, Location = new Point(16, 8) };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => { asosiyPrinterView.Visible = false; printerSubView.Visible = true; };
            backBar.Controls.Add(btnBack);
            Button btnSave = new Button { Text = "Saqlash", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveAll;
            backBar.Controls.Add(btnSave);
            backBar.Resize += (s, e) => btnSave.Location = new Point(backBar.Width - 130, 8);
            asosiyPrinterView.Controls.Add(backBar);

            // Printer ComboBox – shown as first row inside the blocks list
            _cmbAsosiyPrinter = new ComboBox { Font = new Font("Segoe UI", 9), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = BgMain };
            _cmbAsosiyPrinter.Items.Add("— Windows standart printeri —");
            foreach (string pn in PrinterSettings.InstalledPrinters) _cmbAsosiyPrinter.Items.Add(pn);
            _cmbAsosiyPrinter.SelectedIndex = 0;

            // Body
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };
            asosiyPrinterView.Controls.Add(body);

            Panel leftWrap = new Panel { Width = 520, Dock = DockStyle.Left, BackColor = BgCard };
            leftWrap.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), leftWrap.Width-1, 0, leftWrap.Width-1, leftWrap.Height);
            leftWrap.Controls.Add(new Label { Text = "Asosiy printer  —  chek sozlamalari", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextD, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0) });
            blocksListPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgCard };
            leftWrap.Controls.Add(blocksListPanel);
            body.Controls.Add(leftWrap);

            Panel rightWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Padding = new Padding(20) };
            body.Controls.Add(rightWrap);
            rightWrap.BringToFront();
            rightWrap.Controls.Add(new Label { Text = "Chek namunasi (real vaqt)", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextM, AutoSize = true, Location = new Point(0, 0) });

            Panel previewScroll = new Panel { AutoScroll = true, BackColor = BgMain, Location = new Point(0, 30), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            rightWrap.Controls.Add(previewScroll);
            rightWrap.Resize += (s, e) => previewScroll.Size = new Size(rightWrap.Width - 40, rightWrap.Height - 40);

            previewPanel = new Panel { Width = 300, BackColor = Color.White, Top = 10 };
            previewPanel.Paint += DrawReceiptPreview;
            previewScroll.Controls.Add(previewPanel);

            Action centerPreview = () =>
            {
                previewPanel.Height = Math.Max(600, previewScroll.Height - 20);
                int left = (previewScroll.ClientSize.Width - previewPanel.Width) / 2;
                previewPanel.Left = Math.Max(0, left);
            };
            previewScroll.Resize += (s, e) => centerPreview();
            asosiyPrinterView.VisibleChanged += (s, e) =>
            {
                if (asosiyPrinterView.Visible)
                    asosiyPrinterView.BeginInvoke(new Action(centerPreview));
            };
        }

        void OpenAsosiyPrinter()
        {
            SyncBlockValuesFromUI();
            printerSubView.Visible = false;
            asosiyPrinterView.Visible = true;
            RebuildBlocksList();
            previewPanel?.Invalidate();
        }

        // ══════════════════════════════════════════════════════════════════════
        // QO'SHIMCHA PRINTER VIEW (kitchen ticket editor)
        // ══════════════════════════════════════════════════════════════════════
        void BuildQoshimchaPrinterView()
        {
            // Back bar
            Panel backBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            backBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, backBar.Height-1, backBar.Width, backBar.Height-1);
            Button btnBack = new Button { Text = "← Orqaga", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = TextM, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand, Location = new Point(16, 8) };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => { qoshimchaPrinterView.Visible = false; printerSubView.Visible = true; };
            backBar.Controls.Add(btnBack);
            Button btnSave = new Button { Text = "Saqlash", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveKitchenSettings();
            backBar.Controls.Add(btnSave);
            backBar.Resize += (s, e) => btnSave.Location = new Point(backBar.Width - 130, 8);
            qoshimchaPrinterView.Controls.Add(backBar);

            // Body
            Panel body = new Panel { Dock = DockStyle.Fill, BackColor = BgMain };
            qoshimchaPrinterView.Controls.Add(body);

            Panel leftWrap = new Panel { Width = 520, Dock = DockStyle.Left, BackColor = BgCard };
            leftWrap.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), leftWrap.Width-1, 0, leftWrap.Width-1, leftWrap.Height);
            leftWrap.Controls.Add(new Label { Text = "Qo'shimcha printer  —  oshxona chipta sozlamalari", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextD, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0) });
            kitchenBlocksListPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgCard };
            leftWrap.Controls.Add(kitchenBlocksListPanel);
            body.Controls.Add(leftWrap);

            Panel rightWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Padding = new Padding(20) };
            body.Controls.Add(rightWrap);
            rightWrap.BringToFront();
            rightWrap.Controls.Add(new Label { Text = "Oshxona chipta namunasi", Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextM, AutoSize = true, Location = new Point(0, 0) });

            Panel previewScroll = new Panel { AutoScroll = true, BackColor = BgMain, Location = new Point(0, 30), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            rightWrap.Controls.Add(previewScroll);
            rightWrap.Resize += (s, e) => previewScroll.Size = new Size(rightWrap.Width - 40, rightWrap.Height - 40);

            kitchenPreviewPanel = new Panel { Width = 300, BackColor = Color.White, Location = new Point(10, 10) };
            kitchenPreviewPanel.Paint += DrawKitchenTicketPreview;
            previewScroll.Controls.Add(kitchenPreviewPanel);
            previewScroll.Resize += (s, e) => kitchenPreviewPanel.Height = Math.Max(400, previewScroll.Height - 20);
        }

        void OpenQoshimchaPrinter()
        {
            printerSubView.Visible = false;
            qoshimchaPrinterView.Visible = true;
            RebuildKitchenBlocksList();
            kitchenPreviewPanel?.Invalidate();
        }

        // ── Kitchen block list ────────────────────────────────────────────────
        void RebuildKitchenBlocksList()
        {
            kitchenBlocksListPanel.SuspendLayout();
            kitchenBlocksListPanel.Controls.Clear();
            int y = 98;
            for (int i = 0; i < kitchenBlocks.Count; i++)
            {
                Panel row = BuildKitchenBlockRow(kitchenBlocks[i], i);
                row.Top = y;
                kitchenBlocksListPanel.Controls.Add(row);
                y += row.Height + 2;
            }
            kitchenBlocksListPanel.ResumeLayout();
        }

        Panel BuildKitchenBlockRow(ReceiptBlock b, int idx)
        {
            int rowH = 62;
            Panel row = new Panel { Left = 0, Width = kitchenBlocksListPanel.ClientSize.Width, Height = rowH, BackColor = BgCard };
            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, row.Height-1, row.Width, row.Height-1);

            row.Controls.Add(new Label { Text = (idx+1).ToString(), Font = new Font("Segoe UI", 9), ForeColor = TextM, Width = 22, Height = rowH, Location = new Point(8, 0), TextAlign = ContentAlignment.MiddleCenter });

            CheckBox chk = new CheckBox { Location = new Point(32, (rowH-18)/2), Width = 18, Height = 18, Checked = b.Visible, Enabled = !b.Required };
            b.ToggleCtrl = chk;
            chk.CheckedChanged += (s, e) => { b.Visible = chk.Checked; kitchenPreviewPanel?.Invalidate(); };
            row.Controls.Add(chk);

            row.Controls.Add(new Label { Text = b.DisplayName, Font = new Font("Segoe UI", 9, b.Required ? FontStyle.Bold : FontStyle.Regular), ForeColor = b.Required ? Gold : TextD, Location = new Point(56, 12), Width = 270, Height = 20, AutoSize = false });
            row.Controls.Add(new Label { Text = "(DB dan olinadi)", Font = new Font("Segoe UI", 8, FontStyle.Italic), ForeColor = TextM, Location = new Point(56, 34), AutoSize = true });

            int bw = kitchenBlocksListPanel.ClientSize.Width;
            Button btnUp = SmallBtn("↑", new Point(bw - 64, 8));
            btnUp.Enabled = idx > 0;
            btnUp.Click += (s, e) => MoveKitchenBlock(idx, -1);
            Button btnDn = SmallBtn("↓", new Point(bw - 34, 8));
            btnDn.Enabled = idx < kitchenBlocks.Count - 1;
            btnDn.Click += (s, e) => MoveKitchenBlock(idx, +1);
            row.Controls.Add(btnUp);
            row.Controls.Add(btnDn);

            kitchenBlocksListPanel.Resize += (s, e) =>
            {
                int bw2 = kitchenBlocksListPanel.ClientSize.Width;
                row.Width = bw2; btnUp.Left = bw2 - 64; btnDn.Left = bw2 - 34;
            };
            return row;
        }

        void MoveKitchenBlock(int idx, int dir)
        {
            int ni = idx + dir;
            if (ni < 0 || ni >= kitchenBlocks.Count) return;
            var tmp = kitchenBlocks[idx]; kitchenBlocks[idx] = kitchenBlocks[ni]; kitchenBlocks[ni] = tmp;
            RebuildKitchenBlocksList();
            kitchenPreviewPanel?.Invalidate();
        }

        // ── Kitchen ticket preview ────────────────────────────────────────────
        void DrawKitchenTicketPreview(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            Font fHeader = new Font("Courier New", 11, FontStyle.Bold);
            Font fBold   = new Font("Courier New", 9,  FontStyle.Bold);
            Font fNorm   = new Font("Courier New", 9);
            Font fSmall  = new Font("Courier New", 8);
            Brush black  = Brushes.Black;
            var ctr = new StringFormat { Alignment = StringAlignment.Center };
            var rgt = new StringFormat { Alignment = StringAlignment.Far };

            float x = 8, y = 88, w = kitchenPreviewPanel.Width - 16, lh = 16;
            string sep = new string('-', 32);

            foreach (var b in kitchenBlocks)
            {
                if (!b.Visible) continue;
                switch (b.Id)
                {
                    case "kt_category":
                        g.DrawString("1-ovqat", fHeader, black, new RectangleF(x, y, w, lh+2), ctr); y += lh + 6; break;
                    case "kt_items":
                        g.DrawString("1 portsiya  Lag'mon 0.7", fNorm, black, x, y); y += lh;
                        g.DrawString("1 portsiya  Norin",       fNorm, black, x, y); y += lh; break;
                    case "kt_separator":
                        y += 2; g.DrawString(sep, fSmall, black, x, y); y += lh; break;
                    case "kt_admin":
                        g.DrawString("Admin:", fSmall, black, x, y);
                        g.DrawString("Admin",  fBold,  black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                    case "kt_order_num":
                        g.DrawString("Buyurtma raqami:", fSmall, black, x, y);
                        g.DrawString("13",               fBold,  black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                    case "kt_place":
                        g.DrawString("Joy:", fSmall, black, x, y);
                        g.DrawString("1-etaj 2 - divan", fBold, black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                    case "kt_time":
                        g.DrawString("Vaqt:", fSmall, black, x, y);
                        g.DrawString("2026.05.19 15:59", fBold, black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                }
            }

            int newH = (int)y + 20;
            if (kitchenPreviewPanel.Height != newH) kitchenPreviewPanel.Height = newH;
            fHeader.Dispose(); fBold.Dispose(); fNorm.Dispose(); fSmall.Dispose();
        }

        void LoadKitchenSettings()
        {
            foreach (var b in kitchenBlocks)
            {
                string v = PrintService.GetSetting(b.Id + "_visible", "");
                if (!string.IsNullOrEmpty(v)) b.Visible = v == "1";
            }
            string orderStr = PrintService.GetSetting("kitchen_order", "");
            if (!string.IsNullOrEmpty(orderStr))
            {
                string[] ids = orderStr.Split(',');
                List<ReceiptBlock> ordered = new List<ReceiptBlock>();
                foreach (string id in ids)
                {
                    var found = kitchenBlocks.Find(b => b.Id == id.Trim());
                    if (found != null) ordered.Add(found);
                }
                foreach (var b in kitchenBlocks)
                    if (!ordered.Contains(b)) ordered.Add(b);
                kitchenBlocks = ordered;
            }
        }

        void SaveKitchenSettings()
        {
            foreach (var b in kitchenBlocks)
                if (b.ToggleCtrl != null) b.Visible = b.ToggleCtrl.Checked;

            foreach (var b in kitchenBlocks)
                PrintService.SetSetting(b.Id + "_visible", b.Visible ? "1" : "0");

            var sb = new System.Text.StringBuilder();
            foreach (var b in kitchenBlocks) { if (sb.Length > 0) sb.Append(','); sb.Append(b.Id); }
            PrintService.SetSetting("kitchen_order", sb.ToString());

            MessageBox.Show("Oshxona chipta sozlamalari saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ══════════════════════════════════════════════════════════════════════
        // RECEIPT BLOCK LIST (Asosiy printer)
        // ══════════════════════════════════════════════════════════════════════
        void RebuildBlocksList()
        {
            blocksListPanel.SuspendLayout();
            blocksListPanel.Controls.Clear();

            int y = 8;
            for (int i = 0; i < blocks.Count; i++)
            {
                Panel row = BuildBlockRow(blocks[i], i);
                row.Top = y;
                blocksListPanel.Controls.Add(row);
                y += row.Height + 2;
            }

            // Printer selection row – 13th item at the bottom
            Panel prRow = new Panel { Left = 0, Top = y, Width = blocksListPanel.ClientSize.Width, Height = 66, BackColor = Color.FromArgb(255, 251, 240) };
            prRow.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, prRow.Height - 1, prRow.Width, prRow.Height - 1);
                e.Graphics.FillRectangle(new SolidBrush(Gold), 0, 0, 4, prRow.Height);
            };
            prRow.Controls.Add(new Label { Text = "13", Font = new Font("Segoe UI", 9), ForeColor = TextM, Width = 22, Height = 66, Location = new Point(8, 0), TextAlign = ContentAlignment.MiddleCenter });
            prRow.Controls.Add(new Label { Text = "Printer", Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Gold, Location = new Point(36, 10), Width = 90, Height = 20, AutoSize = false });
            prRow.Controls.Add(new Label { Text = "Chekni qaysi printerga yuborish", Font = new Font("Segoe UI", 8), ForeColor = TextM, Location = new Point(36, 32), AutoSize = true });
            if (_cmbAsosiyPrinter != null)
            {
                _cmbAsosiyPrinter.Location = new Point(200, 20);
                _cmbAsosiyPrinter.Height = 26;
                _cmbAsosiyPrinter.Width = Math.Max(150, blocksListPanel.ClientSize.Width - 220);
                prRow.Controls.Add(_cmbAsosiyPrinter);
            }
            blocksListPanel.Controls.Add(prRow);
            prRow.Resize += (s, e) => { if (_cmbAsosiyPrinter != null) _cmbAsosiyPrinter.Width = Math.Max(150, prRow.Width - 220); };
            blocksListPanel.Resize += (s, e) => prRow.Width = blocksListPanel.ClientSize.Width;

            blocksListPanel.ResumeLayout();
        }

        Panel BuildBlockRow(ReceiptBlock b, int idx)
        {
            int rowH = b.Type == BlockType.Image ? 150 : (b.Type == BlockType.Fixed ? 68 : 88);
            Panel row = new Panel { Left = 0, Width = blocksListPanel.ClientSize.Width, Height = rowH, BackColor = BgCard };
            row.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, row.Height-1, row.Width, row.Height-1);

            row.Controls.Add(new Label { Text = (idx+1).ToString(), Font = new Font("Segoe UI", 9), ForeColor = TextM, Width = 22, Height = rowH, Location = new Point(8, 0), TextAlign = ContentAlignment.MiddleCenter });

            CheckBox chk = new CheckBox { Location = new Point(32, (rowH-18)/2), Width = 18, Height = 18, Checked = b.Visible, Enabled = !b.Required };
            b.ToggleCtrl = chk;
            chk.CheckedChanged += (s, e) => { b.Visible = chk.Checked; previewPanel?.Invalidate(); };
            row.Controls.Add(chk);

            row.Controls.Add(new Label { Text = b.DisplayName, Font = new Font("Segoe UI", 9, b.Required ? FontStyle.Bold : FontStyle.Regular), ForeColor = b.Required ? Gold : TextD, Location = new Point(56, 8), Width = 160, Height = 20, AutoSize = false });

            if (b.Type == BlockType.Text || b.Type == BlockType.Number)
            {
                TextBox tb = new TextBox { Text = b.Value, Font = new Font("Segoe UI", 9), Location = new Point(56, 36), Width = 280, Height = 24, BorderStyle = BorderStyle.FixedSingle };
                tb.TextChanged += (s, e) => { b.Value = tb.Text; previewPanel?.Invalidate(); };
                b.InputCtrl = tb;
                row.Controls.Add(tb);
            }
            else if (b.Type == BlockType.Image)
            {
                PictureBox pb = new PictureBox { Width = 60, Height = 48, Location = new Point(56, 34), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = BgMain };
                if (logoImage != null) pb.Image = logoImage;
                Button btnBrowse = new Button { Text = "Tanlash", Location = new Point(122, 34), Width = 80, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = BgMain, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand };
                btnBrowse.FlatAppearance.BorderColor = Border;
                btnBrowse.Click += (s, e) =>
                {
                    using (var dlg = new OpenFileDialog { Filter = "Image|*.png;*.jpg;*.bmp" })
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            logoPath = dlg.FileName; b.Value = logoPath;
                            try { logoImage = Image.FromFile(logoPath); pb.Image = logoImage; } catch { }
                            previewPanel?.Invalidate();
                        }
                    }
                };
                Button btnClear = new Button { Text = "✕", Location = new Point(208, 34), Width = 30, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = BgMain, ForeColor = Danger, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand };
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.Click += (s, e) => { logoPath = ""; b.Value = ""; logoImage = null; pb.Image = null; previewPanel?.Invalidate(); };

                // Width / Height size controls
                row.Controls.Add(new Label { Text = "Kenglik:", Font = new Font("Segoe UI", 8), ForeColor = TextM, Location = new Point(56, 96), AutoSize = true });
                TextBox tbW = new TextBox { Text = logoWidth.ToString(), Font = new Font("Segoe UI", 9), Location = new Point(112, 93), Width = 54, Height = 22, BorderStyle = BorderStyle.FixedSingle };
                tbW.TextChanged += (s, e) => { if (int.TryParse(tbW.Text, out int v) && v > 0) { logoWidth = v; previewPanel?.Invalidate(); } };
                row.Controls.Add(tbW);

                row.Controls.Add(new Label { Text = "Balandlik:", Font = new Font("Segoe UI", 8), ForeColor = TextM, Location = new Point(178, 96), AutoSize = true });
                TextBox tbH = new TextBox { Text = logoHeight.ToString(), Font = new Font("Segoe UI", 9), Location = new Point(242, 93), Width = 54, Height = 22, BorderStyle = BorderStyle.FixedSingle };
                tbH.TextChanged += (s, e) => { if (int.TryParse(tbH.Text, out int v) && v > 0) { logoHeight = v; previewPanel?.Invalidate(); } };
                row.Controls.Add(tbH);

                row.Controls.Add(new Label { Text = "px", Font = new Font("Segoe UI", 8), ForeColor = TextM, Location = new Point(300, 96), AutoSize = true });

                row.Controls.Add(pb); row.Controls.Add(btnBrowse); row.Controls.Add(btnClear);
            }
            else
            {
                row.Controls.Add(new Label { Text = "(DB dan olinadi)", Font = new Font("Segoe UI", 8, FontStyle.Italic), ForeColor = TextM, Location = new Point(56, 34), AutoSize = true });
            }

            int bw = blocksListPanel.ClientSize.Width;
            Button btnUp = SmallBtn("↑", new Point(bw - 64, 8)); btnUp.Enabled = idx > 0; btnUp.Click += (s, e) => { SyncBlockValuesFromUI(); MoveBlock(idx, -1); };
            Button btnDn = SmallBtn("↓", new Point(bw - 34, 8)); btnDn.Enabled = idx < blocks.Count - 1; btnDn.Click += (s, e) => { SyncBlockValuesFromUI(); MoveBlock(idx, +1); };
            row.Controls.Add(btnUp); row.Controls.Add(btnDn);
            blocksListPanel.Resize += (s, e) => { int bw2 = blocksListPanel.ClientSize.Width; row.Width = bw2; btnUp.Left = bw2 - 64; btnDn.Left = bw2 - 34; };
            return row;
        }

        Button SmallBtn(string text, Point loc)
        {
            Button b = new Button { Text = text, Location = loc, Width = 26, Height = 26, FlatStyle = FlatStyle.Flat, BackColor = BgMain, ForeColor = TextD, Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Border; b.FlatAppearance.BorderSize = 1;
            return b;
        }

        void MoveBlock(int idx, int dir) { int ni = idx + dir; if (ni < 0 || ni >= blocks.Count) return; var tmp = blocks[idx]; blocks[idx] = blocks[ni]; blocks[ni] = tmp; RebuildBlocksList(); previewPanel?.Invalidate(); }

        void SyncBlockValuesFromUI()
        {
            foreach (var b in blocks)
            {
                if (b.InputCtrl is TextBox tb) b.Value = tb.Text;
                if (b.ToggleCtrl != null)       b.Visible = b.ToggleCtrl.Checked;
            }
        }

        // ── Receipt preview ───────────────────────────────────────────────────
        void DrawReceiptPreview(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);
            Font fTitle = new Font("Courier New", 10, FontStyle.Bold);
            Font fBig   = new Font("Courier New", 9,  FontStyle.Bold);
            Font fBold  = new Font("Courier New", 7,  FontStyle.Bold);
            Font fNorm  = new Font("Courier New", 7);
            Font fSmall = new Font("Courier New", 6);
            Brush black = Brushes.Black;
            var ctr = new StringFormat { Alignment = StringAlignment.Center };
            var rgt = new StringFormat { Alignment = StringAlignment.Far };
            float x = 8, y = 88, w = previewPanel.Width - 16, lh = 12;
            string sep = new string('-', 38);
            decimal subtotal = 324000m, svcAmt = 0;

            foreach (var b in blocks)
            {
                if (!b.Visible) continue;
                switch (b.Id)
                {
                    case "logo":
                        if (logoImage != null) { g.DrawImage(logoImage, x + (w - logoWidth) / 2, y, logoWidth, logoHeight); y += logoHeight + 4; } break;
                    case "cafe_name":
                        string nm = string.IsNullOrWhiteSpace(b.Value) ? "Kafe nomi" : b.Value;
                        g.DrawString(nm, fTitle, black, new RectangleF(x, y, w, lh+4), ctr); y += lh + 4; break;
                    case "cafe_address":
                        if (!string.IsNullOrWhiteSpace(b.Value)) { g.DrawString(b.Value, fNorm, black, new RectangleF(x, y, w, lh), ctr); y += lh; } break;
                    case "cafe_phone":
                        if (!string.IsNullOrWhiteSpace(b.Value)) { g.DrawString(b.Value, fNorm, black, new RectangleF(x, y, w, lh), ctr); y += lh; } break;
                    case "order_number":
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        g.DrawString("Buyurtma: 12", fNorm, black, x, y); y += lh;
                        g.DrawString(sep, fNorm, black, x, y); y += lh; break;
                    case "items_table":
                        // Printer bilan bir xil: 12+4+8+1+7=32 monospace char
                        Func<string,string,string,string,string> fmtRow =
                            (rn, rq, rp, rs) =>
                                (rn.Length > 12 ? rn.Substring(0,12) : rn).PadRight(12)
                              + (rq.Length > 4  ? rq.Substring(0,4)  : rq).PadLeft(4)
                              + (rp.Length > 8  ? rp.Substring(0,8)  : rp).PadLeft(8)
                              + " "
                              + (rs.Length > 7  ? rs.Substring(0,7)  : rs).PadLeft(7);
                        g.DrawString(fmtRow("Mahsulot","Miq","Narx","Jami"), fBold, black, x, y); y += lh;
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        string[][] sample = {
                            new[]{"Adrenaline","1","18 000","18 000"},
                            new[]{"Jiz 0.5","2","140 000","280 000"},
                            new[]{"Tushonka","1","154 000","154 000"},
                            new[]{"Jigar","3","16 000","48 000"}
                        };
                        foreach (var it in sample)
                        {
                            g.DrawString(fmtRow(it[0], it[1], it[2], it[3]), fBold, black, x, y);
                            y += lh;
                        }
                        break;
                    case "subtotal":
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        g.DrawString("Jami:", fBold, black, x, y); g.DrawString(subtotal.ToString("N0") + " UZS", fBold, black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                    case "service_charge":
                        decimal pct = 0; decimal.TryParse(b.Value, out pct);
                        if (pct > 0) { svcAmt = Math.Round(subtotal * pct / 100); g.DrawString(sep, fNorm, black, x, y); y += lh; g.DrawString($"xizmat haqqi: {pct:0.#} %", fNorm, black, x, y); g.DrawString(svcAmt.ToString("N0") + " UZS", fBold, black, new RectangleF(x, y, w, lh), rgt); y += lh; } break;
                    case "admin_info":
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        void InfoRow2(string lbl, string val) { g.DrawString(lbl, fNorm, black, x, y); g.DrawString(val, fBold, black, new RectangleF(x, y, w, lh), rgt); y += lh; }
                        InfoRow2("Admin:", "Admin"); InfoRow2("Joy nomi:", "2 - divan, 1-etaj"); InfoRow2("Yaratilgan:", DateTime.Now.ToString("yyyy.MM.dd HH:mm")); InfoRow2("Holat:", "Bajarilgan"); break;
                    case "grand_total":
                        decimal grand = subtotal + svcAmt; g.DrawString(sep, fNorm, black, x, y); y += lh;
                        g.DrawString("Jami:", fBig, black, x, y); g.DrawString(grand.ToString("N0") + " UZS", fBig, black, new RectangleF(x, y, w, lh+3), rgt); y += lh + 3; break;
                    case "payment":
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        g.DrawString("Naqd pul", fNorm, black, x, y); g.DrawString((subtotal+svcAmt).ToString("N0") + " UZS", fNorm, black, new RectangleF(x, y, w, lh), rgt); y += lh; break;
                    case "thank_msg":
                        g.DrawString(sep, fNorm, black, x, y); y += lh;
                        string tm = string.IsNullOrWhiteSpace(b.Value) ? "Buyurtmangiz uchun rahmat" : b.Value;
                        g.DrawString(tm, fSmall, black, new RectangleF(x, y, w, lh*2), ctr); y += lh + 2;
                        g.DrawString("v2.8.202", fSmall, black, new RectangleF(x, y, w, lh), ctr); y += lh; break;
                }
            }
            int newH = (int)y + 20;
            if (previewPanel.Height != newH) previewPanel.Height = newH;
            fTitle.Dispose(); fBig.Dispose(); fBold.Dispose(); fNorm.Dispose(); fSmall.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // LOAD / SAVE
        // ══════════════════════════════════════════════════════════════════════
        void LoadSettings()
        {
            foreach (var b in blocks)
            {
                if (b.Type == BlockType.Text || b.Type == BlockType.Number)
                    b.Value = PrintService.GetSetting(b.Id, b.Value);
                else if (b.Type == BlockType.Image)
                {
                    b.Value = PrintService.GetSetting("logo_path", "");
                    logoPath = b.Value;
                    if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
                        try { logoImage = Image.FromFile(logoPath); } catch { }
                    int.TryParse(PrintService.GetSetting("logo_width",  "64"), out logoWidth);  if (logoWidth  <= 0) logoWidth  = 64;
                    int.TryParse(PrintService.GetSetting("logo_height", "32"), out logoHeight); if (logoHeight <= 0) logoHeight = 32;
                }
                string visSaved = PrintService.GetSetting(b.Id + "_visible", "");
                if (!string.IsNullOrEmpty(visSaved)) b.Visible = visSaved == "1";
            }

            string orderStr = PrintService.GetSetting("receipt_order", "");
            if (!string.IsNullOrEmpty(orderStr))
            {
                string[] ids = orderStr.Split(',');
                List<ReceiptBlock> ordered = new List<ReceiptBlock>();
                foreach (string id in ids) { var found = blocks.Find(b => b.Id == id.Trim()); if (found != null) ordered.Add(found); }
                foreach (var b in blocks) if (!ordered.Contains(b)) ordered.Add(b);
                blocks = ordered;
            }

            string savedPrinter = PrintService.GetSetting("main_printer", "");
            if (_cmbAsosiyPrinter != null)
            {
                _cmbAsosiyPrinter.SelectedIndex = 0;
                for (int i = 1; i < _cmbAsosiyPrinter.Items.Count; i++)
                    if (_cmbAsosiyPrinter.Items[i].ToString().Equals(savedPrinter, StringComparison.OrdinalIgnoreCase))
                    { _cmbAsosiyPrinter.SelectedIndex = i; break; }
            }

            LoadKitchenSettings();
            LoadOrderSettings();
            LoadChekFontSettings();
            LoadConnectionSetting();
        }

        void LoadConnectionSetting()
        {
            string mode = PrintService.GetSetting("connection_mode", "");
            if (mode == "offline")
            {
                Session.IsOnline     = false;
                Session.ForceOffline = true;
            }
            else if (mode == "online")
            {
                Session.ForceOffline = false;
            }

            Color colorOn = Color.FromArgb(22, 163, 74);
            if (_connectionTogglePanel != null) _connectionTogglePanel.Invalidate();
            if (_cardConn             != null) _cardConn.Invalidate();
            if (_lblConnStatus        != null)
            {
                _lblConnStatus.Text      = Session.IsOnline ? "Onlayn — serverga ulangan" : "Oflayn — mahalliy bazada";
                _lblConnStatus.ForeColor = Session.IsOnline ? colorOn : TextM;
            }
        }

        void SaveAll(object sender, EventArgs e)
        {
            SyncBlockValuesFromUI();
            var cafeName = blocks.Find(b => b.Id == "cafe_name");
            if (cafeName != null && string.IsNullOrWhiteSpace(cafeName.Value))
            { MessageBox.Show("Kafe nomini kiriting!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            var svcBlock = blocks.Find(b => b.Id == "service_charge");
            if (svcBlock != null) { decimal pct; if (!decimal.TryParse(svcBlock.Value, out pct) || pct < 0 || pct > 100) { MessageBox.Show("Xizmat haqqi 0–100 oralig'ida bo'lishi kerak!", "Diqqat", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } }

            foreach (var b in blocks)
            {
                if (b.Type == BlockType.Text || b.Type == BlockType.Number) PrintService.SetSetting(b.Id, b.Value);
                else if (b.Type == BlockType.Image)
                {
                    PrintService.SetSetting("logo_path",   logoPath);
                    PrintService.SetSetting("logo_width",  logoWidth.ToString());
                    PrintService.SetSetting("logo_height", logoHeight.ToString());
                }
                PrintService.SetSetting(b.Id + "_visible", b.Visible ? "1" : "0");
            }
            var ids2 = new System.Text.StringBuilder();
            foreach (var b in blocks) { if (ids2.Length > 0) ids2.Append(','); ids2.Append(b.Id); }
            PrintService.SetSetting("receipt_order", ids2.ToString());

            if (_cmbAsosiyPrinter != null)
            {
                string p = _cmbAsosiyPrinter.SelectedIndex <= 0 ? "" : _cmbAsosiyPrinter.SelectedItem.ToString();
                PrintService.SetSetting("main_printer", p);
            }

            MessageBox.Show("Sozlamalar saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            previewPanel?.Invalidate();
        }

        // ══════════════════════════════════════════════════════════════════════
        // BUYURTMA SOZLAMALARI
        // ══════════════════════════════════════════════════════════════════════
        void OpenOrderSettings() { LoadOrderSettings(); mainView.Visible = false; orderSettingsView.Visible = true; }

        void BuildOrderSettingsView()
        {
            Panel backBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            backBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, backBar.Height-1, backBar.Width, backBar.Height-1);
            Button btnBack = new Button { Text = "← Orqaga", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = TextM, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand, Location = new Point(16, 8) };
            btnBack.FlatAppearance.BorderSize = 0; btnBack.Click += (s, e) => { orderSettingsView.Visible = false; mainView.Visible = true; }; backBar.Controls.Add(btnBack);
            Button btnSave = new Button { Text = "Saqlash", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0; btnSave.Click += (s, e) => SaveOrderSettings(); backBar.Controls.Add(btnSave);
            backBar.Resize += (s, e) => btnSave.Location = new Point(backBar.Width - 130, 8);
            backBar.Controls.Add(new Label { Text = "Buyurtma sozlamalari", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextD, AutoSize = true, Location = new Point(140, 14) });
            orderSettingsView.Controls.Add(backBar);

            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(245, 246, 250), Padding = new Padding(24, 39, 24, 24) };
            orderSettingsView.Controls.Add(flow);

            var rows = new (string key, string label, bool def)[]
            {
                ("order_remove_items", "Buyurtmani yopmasdan, mahsulotlarni ayirish",    false),
                ("order_anyone_close", "Buyurtmani boshqalar ham yopsin.",                true),
                ("kassir_delete",      "Kassir uchun kamaytirish/o'chirish ruxsati",      false),
                ("waiter_delete",      "Ofitsiant uchun kamaytirish/o'chirish ruxsati",   false),
                ("virtual_keyboard",   "Virtual klaviatura",                              true),
            };
            foreach (var r in rows) { if (!_orderToggles.ContainsKey(r.key)) _orderToggles[r.key] = r.def; flow.Controls.Add(MakeOrderSettingRow(r.key, r.label)); }
            flow.Controls.Add(MakeDiscountRow());
            flow.Resize += (s, e) => { int fw = Math.Max(200, flow.ClientSize.Width - flow.Padding.Horizontal); foreach (Control c in flow.Controls) if (c is Panel) c.Width = fw; };
        }

        Panel MakeOrderSettingRow(string key, string label)
        {
            Panel row = new Panel { Width = 600, Height = 64, BackColor = BgCard, Margin = new Padding(0, 0, 0, 8) };
            row.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = RoundRect(row.ClientRectangle, 10)) { e.Graphics.FillPath(new SolidBrush(BgCard), path); e.Graphics.DrawPath(new Pen(Border), path); } };
            Label lbl = new Label { Text = label, Font = new Font("Segoe UI", 10), ForeColor = TextD, AutoSize = true };
            row.Controls.Add(lbl);
            Panel toggle = MakeOrderToggle(key);
            row.Controls.Add(toggle);
            row.Resize += (s, e) => { lbl.Location = new Point(20, (row.Height - lbl.PreferredHeight) / 2); toggle.Location = new Point(row.Width - toggle.Width - 20, (row.Height - toggle.Height) / 2); };
            return row;
        }

        Panel MakeOrderToggle(string key)
        {
            Color colorOn = Color.FromArgb(22, 163, 74), colorOff = Color.FromArgb(209, 213, 219);
            int W = 50, H = 28;
            Panel t = new Panel { Width = W, Height = H, Cursor = Cursors.Hand, BackColor = Color.Transparent };
            _togglePanels[key] = t;
            t.Paint += (s, e) =>
            {
                bool cur = _orderToggles.ContainsKey(key) && _orderToggles[key];
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(cur ? colorOn : colorOff)) { e.Graphics.FillEllipse(b, 0, 0, H, H); e.Graphics.FillEllipse(b, W-H, 0, H, H); e.Graphics.FillRectangle(b, H/2, 0, W-H, H); }
                int tx = cur ? W - H + 2 : 2;
                e.Graphics.FillEllipse(Brushes.White, tx, 2, H-4, H-4);
            };
            t.Click += (s, e) => { _orderToggles[key] = !(_orderToggles.ContainsKey(key) && _orderToggles[key]); t.Invalidate(); };
            return t;
        }

        Panel MakeDiscountRow()
        {
            Panel row = new Panel { Width = 600, Height = 88, BackColor = BgCard, Margin = new Padding(0) };
            row.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var path = RoundRect(row.ClientRectangle, 10)) { e.Graphics.FillPath(new SolidBrush(BgCard), path); e.Graphics.DrawPath(new Pen(Border), path); } };
            row.Controls.Add(new Label { Text = "Maksimum chegirma foizi", Font = new Font("Segoe UI", 10), ForeColor = TextD, Location = new Point(20, 14), AutoSize = true });
            row.Controls.Add(new Label { Text = "Bitta buyurtmaga qo'llanadigan foiz chegirmaning yuqori chegarasi (%).", Font = new Font("Segoe UI", 8), ForeColor = TextM, Location = new Point(20, 42), Width = 450, Height = 18, AutoSize = false });
            txtMaxDiscount = new TextBox { Width = 72, Height = 28, Font = new Font("Segoe UI", 11), TextAlign = HorizontalAlignment.Center, Text = "50", BorderStyle = BorderStyle.FixedSingle };
            Label lblPct = new Label { Text = "%", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextD, AutoSize = true };
            row.Controls.Add(txtMaxDiscount); row.Controls.Add(lblPct);
            row.Resize += (s, e) => { txtMaxDiscount.Location = new Point(row.Width - 112, (row.Height - txtMaxDiscount.Height) / 2); lblPct.Location = new Point(row.Width - 36, (row.Height - lblPct.PreferredHeight) / 2); };
            return row;
        }

        void LoadOrderSettings()
        {
            string[] keys = { "order_remove_items", "order_anyone_close", "kassir_delete", "waiter_delete", "virtual_keyboard" };
            foreach (string key in keys) { string v = PrintService.GetSetting(key, ""); if (!string.IsNullOrEmpty(v)) _orderToggles[key] = v == "1"; }
            string disc = PrintService.GetSetting("max_discount", "50");
            if (txtMaxDiscount != null) txtMaxDiscount.Text = disc;
            foreach (var kv in _togglePanels) kv.Value.Invalidate();
        }

        void SaveOrderSettings()
        {
            string[] keys = { "order_remove_items", "order_anyone_close", "kassir_delete", "waiter_delete", "virtual_keyboard" };
            foreach (string key in keys) PrintService.SetSetting(key, (_orderToggles.ContainsKey(key) && _orderToggles[key]) ? "1" : "0");
            if (txtMaxDiscount != null) { int v; string val = (int.TryParse(txtMaxDiscount.Text, out v) && v >= 0 && v <= 100) ? v.ToString() : "50"; PrintService.SetSetting("max_discount", val); txtMaxDiscount.Text = val; }
            MessageBox.Show("Buyurtma sozlamalari saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CHEK SHRIFT SOZLAMALARI
        // ══════════════════════════════════════════════════════════════════════
        void BuildChekFontView()
        {
            const int INNER_W = 920;

            Panel backBar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            backBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, backBar.Height - 1, backBar.Width, backBar.Height - 1);
            Button btnBack = new Button { Text = "← Orqaga", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = TextM, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand, Location = new Point(16, 8) };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => { chekFontView.Visible = false; mainView.Visible = true; };
            backBar.Controls.Add(btnBack);
            backBar.Controls.Add(new Label { Text = "Chek shrift sozlamalari", Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextD, AutoSize = true, Location = new Point(140, 14) });
            chekFontView.Controls.Add(backBar);

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgMain };
            chekFontView.Controls.Add(scroll);

            Panel inner = new Panel { Width = INNER_W, Top = 24, BackColor = BgMain };
            scroll.Controls.Add(inner);
            scroll.Resize += (s, e) => inner.Left = Math.Max(0, (scroll.ClientSize.Width - INNER_W) / 2);

            int iy = 0;

            // ── Oshxona cheki sozlamalari ─────────────────────────────────────
            Panel kitCard = MakeFontCard("Oshxona cheki sozlamalari", INNER_W);
            kitCard.Top = iy;
            int kcy = 52;
            txtKNorm = MakeFontInput(kitCard, "Asosiy shrift", "10",  20,  kcy, 430);
            txtKBold = MakeFontInput(kitCard, "Bold shrift",   "12",  470, kcy, 430);
            kcy += 82;
            kitCard.Height = kcy + 20;
            inner.Controls.Add(kitCard);
            iy += kitCard.Height + 16;

            // ── Chek sozlamalari ──────────────────────────────────────────────
            Panel recCard = MakeFontCard("Chek sozlamalari", INNER_W);
            recCard.Top = iy;
            int rcy = 52;
            txtRNorm     = MakeFontInput(recCard, "Asosiy matn",     "9",    20,  rcy, 280);
            txtRBold     = MakeFontInput(recCard, "Bold matn",       "9",    320, rcy, 280);
            txtRTitle    = MakeFontInput(recCard, "Sarlavha",        "16",   620, rcy, 280);
            rcy += 82;
            txtRSmall    = MakeFontInput(recCard, "Kichik matn",     "7.5",  20,  rcy, 280);
            txtRTotal    = MakeFontInput(recCard, "Jami summa",      "18",   320, rcy, 280);
            txtROrderNum = MakeFontInput(recCard, "Buyurtma raqami", "12",   620, rcy, 280);
            rcy += 82;
            txtRLineSpacing = MakeFontInput(recCard, "Qator oralig'i", "4",  20,  rcy, 430);
            txtRWidth       = MakeFontInput(recCard, "Chek eni (mm)",  "72", 470, rcy, 430);
            rcy += 82;
            recCard.Height = rcy + 20;
            inner.Controls.Add(recCard);
            iy += recCard.Height + 24;

            // ── Saqlash tugmasi ───────────────────────────────────────────────
            Button btnSave = new Button { Left = 0, Top = iy, Width = INNER_W, Height = 52, Text = "✓  Saqlash", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(22, 163, 74), ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => SaveChekFontSettings();
            inner.Controls.Add(btnSave);
            iy += 52 + 32;

            inner.Height = iy;
        }

        Panel MakeFontCard(string title, int w)
        {
            Panel card = new Panel { Left = 0, Width = w, BackColor = BgCard };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 12))
                {
                    e.Graphics.FillPath(new SolidBrush(BgCard), path);
                    e.Graphics.DrawPath(new Pen(Border), path);
                }
            };
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextD, Location = new Point(20, 16), AutoSize = true });
            return card;
        }

        TextBox MakeFontInput(Panel parent, string label, string defVal, int x, int y, int w)
        {
            parent.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 9.5f), ForeColor = TextM, Location = new Point(x, y), Width = w, Height = 20, AutoSize = false });
            Panel wrap = new Panel { Left = x, Top = y + 24, Width = w, Height = 44, BackColor = Color.FromArgb(243, 244, 246) };
            wrap.Paint += (s2, e2) =>
            {
                e2.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(wrap.ClientRectangle, 10))
                    e2.Graphics.FillPath(new SolidBrush(Color.FromArgb(243, 244, 246)), path);
            };
            parent.Controls.Add(wrap);
            TextBox tb = new TextBox { Text = defVal, Font = new Font("Segoe UI", 14), Location = new Point(14, 9), Width = w - 28, Height = 26, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(243, 244, 246) };
            wrap.Controls.Add(tb);
            return tb;
        }

        void OpenChekFontView()
        {
            LoadChekFontSettings();
            mainView.Visible = false;
            chekFontView.Visible = true;
        }

        void LoadChekFontSettings()
        {
            if (txtKNorm        != null) txtKNorm.Text        = PrintService.GetSetting("font_kitchen_norm",        "10");
            if (txtKBold        != null) txtKBold.Text        = PrintService.GetSetting("font_kitchen_bold",        "12");
            if (txtRNorm        != null) txtRNorm.Text        = PrintService.GetSetting("font_receipt_norm",        "9");
            if (txtRBold        != null) txtRBold.Text        = PrintService.GetSetting("font_receipt_bold",        "9");
            if (txtRTitle       != null) txtRTitle.Text       = PrintService.GetSetting("font_receipt_title",       "16");
            if (txtRSmall       != null) txtRSmall.Text       = PrintService.GetSetting("font_receipt_small",       "7.5");
            if (txtRTotal       != null) txtRTotal.Text       = PrintService.GetSetting("font_receipt_total",       "18");
            if (txtROrderNum    != null) txtROrderNum.Text    = PrintService.GetSetting("font_receipt_ordernum",    "12");
            if (txtRLineSpacing != null) txtRLineSpacing.Text = PrintService.GetSetting("font_receipt_linespacing", "4");
            if (txtRWidth       != null) txtRWidth.Text       = PrintService.GetSetting("font_receipt_width_mm",    "72");
        }

        void SaveChekFontSettings()
        {
            string[] keys  = { "font_kitchen_norm", "font_kitchen_bold", "font_receipt_norm", "font_receipt_bold", "font_receipt_title", "font_receipt_small", "font_receipt_total", "font_receipt_ordernum", "font_receipt_linespacing", "font_receipt_width_mm" };
            TextBox[] tbs  = { txtKNorm, txtKBold, txtRNorm, txtRBold, txtRTitle, txtRSmall, txtRTotal, txtROrderNum, txtRLineSpacing, txtRWidth };
            string[] defs  = { "10", "12", "9", "9", "16", "7.5", "18", "12", "4", "72" };
            for (int i = 0; i < keys.Length; i++)
            {
                if (tbs[i] == null) continue;
                float v;
                string raw = tbs[i].Text.Replace(",", ".");
                bool ok = float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;
                string val = ok ? raw : defs[i];
                PrintService.SetSetting(keys[i], val);
                tbs[i].Text = val;
            }
            MessageBox.Show("Chek shrift sozlamalari saqlandi!", "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Ulanish rejimi kartasi ────────────────────────────────────────────
        Panel MakeConnectionCard(Point loc)
        {
            Color colorOn  = Color.FromArgb(22, 163, 74);
            Color colorOff = Color.FromArgb(107, 114, 128);

            Panel card = new Panel { Width = 480, Height = 100, Location = loc, BackColor = BgCard };
            card.Paint += (s, e) =>
            {
                Color accent = Session.IsOnline ? colorOn : colorOff;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(BgCard), path);
                    e.Graphics.DrawPath(new Pen(Border), path);
                }
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, 5, card.Height);
            };

            card.Controls.Add(new Label { Text = "🌐", Font = new Font("Segoe UI", 24), ForeColor = TextM, Location = new Point(18, 14), AutoSize = true });
            card.Controls.Add(new Label { Text = "Ulanish rejimi", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = TextD, Location = new Point(82, 22), AutoSize = true });

            _lblConnStatus = new Label
            {
                Text      = Session.IsOnline ? "Onlayn — serverga ulangan" : "Oflayn — mahalliy bazada",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Session.IsOnline ? colorOn : TextM,
                Location  = new Point(82, 48),
                Width = 280, Height = 20, AutoSize = false
            };
            card.Controls.Add(_lblConnStatus);

            int W = 50, H = 28;
            _connectionTogglePanel = new Panel { Width = W, Height = H, Location = new Point(card.Width - 70, 36), Cursor = Cursors.Hand, BackColor = Color.Transparent };
            _connectionTogglePanel.Paint += (s, e) =>
            {
                bool cur = Session.IsOnline;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(cur ? colorOn : Color.FromArgb(209, 213, 219)))
                {
                    e.Graphics.FillEllipse(b, 0, 0, H, H);
                    e.Graphics.FillEllipse(b, W - H, 0, H, H);
                    e.Graphics.FillRectangle(b, H / 2, 0, W - H, H);
                }
                int tx = cur ? W - H + 2 : 2;
                e.Graphics.FillEllipse(Brushes.White, tx, 2, H - 4, H - 4);
            };
            _connectionTogglePanel.Click += delegate
            {
                if (!Session.IsOnline)
                {
                    bool canConnect = dbconnect.CheckCentral();
                    if (!canConnect)
                    {
                        MessageBox.Show(
                            "Server bilan ulanib bo'lmadi.\nInternet aloqasini tekshiring.",
                            "Ulanish xatosi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Session.IsOnline    = true;
                    Session.ForceOffline = false;
                    PrintService.SetSetting("connection_mode", "online");
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { SyncEngine.SyncAll(); });
                }
                else
                {
                    if (!dbconnect.CheckLocal())
                    {
                        MessageBox.Show(
                            "Mahalliy baza topilmadi yoki ochib bo'lmadi!\n\n" +
                            "Avval quyidagi buyruqni ishga tushiring:\n" +
                            "sqlcmd -S .\\SQLEXPRESS -E -i install_local_db.sql\n\n" +
                            "Yoki local_connection.cfg faylida to'g'ri ulanish manzilini ko'rsating.",
                            "Mahalliy baza xatosi",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Session.IsOnline     = false;
                    Session.ForceOffline = true;
                    PrintService.SetSetting("connection_mode", "offline");
                }
                _connectionTogglePanel.Invalidate();
                card.Invalidate();
                if (_lblConnStatus != null)
                {
                    _lblConnStatus.Text      = Session.IsOnline ? "Onlayn — serverga ulangan" : "Oflayn — mahalliy bazada";
                    _lblConnStatus.ForeColor = Session.IsOnline ? colorOn : TextM;
                }
            };
            card.Controls.Add(_connectionTogglePanel);

            card.Resize += (s, e) =>
            {
                _connectionTogglePanel.Location = new Point(card.Width - 70, (card.Height - H) / 2);
                if (_lblConnStatus != null) _lblConnStatus.Width = card.Width - 200;
            };

            return card;
        }

        // ── Nav / back bar factories ──────────────────────────────────────────
        Panel MakeNavCard(string title, string desc, string icon, Color accent, Point loc)
        {
            Panel card = new Panel { Width = 480, Height = 100, Location = loc, BackColor = BgCard, Cursor = Cursors.Hand };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10)) { e.Graphics.FillPath(new SolidBrush(BgCard), path); e.Graphics.DrawPath(new Pen(Border), path); }
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, 5, card.Height);
            };
            card.Controls.Add(new Label { Text = icon, Font = new Font("Segoe UI", 24), ForeColor = accent, Location = new Point(18, 14), AutoSize = true });
            card.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = TextD, Location = new Point(82, 22), AutoSize = true });
            card.Controls.Add(new Label { Text = desc, Font = new Font("Segoe UI", 8.5f), ForeColor = TextM, Location = new Point(82, 48), Width = 340, Height = 40, AutoSize = false });
            Color norm = BgCard, hov = Color.FromArgb(249, 249, 251);
            card.MouseEnter += (s, e) => { card.BackColor = hov; card.Refresh(); };
            card.MouseLeave += (s, e) => { card.BackColor = norm; card.Refresh(); };
            return card;
        }

        Panel MakeBackBar(string title, EventHandler backClick)
        {
            Panel bar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = BgCard };
            bar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Border), 0, bar.Height-1, bar.Width, bar.Height-1);
            Button btnBack = new Button { Text = "← Orqaga", Width = 110, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = TextM, Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand, Location = new Point(16, 8) };
            btnBack.FlatAppearance.BorderSize = 0; btnBack.Click += backClick; bar.Controls.Add(btnBack);
            if (!string.IsNullOrEmpty(title)) bar.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = TextD, AutoSize = true, Location = new Point(140, 14) });
            return bar;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, radius*2, radius*2, 180, 90);
            p.AddArc(r.Right-radius*2, r.Y, radius*2, radius*2, 270, 90);
            p.AddArc(r.Right-radius*2, r.Bottom-radius*2, radius*2, radius*2, 0, 90);
            p.AddArc(r.X, r.Bottom-radius*2, radius*2, radius*2, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
