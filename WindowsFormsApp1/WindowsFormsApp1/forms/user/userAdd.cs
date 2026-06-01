using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.user
{
    public partial class UserAdd : UserControl
    {
        private static readonly Color Gold      = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldLight = Color.FromArgb(255, 248, 230);
        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Success   = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger    = Color.FromArgb(220, 38, 38);

        // ── Xodimlar tab ───────────────────────────────────────────────────
        private Panel   usersListPanel;
        private Panel   userEditPanel;
        private Button  userFab;
        private TextBox txtName, txtLogin, txtPin, txtPhone, txtAppPassword;
        private ComboBox cmbRole;
        private Label   lblUserFormTitle;
        private Button  btnUserSave, btnUserDelete;
        private int     editingUserId = -1;

        // ── Lavozimlar tab ─────────────────────────────────────────────────
        private Panel   rolesListPanel;
        private Panel   roleEditPanel;
        private Button  roleFab;
        private TextBox txtRoleName;
        private string  selectedRoleType  = "ofitsiant";
        private string  selectedRoleColor = "#16A34A";
        private Panel   colorPickerBtn;
        private Label   lblRoleFormTitle;
        private Button  btnRoleSave, btnRoleDelete;
        private Panel   rbAdmin, rbKassir, rbOfitsiant;
        private int     editingRoleId = -1;

        // ── Tab state ──────────────────────────────────────────────────────
        private Panel contentXodimlar, contentLavozimlar;
        private Button btnTabUsers, btnTabRoles;

        public UserAdd()
        {
            InitializeComponent();
            BuildUI();
            this.Load += (s, e) => { ReorderForm.EnsureOrderColumn("user"); RunMigration(); LoadUsers(); LoadRolesCombo(); SwitchTab(0); };
        }

        // ── MIGRATION ─────────────────────────────────────────────────────
        private void RunMigration()
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (SqlCommand cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME='user_category' AND COLUMN_NAME='role_type')
                    BEGIN
                        ALTER TABLE user_category ADD role_type NVARCHAR(20);
                        UPDATE user_category SET role_type = LOWER(name);
                        UPDATE user_category SET role_type = 'ofitsiant'
                            WHERE role_type NOT IN ('admin','kassir','ofitsiant') OR role_type IS NULL;
                    END;
                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME='user' AND COLUMN_NAME='app_password')
                        ALTER TABLE [user] ADD app_password NVARCHAR(64) NULL;", db.GetCon()))
                    cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
            userCategoryAdd.EnsureColorColumn();
        }

        // ── BUILD UI ──────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.BackColor = BgPage;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = BgCard };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            header.Controls.Add(new Label
            {
                Text = "Xodimlar",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(24, 18)
            });

            Button btnReorder = new Button
            {
                Text = "⇅  Tartib", Width = 100, Height = 34,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246),
                ForeColor = Color.FromArgb(107, 114, 128),
                Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand,
                Visible = Session.IsAdmin
            };
            btnReorder.FlatAppearance.BorderSize = 1;
            btnReorder.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235);
            btnReorder.Click += (s, e) =>
            {
                var items = ReorderForm.LoadItems("user");
                using (var frm = new ReorderForm("Xodimlar tartibi", items))
                {
                    frm.ShowDialog();
                    if (frm.Saved) { ReorderForm.SaveOrder("user", frm.OrderedIds); LoadUsers(); }
                }
            };
            header.Controls.Add(btnReorder);
            header.Resize += (s, e) => btnReorder.Location = new Point(header.Width - 116, 15);

            Panel tabBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = BgCard };
            tabBar.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);

            btnTabUsers = MakeTabBtn("Xodimlar", 0);
            btnTabRoles = MakeTabBtn("Lavozimlar", 130);
            tabBar.Controls.Add(btnTabUsers);
            tabBar.Controls.Add(btnTabRoles);

            contentXodimlar   = BuildXodimlarContent();
            contentLavozimlar = BuildLavozimlarContent();
            contentXodimlar.Dock   = DockStyle.Fill;
            contentLavozimlar.Dock = DockStyle.Fill;

            Panel body = new Panel { Dock = DockStyle.Fill };
            body.Controls.Add(contentLavozimlar);
            body.Controls.Add(contentXodimlar);

            this.Controls.Add(body);
            this.Controls.Add(tabBar);
            this.Controls.Add(header);
        }

        private Button MakeTabBtn(string text, int x)
        {
            Button b = new Button
            {
                Text = text,
                Width = 120, Height = 46,
                Location = new Point(x, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            int tabIdx = (x == 0) ? 0 : 1;
            b.Click += (s, e) => SwitchTab(tabIdx);
            return b;
        }

        private void SwitchTab(int tab)
        {
            contentXodimlar.Visible   = (tab == 0);
            contentLavozimlar.Visible = (tab == 1);

            btnTabUsers.ForeColor = tab == 0 ? Gold : TextMuted;
            btnTabUsers.Font      = new Font("Segoe UI", 10, tab == 0 ? FontStyle.Bold : FontStyle.Regular);
            btnTabRoles.ForeColor = tab == 1 ? Gold : TextMuted;
            btnTabRoles.Font      = new Font("Segoe UI", 10, tab == 1 ? FontStyle.Bold : FontStyle.Regular);

            if (tab == 1) LoadRolesList();
        }

        // ── XODIMLAR CONTENT ──────────────────────────────────────────────
        private Panel BuildXodimlarContent()
        {
            Panel root = new Panel { BackColor = BgPage };

            userEditPanel = new Panel { Width = 380, Dock = DockStyle.Right, BackColor = BgCard, Visible = false };
            userEditPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, userEditPanel.Height);
            BuildUserEditPanel();

            usersListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(20, 12, 20, 12)
            };
            usersListPanel.Resize += (s, e) => ResizeUserCards();

            userFab = MakeFab();
            userFab.Click += (s, e) => { ClearUserForm(); userEditPanel.Visible = true; txtName.Focus(); };
            root.Controls.Add(userFab);

            root.Controls.Add(usersListPanel);
            root.Controls.Add(userEditPanel);

            root.Resize += (s, e) => UpdateUserFabPos();
            userEditPanel.VisibleChanged += (s, e) => UpdateUserFabPos();

            return root;
        }

        private void BuildUserEditPanel()
        {
            Button btnClose = new Button
            {
                Text = "×", Width = 30, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => userEditPanel.Visible = false;
            userEditPanel.Controls.Add(btnClose);
            userEditPanel.Resize += (s, e) => btnClose.Location = new Point(userEditPanel.Width - 44, 18);

            int y = 24;
            lblUserFormTitle = new Label
            {
                Text = "Yangi xodim",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            userEditPanel.Controls.Add(lblUserFormTitle);
            y += 40;

            userEditPanel.Controls.Add(new Panel { Left = 24, Top = y, Width = 332, Height = 2, BackColor = Gold });
            y += 14;

            txtName        = AddFieldTo(userEditPanel, "To'liq ism",                      ref y, false);
            txtLogin       = AddFieldTo(userEditPanel, "Login",                          ref y, false);
            txtPin         = AddFieldTo(userEditPanel, "PIN kod (4 ta raqam, WinForms)", ref y, true);
            txtPhone       = AddFieldTo(userEditPanel, "Telefon raqam",                  ref y, false);
            txtAppPassword = AddFieldTo(userEditPanel, "Mobil/Web paroli (ixtiyoriy)",   ref y, true);

            // Mobil parol haqida izoh
            userEditPanel.Controls.Add(new Label
            {
                Text = "📱 Mobil parol — ofitsiant telefonidan kira olishi uchun",
                Font = new Font("Segoe UI", 8), ForeColor = TextMuted,
                Location = new Point(24, y - 28), AutoSize = true
            });

            userEditPanel.Controls.Add(new Label
            {
                Text = "Lavozim",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 22;

            cmbRole = new ComboBox
            {
                Location = new Point(24, y), Width = 332, Height = 36,
                Font = new Font("Segoe UI", 10), FlatStyle = FlatStyle.Flat,
                BackColor = BgPage, ForeColor = TextDark,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            userEditPanel.Controls.Add(cmbRole);
            y += 46;

            btnUserSave = new Button
            {
                Text = "Saqlash", Location = new Point(24, y),
                Width = 156, Height = 40,
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnUserSave.FlatAppearance.BorderSize = 0;
            btnUserSave.Click += SaveUser;
            userEditPanel.Controls.Add(btnUserSave);

            btnUserDelete = new Button
            {
                Text = "O'chirish", Location = new Point(188, y),
                Width = 168, Height = 40,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(254, 242, 242),
                ForeColor = Danger, Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand, Visible = false
            };
            btnUserDelete.FlatAppearance.BorderSize = 1;
            btnUserDelete.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnUserDelete.Click += DeleteUser;
            userEditPanel.Controls.Add(btnUserDelete);
        }

        private void UpdateUserFabPos()
        {
            int rightOffset = (userEditPanel.Visible ? userEditPanel.Width : 0) + 20;
            userFab.Location = new Point(
                contentXodimlar.Width - rightOffset - userFab.Width,
                contentXodimlar.Height - userFab.Height - 20);
        }

        // ── LAVOZIMLAR CONTENT ────────────────────────────────────────────
        private Panel BuildLavozimlarContent()
        {
            Panel root = new Panel { BackColor = BgPage };

            roleEditPanel = new Panel { Width = 380, Dock = DockStyle.Right, BackColor = BgCard, Visible = false };
            roleEditPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, roleEditPanel.Height);
            BuildRoleEditPanel();

            rolesListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(20, 12, 20, 12)
            };
            rolesListPanel.Resize += (s, e) => ResizeRoleCards();

            roleFab = MakeFab();
            roleFab.Click += (s, e) => { ClearRoleForm(); roleEditPanel.Visible = true; txtRoleName.Focus(); };
            root.Controls.Add(roleFab);

            root.Controls.Add(rolesListPanel);
            root.Controls.Add(roleEditPanel);

            root.Resize += (s, e) => UpdateRoleFabPos();
            roleEditPanel.VisibleChanged += (s, e) => UpdateRoleFabPos();

            return root;
        }

        private void BuildRoleEditPanel()
        {
            Button btnClose = new Button
            {
                Text = "×", Width = 30, Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => roleEditPanel.Visible = false;
            roleEditPanel.Controls.Add(btnClose);
            roleEditPanel.Resize += (s, e) => btnClose.Location = new Point(roleEditPanel.Width - 44, 18);

            int y = 24;
            lblRoleFormTitle = new Label
            {
                Text = "Yangi lavozim",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(24, y),
                AutoSize = true
            };
            roleEditPanel.Controls.Add(lblRoleFormTitle);
            y += 40;

            roleEditPanel.Controls.Add(new Panel { Left = 24, Top = y, Width = 332, Height = 2, BackColor = Gold });
            y += 14;

            txtRoleName = AddFieldTo(roleEditPanel, "Lavozim nomi", ref y, false);

            // Permission level pills
            roleEditPanel.Controls.Add(new Label
            {
                Text = "Ruxsat darajasi",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 26;

            rbAdmin     = MakeRolePill("Admin",     "admin",     Danger,   new Point(24,  y));
            rbKassir    = MakeRolePill("Kassir",    "kassir",    Gold,     new Point(130, y));
            rbOfitsiant = MakeRolePill("Ofitsiant", "ofitsiant", Success,  new Point(236, y));
            roleEditPanel.Controls.Add(rbAdmin);
            roleEditPanel.Controls.Add(rbKassir);
            roleEditPanel.Controls.Add(rbOfitsiant);
            y += 50;

            roleEditPanel.Controls.Add(new Label
            {
                Text = "Admin — barcha imkoniyatlar\nKassir — zakaslar va hisobotlar\nOfitsiant — faqat zakaslar",
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 56;

            // Color picker
            roleEditPanel.Controls.Add(new Label
            {
                Text = "Karta rangi",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted,
                Location = new Point(24, y),
                AutoSize = true
            });
            y += 26;

            colorPickerBtn = new Panel
            {
                Width = 48,
                Height = 36,
                Location = new Point(24, y),
                BackColor = ParseColor(selectedRoleColor),
                Cursor = Cursors.Hand,
                BorderStyle = BorderStyle.FixedSingle
            };
            var colorHintLbl = new Label
            {
                Text = "Bosing va rang tanlang",
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted,
                Location = new Point(82, y + 10),
                AutoSize = true
            };
            colorPickerBtn.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = colorPickerBtn.BackColor })
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        selectedRoleColor = ToHex(dlg.Color);
                        colorPickerBtn.BackColor = dlg.Color;
                    }
                }
            };
            roleEditPanel.Controls.Add(colorPickerBtn);
            roleEditPanel.Controls.Add(colorHintLbl);
            y += 56;

            btnRoleSave = new Button
            {
                Text = "Saqlash", Location = new Point(24, y),
                Width = 156, Height = 40,
                FlatStyle = FlatStyle.Flat, BackColor = Gold,
                ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnRoleSave.FlatAppearance.BorderSize = 0;
            btnRoleSave.Click += SaveRole;
            roleEditPanel.Controls.Add(btnRoleSave);

            btnRoleDelete = new Button
            {
                Text = "O'chirish", Location = new Point(188, y),
                Width = 168, Height = 40,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(254, 242, 242),
                ForeColor = Danger, Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand, Visible = false
            };
            btnRoleDelete.FlatAppearance.BorderSize = 1;
            btnRoleDelete.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
            btnRoleDelete.Click += DeleteRole;
            roleEditPanel.Controls.Add(btnRoleDelete);

            SelectRoleType("ofitsiant");
        }

        private void UpdateRoleFabPos()
        {
            int rightOffset = (roleEditPanel.Visible ? roleEditPanel.Width : 0) + 20;
            roleFab.Location = new Point(
                contentLavozimlar.Width - rightOffset - roleFab.Width,
                contentLavozimlar.Height - roleFab.Height - 20);
        }

        private Panel MakeRolePill(string label, string roleType, Color color, Point location)
        {
            Panel pill = new Panel
            {
                Width = 96, Height = 36,
                Location = location,
                BackColor = BgPage,
                Cursor = Cursors.Hand,
                Tag = roleType
            };
            pill.Paint += (s, e) =>
            {
                bool active = selectedRoleType == (string)pill.Tag;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(pill.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(active ? color : BgPage), path);
                    e.Graphics.DrawPath(new Pen(active ? color : Border, 1.5f), path);
                }
                using (var f = new Font("Segoe UI", 9, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(label, f,
                        new SolidBrush(active ? Color.White : TextMuted),
                        new RectangleF(0, 0, pill.Width, pill.Height), sf);
            };
            pill.Click += (s, e) => SelectRoleType(roleType);
            return pill;
        }

        private void SelectRoleType(string roleType)
        {
            selectedRoleType = roleType;
            rbAdmin?.Invalidate();
            rbKassir?.Invalidate();
            rbOfitsiant?.Invalidate();
        }

        // ── DATA: USERS ───────────────────────────────────────────────────
        private void LoadRolesCombo()
        {
            try
            {
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter("SELECT id, name FROM user_category ORDER BY name", db.GetCon()))
                    da.Fill(dt);
                cmbRole.DataSource = null;
                cmbRole.DisplayMember = "name";
                cmbRole.ValueMember   = "id";
                cmbRole.DataSource = dt;
            }
            catch (Exception ex) { MessageBox.Show("Lavozimlarni yuklashda xatolik: " + ex.Message); }
        }

        private void LoadUsers()
        {
            try
            {
                usersListPanel.SuspendLayout();
                usersListPanel.Controls.Clear();
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(@"
                    SELECT u.id, u.name, u.login, u.phone_number,
                           uc.name AS cat_name,
                           ISNULL(uc.role_type, LOWER(uc.name)) AS role_type,
                           ISNULL(uc.color, '') AS color
                    FROM [user] u
                    JOIN user_category uc ON uc.id = u.user_category_id
                    ORDER BY ISNULL(u.sort_order, 9999), CASE ISNULL(uc.role_type,LOWER(uc.name)) WHEN 'admin' THEN 0 WHEN 'kassir' THEN 1 ELSE 2 END, u.name",
                    db.GetCon()))
                    da.Fill(dt);

                int y = 0;
                foreach (DataRow row in dt.Rows)
                {
                    Panel card = MakeUserCard(
                        Convert.ToInt32(row["id"]),
                        row["name"].ToString(),
                        row["login"].ToString(),
                        row["phone_number"].ToString(),
                        row["cat_name"].ToString(),
                        row["color"].ToString());
                    card.Top = y;
                    usersListPanel.Controls.Add(card);
                    y += card.Height + 8;
                }

                if (dt.Rows.Count == 0)
                    usersListPanel.Controls.Add(new Label
                    {
                        Text = "Xodimlar yo'q", Font = new Font("Segoe UI", 11),
                        ForeColor = TextMuted, Location = new Point(0, 40), AutoSize = true
                    });
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { usersListPanel.ResumeLayout(); }
        }

        private Panel MakeUserCard(int uid, string name, string login, string phone, string catName, string colorHex)
        {
            Color roleClr = ParseColor(colorHex);
            string initials = name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();

            Panel card = new Panel
            {
                Width = Math.Max(100, usersListPanel.ClientSize.Width - 4),
                Height = 72, BackColor = BgCard, Cursor = Cursors.Hand
            };
            usersListPanel.Resize += (s, e) => card.Width = Math.Max(100, usersListPanel.ClientSize.Width - 4);

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border, 1f), path);
                }
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(243, 244, 246)), 16, 14, 44, 44);
                e.Graphics.DrawEllipse(new Pen(roleClr, 2f), 17, 15, 42, 42);
                using (var f = new Font("Segoe UI", 13, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(initials, f, new SolidBrush(roleClr), new RectangleF(16, 14, 44, 44), sf);
            };

            card.Controls.Add(new Label { Text = name, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark, BackColor = Color.Transparent, Location = new Point(72, 12), AutoSize = true });
            card.Controls.Add(new Label { Text = catName, Font = new Font("Segoe UI", 8), ForeColor = roleClr, BackColor = Color.Transparent, Location = new Point(72, 32), AutoSize = true });
            card.Controls.Add(new Label { Text = "@" + login + (string.IsNullOrEmpty(phone) ? "" : "  •  " + phone), Font = new Font("Segoe UI", 8), ForeColor = TextMuted, BackColor = Color.Transparent, Location = new Point(72, 50), AutoSize = true });

            EventHandler loadEdit = (s, e) =>
            {
                editingUserId = uid;
                lblUserFormTitle.Text = "Xodimni tahrirlash";
                txtName.Text = name; txtLogin.Text = login;
                txtPin.Text = ""; txtPhone.Text = phone;
                if (cmbRole.DataSource is DataTable dt2)
                    foreach (DataRow r in dt2.Rows)
                        if (r["name"].ToString() == catName) { cmbRole.SelectedValue = r["id"]; break; }
                btnUserDelete.Visible = true;
                userEditPanel.Visible = true;
            };

            Color normalBg = BgCard, hoverBg = GoldLight;
            card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
            card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };
            card.Click += loadEdit;
            foreach (Control c in card.Controls) { c.Click += loadEdit; c.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); }; c.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); }; }
            return card;
        }

        private void SaveUser(object sender, EventArgs e)
        {
            string name  = txtName.Text.Trim();
            string login = txtLogin.Text.Trim().ToLower();
            string pin   = txtPin.Text.Trim();
            string phone = txtPhone.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(login))
            { MessageBox.Show("Ism va login majburiy!"); return; }
            if (editingUserId == -1 && (pin.Length != 4 || !IsDigits(pin)))
            { MessageBox.Show("PIN 4 ta raqamdan iborat bo'lishi kerak!"); return; }
            if (editingUserId != -1 && pin.Length > 0 && (pin.Length != 4 || !IsDigits(pin)))
            { MessageBox.Show("PIN 4 ta raqamdan iborat bo'lishi kerak!"); return; }
            if (cmbRole.SelectedIndex < 0) { MessageBox.Show("Lavozim tanlang!"); return; }

            int catId = Convert.ToInt32(cmbRole.SelectedValue);
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                object phoneVal = string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone;
                string appPw = txtAppPassword?.Text.Trim() ?? "";

                if (editingUserId == -1)
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "INSERT INTO [user](name,login,password,phone_number,user_category_id,app_password) VALUES(@n,@l,@p,@ph,@c,@ap)",
                        db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n",  name);
                        cmd.Parameters.AddWithValue("@l",  login);
                        cmd.Parameters.AddWithValue("@p",  Session.HashPin(pin));
                        cmd.Parameters.AddWithValue("@ph", phoneVal);
                        cmd.Parameters.AddWithValue("@c",  catId);
                        cmd.Parameters.AddWithValue("@ap", string.IsNullOrEmpty(appPw) ? (object)DBNull.Value : Session.HashPin(appPw));
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    var setParts = new System.Collections.Generic.List<string>
                        { "name=@n","login=@l","phone_number=@ph","user_category_id=@c" };
                    if (!string.IsNullOrEmpty(pin))   setParts.Add("password=@p");
                    if (!string.IsNullOrEmpty(appPw)) setParts.Add("app_password=@ap");
                    string sql = "UPDATE [user] SET " + string.Join(",", setParts) + " WHERE id=@id";
                    using (SqlCommand cmd = new SqlCommand(sql, db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n",  name);
                        cmd.Parameters.AddWithValue("@l",  login);
                        cmd.Parameters.AddWithValue("@ph", phoneVal);
                        cmd.Parameters.AddWithValue("@c",  catId);
                        cmd.Parameters.AddWithValue("@id", editingUserId);
                        if (!string.IsNullOrEmpty(pin))   cmd.Parameters.AddWithValue("@p",  Session.HashPin(pin));
                        if (!string.IsNullOrEmpty(appPw)) cmd.Parameters.AddWithValue("@ap", Session.HashPin(appPw));
                        cmd.ExecuteNonQuery();
                    }
                }
                db.CloseCon();
                userEditPanel.Visible = false;
                ClearUserForm(); LoadUsers(); LoadRolesCombo();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void DeleteUser(object sender, EventArgs e)
        {
            if (editingUserId == -1) return;
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                int orderCnt = 0;
                using (SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM [order] WHERE user_id=@id", db.GetCon()))
                { chk.Parameters.AddWithValue("@id", editingUserId); orderCnt = (int)chk.ExecuteScalar(); }
                db.CloseCon();

                string msg = orderCnt > 0
                    ? $"Bu xodimda {orderCnt} ta buyurtma mavjud. O'chirilsa buyurtmalarda xodim ma'lumoti o'chadi. Davom etasizmi?"
                    : "Xodimni o'chirishni tasdiqlaysizmi?";
                if (MessageBox.Show(msg, "Tasdiqlash", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                db.OpenCon();
                // Unlink orders first to avoid FK violation
                using (SqlCommand ul = new SqlCommand("UPDATE [order] SET user_id=NULL WHERE user_id=@id", db.GetCon()))
                { ul.Parameters.AddWithValue("@id", editingUserId); ul.ExecuteNonQuery(); }
                using (SqlCommand cmd = new SqlCommand("DELETE FROM [user] WHERE id=@id", db.GetCon()))
                { cmd.Parameters.AddWithValue("@id", editingUserId); cmd.ExecuteNonQuery(); }
                db.CloseCon();
                userEditPanel.Visible = false;
                ClearUserForm(); LoadUsers();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void ClearUserForm()
        {
            editingUserId = -1;
            lblUserFormTitle.Text = "Yangi xodim";
            txtName.Text = txtLogin.Text = txtPin.Text = txtPhone.Text = "";
            if (txtAppPassword != null) txtAppPassword.Text = "";
            btnUserDelete.Visible = false;
            if (cmbRole.Items.Count > 0) cmbRole.SelectedIndex = 0;
        }

        // ── DATA: ROLES ───────────────────────────────────────────────────
        private void LoadRolesList()
        {
            try
            {
                rolesListPanel.SuspendLayout();
                rolesListPanel.Controls.Clear();
                dbconnect db = new dbconnect();
                DataTable dt = new DataTable();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    @"SELECT id, name, ISNULL(role_type,'ofitsiant') AS role_type,
                             ISNULL(color, '#16A34A') AS color,
                             (SELECT COUNT(*) FROM [user] WHERE user_category_id=uc.id) AS cnt
                      FROM user_category uc ORDER BY name",
                    db.GetCon()))
                    da.Fill(dt);

                int y = 0;
                foreach (DataRow row in dt.Rows)
                {
                    Panel card = MakeRoleCard(
                        Convert.ToInt32(row["id"]),
                        row["name"].ToString(),
                        row["role_type"].ToString(),
                        row["color"].ToString(),
                        Convert.ToInt32(row["cnt"]));
                    card.Top = y;
                    rolesListPanel.Controls.Add(card);
                    y += card.Height + 8;
                }
                if (dt.Rows.Count == 0)
                    rolesListPanel.Controls.Add(new Label
                    {
                        Text = "Lavozimlar yo'q", Font = new Font("Segoe UI", 11),
                        ForeColor = TextMuted, Location = new Point(0, 40), AutoSize = true
                    });
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { rolesListPanel.ResumeLayout(); }
        }

        private Panel MakeRoleCard(int rid, string name, string roleType, string colorHex, int userCount)
        {
            Color roleClr = ParseColor(colorHex);
            Panel card = new Panel
            {
                Width = Math.Max(100, rolesListPanel.ClientSize.Width - 4),
                Height = 64, BackColor = BgCard, Cursor = Cursors.Hand
            };
            rolesListPanel.Resize += (s, e) => card.Width = Math.Max(100, rolesListPanel.ClientSize.Width - 4);

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 8))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    e.Graphics.DrawPath(new Pen(Border, 1f), path);
                }
                e.Graphics.FillRectangle(new SolidBrush(roleClr), 0, 0, 4, card.Height);
            };

            card.Controls.Add(new Label { Text = name, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark, BackColor = Color.Transparent, Location = new Point(16, 10), AutoSize = true });
            card.Controls.Add(new Label { Text = GetRoleName(roleType) + $"  •  {userCount} xodim", Font = new Font("Segoe UI", 8), ForeColor = roleClr, BackColor = Color.Transparent, Location = new Point(16, 34), AutoSize = true });

            EventHandler loadEdit = (s, e) =>
            {
                editingRoleId = rid;
                lblRoleFormTitle.Text = "Lavozimni tahrirlash";
                txtRoleName.Text = name;
                SelectRoleType(roleType);
                selectedRoleColor = colorHex;
                colorPickerBtn.BackColor = ParseColor(colorHex);
                btnRoleDelete.Visible = true;
                roleEditPanel.Visible = true;
            };

            Color normalBg = BgCard, hoverBg = GoldLight;
            card.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); };
            card.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); };
            card.Click += loadEdit;
            foreach (Control c in card.Controls) { c.Click += loadEdit; c.MouseEnter += (s, e) => { card.BackColor = hoverBg; card.Refresh(); }; c.MouseLeave += (s, e) => { card.BackColor = normalBg; card.Refresh(); }; }
            return card;
        }

        private void SaveRole(object sender, EventArgs e)
        {
            string name = txtRoleName.Text.Trim();
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("Lavozim nomini kiriting!"); return; }
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                if (editingRoleId == -1)
                {
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO user_category(name, role_type, color) VALUES(@n, @r, @c)", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@r", selectedRoleType);
                        cmd.Parameters.AddWithValue("@c", selectedRoleColor);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SqlCommand cmd = new SqlCommand("UPDATE user_category SET name=@n, role_type=@r, color=@c WHERE id=@id", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@r", selectedRoleType);
                        cmd.Parameters.AddWithValue("@c", selectedRoleColor);
                        cmd.Parameters.AddWithValue("@id", editingRoleId);
                        cmd.ExecuteNonQuery();
                    }
                }
                db.CloseCon();
                roleEditPanel.Visible = false;
                ClearRoleForm(); LoadRolesList(); LoadRolesCombo();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void DeleteRole(object sender, EventArgs e)
        {
            if (editingRoleId == -1) return;
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                int cnt = 0;
                using (SqlCommand c = new SqlCommand("SELECT COUNT(*) FROM [user] WHERE user_category_id=@id", db.GetCon()))
                { c.Parameters.AddWithValue("@id", editingRoleId); cnt = (int)c.ExecuteScalar(); }
                db.CloseCon();
                if (cnt > 0) { MessageBox.Show($"Bu lavozimda {cnt} ta xodim bor. Avval xodimlarni boshqa lavozimga o'tkazing!"); return; }
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); return; }

            if (MessageBox.Show("Lavozimni o'chirasizmi?", "Tasdiqlash",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (SqlCommand cmd = new SqlCommand("DELETE FROM user_category WHERE id=@id", db.GetCon()))
                { cmd.Parameters.AddWithValue("@id", editingRoleId); cmd.ExecuteNonQuery(); }
                db.CloseCon();
                roleEditPanel.Visible = false;
                ClearRoleForm(); LoadRolesList(); LoadRolesCombo();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void ClearRoleForm()
        {
            editingRoleId = -1;
            lblRoleFormTitle.Text = "Yangi lavozim";
            txtRoleName.Text = "";
            btnRoleDelete.Visible = false;
            selectedRoleColor = "#16A34A";
            colorPickerBtn.BackColor = ParseColor(selectedRoleColor);
            SelectRoleType("ofitsiant");
        }

        // ── RESIZE HELPERS ────────────────────────────────────────────────
        private void ResizeUserCards()
        {
            int y = 0;
            foreach (Control c in usersListPanel.Controls)
            {
                if (!(c is Panel)) continue;
                c.Width = Math.Max(100, usersListPanel.ClientSize.Width - 4);
                c.Top   = y;
                y      += c.Height + 8;
            }
        }

        private void ResizeRoleCards()
        {
            int y = 0;
            foreach (Control c in rolesListPanel.Controls)
            {
                if (!(c is Panel)) continue;
                c.Width = Math.Max(100, rolesListPanel.ClientSize.Width - 4);
                c.Top   = y;
                y      += c.Height + 8;
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────
        private Button MakeFab()
        {
            Button b = new Button
            {
                Width = 52, Height = 52,
                BackColor = Success,
                FlatStyle = FlatStyle.Flat,
                Text = "+",
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(b.ClientRectangle, 26))
                    e.Graphics.FillPath(new SolidBrush(Success), path);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var f = new Font("Segoe UI", 24, FontStyle.Bold))
                    e.Graphics.DrawString("+", f, Brushes.White, new RectangleF(0, 0, b.Width, b.Height), sf);
            };
            return b;
        }

        private TextBox AddFieldTo(Panel parent, string label, ref int y, bool isPassword)
        {
            parent.Controls.Add(new Label
            {
                Text = label, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = TextMuted, Location = new Point(24, y), AutoSize = true
            });
            y += 22;

            Panel box = new Panel { Location = new Point(24, y), Width = 332, Height = 36, BackColor = BgPage };
            box.Paint += (s, e) => e.Graphics.DrawRectangle(new Pen(Border), 0, 0, box.Width - 1, box.Height - 1);

            TextBox tb = new TextBox
            {
                Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.None, BackColor = BgPage, ForeColor = TextDark
            };
            if (isPassword) tb.UseSystemPasswordChar = true;
            tb.GotFocus  += (s, e) => { box.BackColor = GoldLight; box.Invalidate(); };
            tb.LostFocus += (s, e) => { box.BackColor = BgPage;    box.Invalidate(); };

            box.Controls.Add(tb);
            parent.Controls.Add(box);
            y += 44;
            return tb;
        }

        private bool IsDigits(string s) { foreach (char c in s) if (!char.IsDigit(c)) return false; return true; }

        private string GetRoleName(string r)
        {
            switch (r?.ToLower()) { case "admin": return "Administrator"; case "kassir": return "Kassir"; default: return "Ofitsiant"; }
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex))
                try { return ColorTranslator.FromHtml(hex); } catch { }
            return Color.FromArgb(22, 163, 74);
        }

        private static string ToHex(Color c) =>
            "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

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
