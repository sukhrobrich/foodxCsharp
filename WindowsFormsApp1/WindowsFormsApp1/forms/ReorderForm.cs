using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Generic drag-to-reorder dialog. Pass any list of (Id, Name) items,
    /// drag rows to rearrange, click Save — OrderedIds returns the new sequence.
    /// </summary>
    public class ReorderForm : Form
    {
        private const int ROW_H = 52;
        private const int ROW_G = 4;

        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color BgMain   = Color.FromArgb(248, 248, 250);
        private static readonly Color CardBg   = Color.White;
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color TextMuted = Color.FromArgb(107, 114, 128);
        private static readonly Color DragBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color SaveGreen = Color.FromArgb(22, 163, 74);
        private static readonly Color HandleClr = Color.FromArgb(190, 190, 200);

        private Panel       _listWrap;
        private Panel       _scrollOuter;
        private List<Panel> _rows = new List<Panel>();
        private Panel       _dragRow;
        private int         _dragOffsetY;

        public bool      Saved      { get; private set; }
        public List<int> OrderedIds { get; private set; }

        public ReorderForm(string title, List<(int Id, string Name)> items)
        {
            BuildUI(title, items);
        }

        private void BuildUI(string title, List<(int Id, string Name)> items)
        {
            this.Text            = title;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Size            = new Size(480, 640);
            this.BackColor       = BgMain;

            // ── Header ─────────────────────────────────────────────
            Panel header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = CardBg };
            header.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 57, header.Width, 57);

            Button btnX = MakeBtn("✕", 36, Color.FromArgb(243, 244, 246), TextDark);
            btnX.Location = new Point(12, 11);
            btnX.Font     = new Font("Segoe UI", 11);
            btnX.Click   += (s, e) => this.Close();
            header.Controls.Add(btnX);

            Label lblTitle = new Label
            {
                Text = title, Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark, AutoSize = true
            };
            header.Controls.Add(lblTitle);
            header.Resize += (s, e) =>
                lblTitle.Location = new Point((header.Width - lblTitle.Width) / 2, (58 - lblTitle.Height) / 2);

            // ── Hint ───────────────────────────────────────────────
            Panel hint = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = BgMain };
            hint.Controls.Add(new Label
            {
                Text = "≡ belgisini bosib sudrab tartibini o'zgartiring",
                Font = new Font("Segoe UI", 8), ForeColor = TextMuted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
            });

            // ── Footer ─────────────────────────────────────────────
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 64, BackColor = CardBg };
            footer.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, footer.Width, 0);

            Button btnSave = MakeBtn("Saqlash", 40, SaveGreen, Color.White);
            btnSave.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnSave.Click += OnSave;
            footer.Controls.Add(btnSave);
            footer.Resize += (s, e) =>
            {
                btnSave.Width    = footer.Width - 32;
                btnSave.Location = new Point(16, 12);
            };

            // ── Scroll area ────────────────────────────────────────
            _scrollOuter = new Panel { Dock = DockStyle.Fill, BackColor = BgMain, AutoScroll = true };

            _listWrap = new Panel { BackColor = BgMain, Location = new Point(12, 8) };
            _scrollOuter.Controls.Add(_listWrap);
            _scrollOuter.Resize += (s, e) => ResizeList();

            this.Controls.Add(_scrollOuter); // Fill FIRST
            this.Controls.Add(footer);       // Bottom
            this.Controls.Add(hint);         // Top
            this.Controls.Add(header);       // Top, last = topmost

            BuildRows(items);
        }

        private void ResizeList()
        {
            int w = Math.Max(200, _scrollOuter.ClientSize.Width - 24);
            _listWrap.Width  = w;
            _listWrap.Height = _rows.Count * (ROW_H + ROW_G) + 8;
            foreach (Panel r in _rows) r.Width = w;
        }

        private void BuildRows(List<(int Id, string Name)> items)
        {
            _rows.Clear();
            _listWrap.Controls.Clear();

            for (int i = 0; i < items.Count; i++)
                _rows.Add(CreateRow(items[i].Id, items[i].Name, i));

            _listWrap.Height = _rows.Count * (ROW_H + ROW_G) + 8;
        }

        private Panel CreateRow(int id, string name, int index)
        {
            Panel row = new Panel
            {
                Tag       = id,
                Height    = ROW_H,
                Width     = Math.Max(200, (_scrollOuter?.ClientSize.Width ?? 456) - 24),
                BackColor = CardBg,
                Location  = new Point(0, index * (ROW_H + ROW_G))
            };
            row.Paint += (s, e) =>
            {
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
            };

            // Left drag handle
            Panel hndL = MakeHandle();
            hndL.Location = new Point(0, 0);

            // Row number
            Label lblNum = new Label
            {
                Text = (index + 1).ToString(),
                Font = new Font("Segoe UI", 9), ForeColor = TextMuted,
                Width = 28, Height = ROW_H, Location = new Point(38, 0),
                TextAlign = ContentAlignment.MiddleCenter, Tag = "num"
            };

            // Name
            Label lblName = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = TextDark,
                Height = ROW_H, Location = new Point(66, 0),
                TextAlign = ContentAlignment.MiddleLeft, AutoSize = false
            };

            // Right drag handle
            Panel hndR = MakeHandle();
            row.Resize += (s, e) =>
            {
                hndR.Location  = new Point(row.Width - 38, 0);
                lblName.Width  = row.Width - 66 - 38;
            };
            hndR.Location = new Point(row.Width - 38, 0);
            lblName.Width  = row.Width - 66 - 38;

            _listWrap.Controls.Add(row);
            row.Controls.AddRange(new Control[] { hndL, lblNum, lblName, hndR });

            // Wire drag events on both handles
            foreach (Panel hnd in new[] { hndL, hndR })
            {
                hnd.MouseDown += (s, e) => StartDrag(row, (Control)s, e);
                hnd.MouseMove += (s, e) => DoDrag(row,   (Control)s, e);
                hnd.MouseUp   += (s, e) => EndDrag(row);
            }

            return row;
        }

        private Panel MakeHandle()
        {
            Panel h = new Panel { Width = 38, Height = ROW_H, BackColor = CardBg, Cursor = Cursors.SizeNS };
            h.Paint += (s, e) =>
            {
                int cx = h.Width / 2, cy = h.Height / 2;
                for (int i = -1; i <= 1; i++)
                    using (var p = new Pen(HandleClr, 2) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                        e.Graphics.DrawLine(p, cx - 9, cy + i * 7, cx + 9, cy + i * 7);
            };
            return h;
        }

        private static Button MakeBtn(string text, int height, Color bg, Color fg)
        {
            Button b = new Button
            {
                Text = text, Height = height, Width = 36,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = fg,
                Font = new Font("Segoe UI", 9), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        // ── Drag logic ──────────────────────────────────────────────

        private void StartDrag(Panel row, Control handle, MouseEventArgs e)
        {
            _dragRow     = row;
            _dragOffsetY = row.PointToClient(handle.PointToScreen(e.Location)).Y;
            row.BackColor = DragBg;
            foreach (Control c in row.Controls) c.BackColor = DragBg;
            row.BringToFront();
        }

        private void DoDrag(Panel row, Control handle, MouseEventArgs e)
        {
            if (_dragRow != row) return;

            Point listPt = _listWrap.PointToClient(handle.PointToScreen(e.Location));
            int newTop   = Math.Max(0, Math.Min(_listWrap.Height - ROW_H, listPt.Y - _dragOffsetY));
            row.Top = newTop;

            int newIdx = Math.Min(_rows.Count - 1, Math.Max(0, (newTop + ROW_H / 2) / (ROW_H + ROW_G)));
            int curIdx = _rows.IndexOf(row);

            if (newIdx != curIdx)
            {
                _rows.RemoveAt(curIdx);
                _rows.Insert(newIdx, row);
                RelayoutExcept(row);
            }
        }

        private void EndDrag(Panel row)
        {
            if (_dragRow != row) return;
            _dragRow      = null;
            row.BackColor = CardBg;
            foreach (Control c in row.Controls)
                if (c.BackColor == DragBg) c.BackColor = CardBg;

            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Top = i * (ROW_H + ROW_G);
                SetNum(_rows[i], i + 1);
            }
        }

        private void RelayoutExcept(Panel skip)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != skip)
                    _rows[i].Top = i * (ROW_H + ROW_G);
                SetNum(_rows[i], i + 1);
            }
        }

        private static void SetNum(Panel row, int num)
        {
            foreach (Control c in row.Controls)
                if (c.Tag is string s && s == "num") { c.Text = num.ToString(); return; }
        }

        private void OnSave(object sender, EventArgs e)
        {
            OrderedIds = _rows.Select(r => (int)r.Tag).ToList();
            Saved = true;
            this.Close();
        }

        // ── Static helpers ──────────────────────────────────────────

        /// <summary>Ensures sort_order column on all sortable tables. Call once at app startup.</summary>
        public static void EnsureAllOrderColumns()
        {
            string[] tables = { "user", "food_category", "food", "place_out" };
            foreach (string t in tables)
                EnsureOrderColumn(t);
        }

        /// <summary>Ensures sort_order column exists on the given table.</summary>
        public static void EnsureOrderColumn(string tableName)
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                new SqlCommand(
                    $"IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('[{tableName}]') AND name='sort_order') " +
                    $"ALTER TABLE [{tableName}] ADD sort_order INT NULL",
                    db.GetCon()).ExecuteNonQuery();
                db.CloseCon();
            }
            catch { }
        }

        /// <summary>Saves the ordered list of IDs as sort_order values.</summary>
        public static void SaveOrder(string tableName, List<int> orderedIds)
        {
            try
            {
                dbconnect db = new dbconnect();
                db.OpenCon();
                for (int i = 0; i < orderedIds.Count; i++)
                    using (var cmd = new SqlCommand(
                        $"UPDATE [{tableName}] SET sort_order=@so WHERE id=@id", db.GetCon()))
                    {
                        cmd.Parameters.AddWithValue("@so", i + 1);
                        cmd.Parameters.AddWithValue("@id", orderedIds[i]);
                        cmd.ExecuteNonQuery();
                    }
                db.CloseCon();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tartibni saqlashda xatolik: " + ex.Message);
            }
        }

        /// <summary>Loads (id, name) pairs from a table ordered by sort_order then name.</summary>
        public static List<(int Id, string Name)> LoadItems(string tableName, string nameColumn = "name", string extraWhere = "")
        {
            var list = new List<(int, string)>();
            try
            {
                string where = string.IsNullOrEmpty(extraWhere) ? "" : " WHERE " + extraWhere;
                dbconnect db = new dbconnect();
                db.OpenCon();
                using (var cmd = new SqlCommand(
                    $"SELECT id, [{nameColumn}] AS n FROM [{tableName}]{where} ORDER BY ISNULL(sort_order,9999), [{nameColumn}]",
                    db.GetCon()))
                using (var dr = cmd.ExecuteReader())
                    while (dr.Read())
                        list.Add((Convert.ToInt32(dr["id"]), dr["n"].ToString()));
                db.CloseCon();
            }
            catch { }
            return list;
        }
    }
}
