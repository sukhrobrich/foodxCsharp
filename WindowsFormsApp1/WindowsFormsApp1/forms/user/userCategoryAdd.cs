using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.user
{
    public partial class userCategoryAdd : UserControl
    {
        private TextBox txtName;
        private Panel colorPreview;
        private string selectedColor = "#16A34A";
        private Panel listPanel;

        private static readonly Color BgPage    = Color.FromArgb(249, 249, 251);
        private static readonly Color BgCard    = Color.White;
        private static readonly Color TextDark  = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(156, 163, 175);
        private static readonly Color Border    = Color.FromArgb(229, 231, 235);
        private static readonly Color Green     = Color.FromArgb(22, 163, 74);
        private static readonly Color Red       = Color.FromArgb(220, 38, 38);

        public userCategoryAdd()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.BackColor = BgPage;
            this.Dock = DockStyle.Fill;

            var title = new Label
            {
                Text = "Foydalanuvchi kategoriyalari",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 56,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };

            // Add form card
            var formCard = new Panel
            {
                BackColor = BgCard,
                Dock = DockStyle.Top,
                Height = 76,
            };
            formCard.Paint += (s, e) =>
            {
                using (var p = new Pen(Border, 1))
                    e.Graphics.DrawLine(p, 0, formCard.Height - 1, formCard.Width, formCard.Height - 1);
            };

            var lblName = new Label
            {
                Text = "Kategoriya nomi",
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(16, 10)
            };
            txtName = new TextBox
            {
                Font = new Font("Segoe UI", 11),
                Location = new Point(16, 30),
                Width = 260,
                Height = 32,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblColor = new Label
            {
                Text = "Rang",
                Font = new Font("Segoe UI", 9),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(292, 10)
            };
            colorPreview = new Panel
            {
                Width = 40,
                Height = 32,
                Location = new Point(292, 30),
                BackColor = ParseColor(selectedColor),
                Cursor = Cursors.Hand,
                BorderStyle = BorderStyle.FixedSingle
            };
            colorPreview.Click += PickColor;

            var btnSave = new Button
            {
                Text = "Saqlash",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Green,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(348, 30),
                Width = 110,
                Height = 32,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveCategory;

            formCard.Controls.AddRange(new Control[] { lblName, txtName, lblColor, colorPreview, btnSave });

            // Scrollable list
            var listScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(16, 12, 16, 12)
            };
            listPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = 600,
                BackColor = BgPage
            };
            listScroll.Controls.Add(listPanel);

            this.Controls.Add(listScroll);
            this.Controls.Add(formCard);
            this.Controls.Add(title);
        }

        private void PickColor(object sender, EventArgs e)
        {
            using (var dlg = new ColorDialog { Color = colorPreview.BackColor })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    selectedColor = ToHex(dlg.Color);
                    colorPreview.BackColor = dlg.Color;
                }
            }
        }

        private void SaveCategory(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) { MessageBox.Show("Kategoriya nomini kiriting!"); return; }

            dbconnect db = new dbconnect();
            try
            {
                SqlCommand cmd = new SqlCommand("INSERT INTO user_category (name, color) VALUES (@name, @color)", db.GetCon());
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@color", selectedColor);
                db.OpenCon();
                cmd.ExecuteNonQuery();
                db.CloseCon();
                txtName.Clear();
                LoadCategories();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void userCategoryAdd_Load(object sender, EventArgs e)
        {
            EnsureColorColumn();
            LoadCategories();
        }

        private void LoadCategories()
        {
            listPanel.Controls.Clear();
            dbconnect db = new dbconnect();
            int y = 0;
            try
            {
                SqlCommand cmd = new SqlCommand("SELECT id, name, ISNULL(color, '#16A34A') AS color FROM user_category ORDER BY name", db.GetCon());
                db.OpenCon();
                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    int id = (int)dr["id"];
                    string catName = dr["name"].ToString();
                    string color = dr["color"].ToString();
                    Panel row = MakeCategoryRow(id, catName, color);
                    row.Top = y;
                    listPanel.Controls.Add(row);
                    y += row.Height + 6;
                }
                dr.Close();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
            finally { db.CloseCon(); }
        }

        private Panel MakeCategoryRow(int id, string name, string colorHex)
        {
            Color c = ParseColor(colorHex);
            Panel row = new Panel
            {
                Width = 560,
                Height = 52,
                BackColor = BgCard,
                Padding = new Padding(0)
            };
            row.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
                // Left color accent
                e.Graphics.FillRectangle(new SolidBrush(c), 0, 0, 5, row.Height);
            };

            var circle = new Panel
            {
                Width = 28,
                Height = 28,
                Location = new Point(18, 12),
                BackColor = Color.Transparent
            };
            circle.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(new SolidBrush(c), 0, 0, 27, 27);
                using (var f = new Font("Segoe UI", 10, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper(),
                        f, Brushes.White, new RectangleF(0, 0, 27, 27), sf);
            };

            var lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(56, 10)
            };
            var lblColor = new Label
            {
                Text = colorHex,
                Font = new Font("Courier New", 8),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(57, 31)
            };

            var btnColor = new Button
            {
                Text = "Rang o'zgartir",
                Font = new Font("Segoe UI", 8),
                BackColor = c,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 110,
                Height = 28,
                Location = new Point(row.Width - 232, 12),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnColor.FlatAppearance.BorderSize = 0;
            btnColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = c })
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        UpdateCategoryColor(id, ToHex(dlg.Color));
                        LoadCategories();
                    }
                }
            };

            var btnDel = new Button
            {
                Text = "O'chirish",
                Font = new Font("Segoe UI", 8),
                BackColor = Red,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 80,
                Height = 28,
                Location = new Point(row.Width - 112, 12),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (s, e) =>
            {
                if (MessageBox.Show($"'{name}' kategoriyasini o'chirasizmi?", "Tasdiqlash", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    DeleteCategory(id);
                    LoadCategories();
                }
            };

            row.Controls.AddRange(new Control[] { circle, lblName, lblColor, btnColor, btnDel });
            return row;
        }

        private void UpdateCategoryColor(int id, string color)
        {
            dbconnect db = new dbconnect();
            try
            {
                SqlCommand cmd = new SqlCommand("UPDATE user_category SET color=@c WHERE id=@id", db.GetCon());
                cmd.Parameters.AddWithValue("@c", color);
                cmd.Parameters.AddWithValue("@id", id);
                db.OpenCon();
                cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        private void DeleteCategory(int id)
        {
            dbconnect db = new dbconnect();
            try
            {
                db.OpenCon();
                int? centralCatId = null;
                using (var cid = new SqlCommand("SELECT central_id FROM user_category WHERE id=@id", db.GetCon()))
                { cid.Parameters.AddWithValue("@id", id); object v = cid.ExecuteScalar(); if (v != null && v != DBNull.Value) centralCatId = Convert.ToInt32(v); }
                SqlCommand cmd = new SqlCommand("DELETE FROM user_category WHERE id=@id", db.GetCon());
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                db.CloseCon();
                if (centralCatId.HasValue && Session.IsOnline && Session.TenantId > 0)
                    SyncQueueHelper.Add("UserCategoryDelete", centralCatId.Value, "Delete");
            }
            catch (Exception ex) { MessageBox.Show("Xatolik: " + ex.Message); }
        }

        public static void EnsureColorColumn()
        {
            try
            {
                dbconnect db = new dbconnect();
                string sql = @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('user_category') AND name='color')
                               ALTER TABLE user_category ADD color NVARCHAR(20) NULL";
                SqlCommand cmd = new SqlCommand(sql, db.GetCon());
                db.OpenCon();
                cmd.ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        private static Color ParseColor(string hex)
        {
            if (!string.IsNullOrEmpty(hex))
                try { return ColorTranslator.FromHtml(hex); } catch { }
            return Color.FromArgb(22, 163, 74);
        }

        private static string ToHex(Color c) =>
            "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
    }
}
