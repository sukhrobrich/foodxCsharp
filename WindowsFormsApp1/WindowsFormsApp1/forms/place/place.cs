using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.place
{
    public partial class Place : UserControl
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color BgMain   = Color.FromArgb(248, 248, 250);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted= Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color AvatarBg = Color.FromArgb(240, 240, 245);

        private Panel  zonesScrollPanel;
        private Panel  tablesListPanel;
        private Label  lblSelectedZone;
        private Button btnFab;
        private Panel  zonesWrap;
        private Panel  tablesWrap;

        private int    selectedZoneId   = 0;
        private string selectedZoneName = "";

        public Place()
        {
            InitializeComponent();
            EnsureColumns();
            BuildUI();
            this.Load += (s, e) => this.BeginInvoke(new Action(() => { ReorderForm.EnsureOrderColumn("place_out"); LoadZones(); }));
        }

        private static void EnsureColumns()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                new SqlCommand(
                    @"IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('place_out') AND name='price')
                          ALTER TABLE place_out ADD price DECIMAL(18,2) NULL;
                      IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('place_out') AND name='price_type')
                          ALTER TABLE place_out ADD price_type NVARCHAR(3) NULL DEFAULT 'UZS'",
                    db.GetCon()).ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        // ── BUILD UI ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.BackColor = BgMain;

            // HEADER
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "Joylar",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(24, 18)
            });

            if (Session.IsAdmin)
            {
                Button btnReorder = new Button
                {
                    Text = "⇅ Tartib",
                    Height = 34, Width = 100,
                    Location = new Point(header.Width - 116, 15),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(243, 244, 246),
                    ForeColor = TextDark,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnReorder.FlatAppearance.BorderColor = Border;
                btnReorder.Click += (s, e) =>
                {
                    var items = ReorderForm.LoadItems("place_out");
                    using (var frm = new ReorderForm("Zonalar tartibi", items))
                    {
                        frm.ShowDialog();
                        if (frm.Saved) { ReorderForm.SaveOrder("place_out", frm.OrderedIds); LoadZones(); }
                    }
                };
                header.Controls.Add(btnReorder);
                header.Resize += (s, e) => btnReorder.Location = new Point(header.Width - 116, 15);
            }

            // LEFT: zones panel
            zonesWrap = new Panel { Dock = DockStyle.Fill, BackColor = CardBg };
            zonesWrap.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), zonesWrap.Width - 1, 0, zonesWrap.Width - 1, zonesWrap.Height);

            zonesScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = CardBg,
                Padding = new Padding(0, 0, 0, 72)
            };
            // FAB — add zone (added FIRST = lowest index = painted last = on top ✓)
            btnFab = new Button
            {
                Width = 52, Height = 52,
                BackColor = Success,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnFab.FlatAppearance.BorderSize = 0;
            btnFab.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(btnFab.ClientRectangle, 26))
                    e.Graphics.FillPath(new SolidBrush(Success), path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI", 24, FontStyle.Bold))
                    e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, btnFab.Width, btnFab.Height), sf);
            };
            btnFab.Click += (s, e) => ShowZoneForm(0, "", 0, true);
            zonesWrap.Controls.Add(btnFab);
            zonesWrap.Resize += (s, e) =>
                btnFab.Location = new Point((zonesWrap.Width - btnFab.Width) / 2, zonesWrap.Height - btnFab.Height - 16);

            // zonesScrollPanel added AFTER btnFab so FAB stays on top
            zonesWrap.Controls.Add(zonesScrollPanel);

            // RIGHT: tables panel (initially hidden)
            tablesWrap = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, Visible = false };

            Panel tablesHeader = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = CardBg };
            tablesHeader.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, tablesHeader.Height - 1, tablesHeader.Width, tablesHeader.Height - 1);

            lblSelectedZone = new Label
            {
                Text = "Zona tanlang",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(24, 16)
            };
            tablesHeader.Controls.Add(lblSelectedZone);

            Button btnAddTable = new Button
            {
                Text = "+ Joy qo'shish",
                Height = 36, Width = 150,
                FlatStyle = FlatStyle.Flat,
                BackColor = Success, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAddTable.FlatAppearance.BorderSize = 0;
            btnAddTable.Click += (s, e) =>
            {
                if (selectedZoneId == 0) { MessageBox.Show("Avval chapdan zona tanlang!"); return; }
                ShowAddTablesDialog();
            };
            tablesHeader.Controls.Add(btnAddTable);
            tablesHeader.Resize += (s, e) =>
                btnAddTable.Location = new Point(tablesHeader.Width - btnAddTable.Width - 16, 12);

            tablesListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgMain,
                Padding = new Padding(20, 12, 20, 12)
            };
            tablesListPanel.Resize += (s, e) => ResizeTableCards();
            ShowPlaceholder(tablesListPanel, "Chapdan zona tanlang");

            // tablesWrap: Fill first, Top last ✓
            tablesWrap.Controls.Add(tablesListPanel);
            tablesWrap.Controls.Add(tablesHeader);

            // Main: Fill first (tablesWrap hidden), Left second, Top last ✓
            this.Controls.Add(tablesWrap);
            this.Controls.Add(zonesWrap);
            this.Controls.Add(header);
        }

        // ── ZONE FORM (qo'shish / tahrirlash) ────────────────────────────────
        private void ShowZoneForm(int id, string name, decimal price = 0, bool svcFee = true, string priceType = "UZS")
        {
            using (Form dlg = MakeDlg(id == 0 ? "Yangi zona qo'shish" : "Zonani tahrirlash", 380, 330))
            {
                TextBox txtName  = DlgField(dlg, "Zona nomi:", name, 16);
                TextBox txtPrice = DlgField(dlg, "Joy narxi (0 = bepul):",
                    price > 0 ? price.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) : "0", 88);
                txtPrice.KeyPress += (s, e) =>
                { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.') e.Handled = true; };

                // Narx turi: UZS yoki %
                var rbUzs = new RadioButton
                {
                    Text = "So'm (UZS)", Checked = priceType != "%",
                    Location = new Point(16, 148), AutoSize = true,
                    Font = new Font("Segoe UI", 10), ForeColor = TextDark
                };
                var rbPct = new RadioButton
                {
                    Text = "Foiz (%)", Checked = priceType == "%",
                    Location = new Point(160, 148), AutoSize = true,
                    Font = new Font("Segoe UI", 10), ForeColor = TextDark
                };
                dlg.Controls.Add(rbUzs);
                dlg.Controls.Add(rbPct);

                dlg.Controls.Add(new Label
                {
                    Text = "Xizmat haqqi:",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = TextMuted,
                    Location = new Point(16, 184),
                    AutoSize = true
                });
                CheckBox chkSvc = new CheckBox
                {
                    Text = "Ushbu zonaga xizmat haqqi qo'llanadi",
                    Checked = svcFee,
                    Location = new Point(16, 202),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10),
                    ForeColor = TextDark
                };
                dlg.Controls.Add(chkSvc);

                Button btnSave = DlgBtn(dlg, id == 0 ? "Qo'shish" : "Saqlash", Success, Color.White, 16, 248);
                btnSave.Click += (s, e) =>
                {
                    string n = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(n)) { MessageBox.Show("Zona nomini kiriting!"); return; }
                    string raw = txtPrice.Text.Replace(',', '.');
                    if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal pr)) pr = 0;
                    if (pr < 0) pr = 0;
                    if (rbPct.Checked && pr > 100) { MessageBox.Show("Foiz 0–100 oralig'ida bo'lishi kerak!"); return; }
                    string sf  = chkSvc.Checked ? "YES" : "NO";
                    string pt  = rbPct.Checked ? "%" : "UZS";
                    try
                    {
                        dbconnect db = new dbconnect();
                        db.OpenCon();
                        if (id == 0)
                        {
                            int cat = EnsureDefaultCategory(db);
                            using (SqlCommand cmd = new SqlCommand(
                                "INSERT INTO place_out(place_category_id,name,place_count,serviceFee,price,price_type) VALUES(@c,@n,0,@sf,@pr,@pt)",
                                db.GetCon()))
                            {
                                cmd.Parameters.AddWithValue("@c",  cat);
                                cmd.Parameters.AddWithValue("@n",  n);
                                cmd.Parameters.AddWithValue("@sf", sf);
                                cmd.Parameters.AddWithValue("@pr", pr);
                                cmd.Parameters.AddWithValue("@pt", pt);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            using (SqlCommand cmd = new SqlCommand(
                                "UPDATE place_out SET name=@n, serviceFee=@sf, price=@pr, price_type=@pt WHERE id=@id",
                                db.GetCon()))
                            {
                                cmd.Parameters.AddWithValue("@n",  n);
                                cmd.Parameters.AddWithValue("@sf", sf);
                                cmd.Parameters.AddWithValue("@pr", pr);
                                cmd.Parameters.AddWithValue("@pt", pt);
                                cmd.Parameters.AddWithValue("@id", id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        db.CloseCon();
                        if (Session.IsOnline && Session.TenantId > 0)
                            System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
                        dlg.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                };

                if (id > 0)
                {
                    Button btnDel = DlgBtn(dlg, "O'chirish", Color.FromArgb(254, 242, 242), Danger, 198, 224);
                    btnDel.Click += (s, e) =>
                    {
                        if (MessageBox.Show("Bu zonani va barcha stollarni o'chirasizmi?",
                            "O'chirish", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                        try
                        {
                            var db = new dbconnect(); db.OpenCon();
                            // Central ID larni olish (delete uchun)
                            int? centralPlaceOutId = null;
                            using (var chk = new SqlCommand("SELECT central_id FROM place_out WHERE id=@id", db.GetCon()))
                            { chk.Parameters.AddWithValue("@id", id); object v = chk.ExecuteScalar(); if (v != null && v != DBNull.Value) centralPlaceOutId = Convert.ToInt32(v); }
                            using (SqlCommand c = new SqlCommand("DELETE FROM place_in WHERE place_out_id=@id", db.GetCon()))
                            { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                            using (SqlCommand c = new SqlCommand("DELETE FROM place_out WHERE id=@id", db.GetCon()))
                            { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                            db.CloseCon();
                            // Centraldan ham o'chirish
                            if (centralPlaceOutId.HasValue && Session.IsOnline && Session.TenantId > 0)
                            {
                                try
                                {
                                    using (var central = dbconnect.OpenCentralForSync(Session.TenantId))
                                    {
                                        using (var c = new SqlCommand("DELETE FROM place_in WHERE place_out_id=@id", central))
                                        { c.Parameters.AddWithValue("@id", centralPlaceOutId.Value); c.ExecuteNonQuery(); }
                                        using (var c = new SqlCommand("DELETE FROM place_out WHERE id=@id", central))
                                        { c.Parameters.AddWithValue("@id", centralPlaceOutId.Value); c.ExecuteNonQuery(); }
                                    }
                                }
                                catch { }
                            }
                            dlg.Tag = "deleted";
                            dlg.DialogResult = DialogResult.OK;
                        }
                        catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                    };
                }

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    if (dlg.Tag?.ToString() == "deleted" && id == selectedZoneId)
                    {
                        selectedZoneId = 0; selectedZoneName = "";
                        lblSelectedZone.Text = "Zona tanlang";
                        ShowPlaceholder(tablesListPanel, "Chapdan zona tanlang");
                    }
                    LoadZones();
                }
            }
        }

        // ── ADD TABLES DIALOG ────────────────────────────────────────────────
        private void ShowAddTablesDialog()
        {
            using (Form dlg = MakeDlg("Joy qo'shish — " + selectedZoneName, 380, 280))
            {
                TextBox txtName  = DlgField(dlg, "Joy nomi  (masalan: stol, tapchan, karavot):", "", 16);
                TextBox txtCount = DlgField(dlg, "Soni  (shu nomdan nechtasi qo'shilsin):", "1", 96);
                txtCount.KeyPress += (s, e) =>
                { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

                Button btnSave = DlgBtn(dlg, "Qo'shish", Success, Color.White, 16, 188);
                btnSave.Width = dlg.ClientSize.Width - 32;
                btnSave.Click += (s, e) =>
                {
                    string n = txtName.Text.Trim();
                    if (string.IsNullOrEmpty(n)) { MessageBox.Show("Joy nomini kiriting!"); return; }
                    if (!int.TryParse(txtCount.Text, out int cnt) || cnt < 1 || cnt > 500)
                    { MessageBox.Show("Soni 1–500 orasida bo'lishi kerak!"); return; }
                    try
                    {
                        dbconnect db = new dbconnect();
                        db.OpenCon();
                        for (int i = 1; i <= cnt; i++)
                        {
                            string roomName = cnt == 1 ? n : $"{i} - {n}";
                            using (SqlCommand cmd = new SqlCommand(
                                "INSERT INTO place_in(place_out_id,room_name,empty) VALUES(@z,@r,'YES')",
                                db.GetCon()))
                            { cmd.Parameters.AddWithValue("@z", selectedZoneId); cmd.Parameters.AddWithValue("@r", roomName); cmd.ExecuteNonQuery(); }
                        }
                        db.CloseCon();
                        if (Session.IsOnline && Session.TenantId > 0)
                            System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
                        dlg.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                };

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadTables(selectedZoneId);
                    LoadZones();
                }
            }
        }

        // ── EDIT TABLE DIALOG ─────────────────────────────────────────────────
        private void ShowEditTableDialog(int id, string currentName)
        {
            using (Form dlg = MakeDlg("Joyni tahrirlash", 360, 195))
            {
                TextBox txt = DlgField(dlg, "Joy nomi:", currentName, 16);

                Button btnSave = DlgBtn(dlg, "Saqlash", Success, Color.White, 16, 110);
                btnSave.Width = dlg.ClientSize.Width - 32;
                btnSave.Click += (s, e) =>
                {
                    string n = txt.Text.Trim();
                    if (string.IsNullOrEmpty(n)) { MessageBox.Show("Joy nomini kiriting!"); return; }
                    try
                    {
                        dbconnect db = new dbconnect();
                        db.OpenCon();
                        using (SqlCommand cmd = new SqlCommand("UPDATE place_in SET room_name=@n WHERE id=@id", db.GetCon()))
                        { cmd.Parameters.AddWithValue("@n", n); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                        db.CloseCon();
                        // Centralga ham yangilash
                        if (Session.IsOnline && Session.TenantId > 0)
                            System.Threading.Tasks.Task.Run(() => SyncEngine.SyncAll());
                        dlg.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
                };

                if (dlg.ShowDialog(this) == DialogResult.OK)
                    LoadTables(selectedZoneId);
            }
        }

        // ── DATA ─────────────────────────────────────────────────────────────
        private void LoadZones()
        {
            zonesScrollPanel.SuspendLayout();
            zonesScrollPanel.Controls.Clear();
            try
            {
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    @"SELECT po.id, po.name,
                             (SELECT COUNT(*) FROM place_in WHERE place_out_id=po.id) AS seat_count,
                             ISNULL(po.price, 0)          AS price,
                             ISNULL(po.price_type,'UZS')  AS price_type,
                             ISNULL(po.serviceFee,'YES')   AS svc_fee
                      FROM place_out po
                      JOIN place_category pc ON pc.id = po.place_category_id
                      ORDER BY ISNULL(po.sort_order,9999), pc.name, po.name", db.GetCon()))
                    da.Fill(dt);

                int y = 0;
                foreach (DataRow row in dt.Rows)
                {
                    int zid       = Convert.ToInt32(row["id"]);
                    string zname  = row["name"].ToString();
                    int sc        = Convert.ToInt32(row["seat_count"]);
                    decimal zp    = Convert.ToDecimal(row["price"]);
                    string zpt    = row["price_type"].ToString();
                    bool zsvc     = row["svc_fee"].ToString().ToUpper() == "YES";
                    Panel card    = CreateZoneCard(zid, zname, sc, zp, zsvc, zpt);
                    card.Location = new Point(0, y);
                    card.Width = zonesScrollPanel.ClientSize.Width;
                    zonesScrollPanel.Controls.Add(card);
                    y += card.Height;
                }

                if (dt.Rows.Count == 0)
                    zonesScrollPanel.Controls.Add(new Label
                    {
                        Text = "Hali zona qo'shilmagan",
                        Font = new Font("Segoe UI", 10),
                        ForeColor = TextMuted,
                        Location = new Point(16, 24),
                        AutoSize = true
                    });
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { zonesScrollPanel.ResumeLayout(); }
        }

        private void LoadTables(int zoneId)
        {
            tablesListPanel.SuspendLayout();
            tablesListPanel.Controls.Clear();
            try
            {
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    @"SELECT pi.id, pi.room_name, pi.empty,
                             (SELECT COUNT(*) FROM [order] WHERE place_id=pi.id AND paid='NO') AS order_count
                      FROM place_in pi WHERE pi.place_out_id=@zid
                      ORDER BY TRY_CAST(SUBSTRING(pi.room_name,1,PATINDEX('%[^0-9]%',pi.room_name+'x')-1) AS INT), pi.room_name", db.GetCon()))
                {
                    da.SelectCommand.Parameters.AddWithValue("@zid", zoneId);
                    da.Fill(dt);
                }

                int y = 0;
                foreach (DataRow row in dt.Rows)
                {
                    Panel card = CreateTableCard(
                        Convert.ToInt32(row["id"]),
                        row["room_name"].ToString(),
                        row["empty"].ToString(),
                        Convert.ToInt32(row["order_count"]));
                    card.Location = new Point(0, y);
                    card.Width = Math.Max(100, tablesListPanel.ClientSize.Width - 40);
                    tablesListPanel.Controls.Add(card);
                    y += card.Height + 8;
                }

                if (dt.Rows.Count == 0)
                    ShowPlaceholder(tablesListPanel, "Bu zonada joy yo'q.\n\"+ Joy qo'shish\" tugmasini bosing.");
            }
            catch (Exception ex) { MessageBox.Show("Stollarni yuklashda xatolik: " + ex.Message); }
            finally { tablesListPanel.ResumeLayout(); }
        }

        // ── CARD BUILDERS ─────────────────────────────────────────────────────
        private Panel CreateZoneCard(int id, string name, int seatCount, decimal price, bool svcFee, string priceType = "UZS")
        {
            bool sel = id == selectedZoneId;
            Panel card = new Panel { Height = 80, BackColor = sel ? GoldBg : CardBg, Cursor = Cursors.Hand };
            card.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(Border), 0, card.Height - 1, card.Width, card.Height - 1);
                if (sel) e.Graphics.FillRectangle(new SolidBrush(Gold), 0, 0, 3, card.Height);
            };

            Panel icon = new Panel { Width = 36, Height = 36, Location = new Point(14, 22), BackColor = Color.Transparent };
            icon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(icon.ClientRectangle, 8))
                    e.Graphics.FillPath(new SolidBrush(AvatarBg), path);
                DrawTableIcon(e.Graphics, new Rectangle(3, 3, 30, 30), sel ? Gold : TextMuted);
            };
            card.Controls.Add(icon);

            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 10, sel ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = sel ? Gold : TextDark,
                Location = new Point(58, 12), AutoSize = true
            };
            card.Controls.Add(lblName);

            Label lblCount = new Label
            {
                Text = $"Joylar: {seatCount}",
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted,
                Location = new Point(58, 34), AutoSize = true
            };
            card.Controls.Add(lblCount);

            string priceText = price > 0
                ? (priceType == "%" ? $"+{price:G29}%" : $"+{price:N0} so'm")
                : "Bepul";
            string svcText   = svcFee ? "✓ xizmat" : "✗ xizmat yo'q";
            Label lblInfo = new Label
            {
                Text = $"{priceText}  |  {svcText}",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = price > 0 ? Gold : TextMuted,
                Location = new Point(58, 54), AutoSize = true
            };
            card.Controls.Add(lblInfo);

            Button btnEdit = IconBtn("✏", TextMuted);
            Button btnDel  = IconBtn("🗑", Danger);

            btnEdit.Click += (s, e) => ShowZoneForm(id, name, price, svcFee, priceType);
            btnDel.Click  += (s, e) => DeleteZone(id, name);

            card.Controls.Add(btnDel);
            card.Controls.Add(btnEdit);

            card.Resize += (s, e) =>
            {
                int x = card.Width - 8;
                btnDel.Location  = new Point(x - 30, 25);
                btnEdit.Location = new Point(x - 62, 25);
            };

            EventHandler onSelect = (s, e) =>
            {
                selectedZoneId = id;
                selectedZoneName = name;
                lblSelectedZone.Text = name;
                if (!tablesWrap.Visible)
                {
                    zonesWrap.Dock = DockStyle.Left;
                    zonesWrap.Width = 240;
                    tablesWrap.Visible = true;
                }
                LoadZones();
                LoadTables(id);
            };
            card.Click     += onSelect;
            icon.Click     += onSelect;
            lblName.Click  += onSelect;
            lblCount.Click += onSelect;
            lblInfo.Click  += onSelect;

            Color normalBg = sel ? GoldBg : CardBg;
            Color hoverBg  = sel ? GoldBg : Color.FromArgb(250, 250, 252);
            card.MouseEnter += (s, e) => card.BackColor = hoverBg;
            card.MouseLeave += (s, e) => card.BackColor = normalBg;

            return card;
        }

        private void DeleteZone(int id, string name)
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                int unpaid = 0;
                using (SqlCommand c = new SqlCommand(
                    "SELECT COUNT(*) FROM [order] o JOIN place_in pi ON pi.id=o.place_id WHERE pi.place_out_id=@id AND o.paid='NO'",
                    db.GetCon()))
                { c.Parameters.AddWithValue("@id", id); unpaid = (int)c.ExecuteScalar(); }
                db.CloseCon();

                if (unpaid > 0)
                {
                    MessageBox.Show($"Bu zonada {unpaid} ta ochiq zakaz bor.\nAvval zakaslani yoping!",
                        "O'chirib bo'lmaydi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); return; }

            if (MessageBox.Show($"'{name}' zonasi va barcha joylarni o'chirasizmi?",
                "O'chirish", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                // Cascade: order_food → order → place_in → place_out
                using (SqlCommand c = new SqlCommand(
                    "DELETE FROM order_food WHERE order_id IN (SELECT o.id FROM [order] o JOIN place_in pi ON pi.id=o.place_id WHERE pi.place_out_id=@id)",
                    db.GetCon()))
                { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                using (SqlCommand c = new SqlCommand(
                    "DELETE FROM [order] WHERE place_id IN (SELECT id FROM place_in WHERE place_out_id=@id)",
                    db.GetCon()))
                { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                using (SqlCommand c = new SqlCommand("DELETE FROM place_in WHERE place_out_id=@id", db.GetCon()))
                { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                using (SqlCommand c = new SqlCommand("DELETE FROM place_out WHERE id=@id", db.GetCon()))
                { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                db.CloseCon();

                if (id == selectedZoneId)
                {
                    selectedZoneId = 0; selectedZoneName = "";
                    lblSelectedZone.Text = "Zona tanlang";
                    ShowPlaceholder(tablesListPanel, "Chapdan zona tanlang");
                }
                LoadZones();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private Panel CreateTableCard(int id, string name, string empty, int orderCount)
        {
            bool isEmpty = empty?.ToUpper() == "YES";
            Color statusColor = isEmpty ? Success : Danger;
            string statusText = isEmpty ? "Bo'sh" : $"{orderCount} ta buyurtma";

            Panel card = new Panel { Height = 64, BackColor = CardBg };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(CardBg), path);
                    e.Graphics.DrawPath(new Pen(Border), path);
                }
            };

            Panel icon = new Panel { Width = 40, Height = 40, Location = new Point(12, 12), BackColor = Color.Transparent };
            icon.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(icon.ClientRectangle, 8))
                    e.Graphics.FillPath(new SolidBrush(AvatarBg), path);
                DrawTableIcon(e.Graphics, new Rectangle(5, 5, 30, 30), TextMuted);
            };
            card.Controls.Add(icon);

            card.Controls.Add(new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(62, 10), AutoSize = true
            });
            card.Controls.Add(new Label
            {
                Text = statusText,
                Font = new Font("Segoe UI", 8),
                ForeColor = statusColor,
                Location = new Point(62, 34), AutoSize = true
            });

            // Action buttons — positioned by Resize
            Button btnEdit = IconBtn("✏", TextMuted);
            Button btnDel  = IconBtn("🗑", Danger);

            btnEdit.Click += (s, e) => ShowEditTableDialog(id, name);
            btnDel.Click  += (s, e) =>
            {
                try
                {
                    dbconnect db = new dbconnect();
                    db.OpenCon();
                    int unpaid = 0;
                    using (SqlCommand c = new SqlCommand(
                        "SELECT COUNT(*) FROM [order] WHERE place_id=@id AND paid='NO'", db.GetCon()))
                    { c.Parameters.AddWithValue("@id", id); unpaid = (int)c.ExecuteScalar(); }
                    db.CloseCon();

                    if (unpaid > 0)
                    {
                        MessageBox.Show($"Bu joyda {unpaid} ta ochiq zakaz bor.\nAvval zakaslani yoping!",
                            "O'chirib bo'lmaydi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); return; }

                if (MessageBox.Show($"'{name}' joyini o'chirasizmi?",
                    "O'chirish", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    var db = new dbconnect(); db.OpenCon();
                    // Central ID ni olib qo'yamiz
                    int? centralPlaceInId = null;
                    using (var chk = new SqlCommand("SELECT central_id FROM place_in WHERE id=@id", db.GetCon()))
                    { chk.Parameters.AddWithValue("@id", id); object v = chk.ExecuteScalar(); if (v != null && v != DBNull.Value) centralPlaceInId = Convert.ToInt32(v); }
                    using (SqlCommand c = new SqlCommand(
                        "DELETE FROM order_food WHERE order_id IN (SELECT id FROM [order] WHERE place_id=@id)",
                        db.GetCon()))
                    { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                    using (SqlCommand c = new SqlCommand(
                        "DELETE FROM [order] WHERE place_id=@id", db.GetCon()))
                    { c.Parameters.AddWithValue("@id", id); c.ExecuteNonQuery(); }
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM place_in WHERE id=@id", db.GetCon()))
                    { cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
                    db.CloseCon();
                    // Centraldan ham o'chirish
                    if (centralPlaceInId.HasValue && Session.IsOnline && Session.TenantId > 0)
                    {
                        try
                        {
                            using (var central = dbconnect.OpenCentralForSync(Session.TenantId))
                            using (var c = new SqlCommand("DELETE FROM place_in WHERE id=@id", central))
                            { c.Parameters.AddWithValue("@id", centralPlaceInId.Value); c.ExecuteNonQuery(); }
                        }
                        catch { }
                    }
                    LoadTables(selectedZoneId);
                    LoadZones();
                }
                catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            };

            card.Controls.Add(btnDel);
            card.Controls.Add(btnEdit);

            card.Resize += (s, e) =>
            {
                int x = card.Width - 8;
                btnDel.Location  = new Point(x - 30, 17);
                btnEdit.Location = new Point(x - 62, 17);
            };

            return card;
        }

        // ── HELPERS ──────────────────────────────────────────────────────────
        private Button IconBtn(string icon, Color color)
        {
            Button b = new Button
            {
                Text = icon,
                Width = 30, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand,
                Padding = new Padding(0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
            return b;
        }

        private void ResizeTableCards()
        {
            int y = 0;
            foreach (Control c in tablesListPanel.Controls)
            {
                if (!(c is Panel)) continue;
                c.Width = Math.Max(100, tablesListPanel.ClientSize.Width - 40);
                c.Top = y;
                y += c.Height + 8;
            }
        }

        private void ShowPlaceholder(Panel parent, string text)
        {
            parent.Controls.Clear();
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 12),
                ForeColor = TextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        // ── DIALOG FACTORY ───────────────────────────────────────────────────
        private Form MakeDlg(string title, int w, int h)
        {
            return new Form
            {
                Text = title, Width = w, Height = h,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = CardBg
            };
        }

        private TextBox DlgField(Form dlg, string labelText, string value, int y)
        {
            dlg.Controls.Add(new Label
            {
                Text = labelText,
                Location = new Point(16, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted
            });
            TextBox tb = new TextBox
            {
                Location = new Point(16, y + 22),
                Width = dlg.ClientSize.Width - 32,
                Font = new Font("Segoe UI", 11),
                Text = value,
                BorderStyle = BorderStyle.FixedSingle
            };
            dlg.Controls.Add(tb);
            return tb;
        }

        private Button DlgBtn(Form dlg, string text, Color bg, Color fg, int x, int y)
        {
            Button b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = 150, Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg, ForeColor = fg,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(b);
            return b;
        }

        private void DrawTableIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, 1.5f))
            {
                int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                int tw = r.Width - 6, th = r.Height / 3;
                g.DrawRectangle(pen, cx - tw / 2, cy - th / 2, tw, th);
                g.DrawLine(pen, cx - tw / 2 + 4, cy + th / 2, cx - tw / 2 + 4, r.Bottom - 1);
                g.DrawLine(pen, cx + tw / 2 - 4, cy + th / 2, cx + tw / 2 - 4, r.Bottom - 1);
                g.DrawLine(pen, cx - tw / 4,     cy - th / 2, cx - tw / 4,     r.Y + 1);
                g.DrawLine(pen, cx + tw / 4,     cy - th / 2, cx + tw / 4,     r.Y + 1);
            }
        }

        private int EnsureDefaultCategory(dbconnect db)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 id FROM place_category ORDER BY id", db.GetCon()))
            {
                object result = cmd.ExecuteScalar();
                if (result != null) return Convert.ToInt32(result);
            }
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO place_category(name) VALUES('Umumiy'); SELECT SCOPE_IDENTITY();", db.GetCon()))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            p.AddArc(r.X,                  r.Y,                   radius * 2, radius * 2, 180, 90);
            p.AddArc(r.Right - radius * 2, r.Y,                   radius * 2, radius * 2, 270, 90);
            p.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2,   0, 90);
            p.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2,  90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
