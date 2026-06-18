using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.services;

namespace WindowsFormsApp1.forms.multimonoblok
{
    public class MultiOrderForm : Form
    {
        private static readonly Color Gold     = Color.FromArgb(217, 119, 6);
        private static readonly Color GoldBg   = Color.FromArgb(255, 248, 230);
        private static readonly Color BgPage   = Color.FromArgb(248, 248, 250);
        private static readonly Color BgCard   = Color.White;
        private static readonly Color TextDark = Color.FromArgb(17, 24, 39);
        private static readonly Color Muted    = Color.FromArgb(107, 114, 128);
        private static readonly Color Border   = Color.FromArgb(229, 231, 235);
        private static readonly Color Success  = Color.FromArgb(22, 163, 74);
        private static readonly Color Danger   = Color.FromArgb(220, 38, 38);
        private static readonly Color Blue     = Color.FromArgb(59, 130, 246);

        private readonly MultiMonoblokClient _client;
        private readonly int    _tableId;
        private readonly string _tableName;
        private readonly int    _existingOrderId;
        private readonly string _userRole;

        // Joriy buyurtma elementlari: food_id → (name, qty, price, note)
        private readonly Dictionary<int, (string name, int qty, decimal price, string note)> _items
            = new Dictionary<int, (string name, int qty, decimal price, string note)>();

        // UI elementlari
        private Panel          _panCategories;
        private FlowLayoutPanel _flpFoods;
        private Panel          _panOrderList;
        private Label          _lblTotal;
        private Button         _btnSave;
        private Label          _lblStatus;
        private int            _activeCategoryId = -1;

        // Taom kategoriyalari
        private readonly List<(int id, string name)> _categories = new List<(int, string)>();
        private readonly List<string>                 _allFoods   = new List<string>();

        public MultiOrderForm(MultiMonoblokClient client, int tableId, string tableName, int existingOrderId, string userRole = "ofitsiant")
        {
            _client          = client;
            _tableId         = tableId;
            _tableName       = tableName;
            _existingOrderId = existingOrderId;
            _userRole        = userRole;

            BuildUI();
            this.Load += async (s, e) => await InitAsync();
        }

        private void BuildUI()
        {
            this.WindowState     = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor       = BgPage;
            this.Text            = $"FoodX — {_tableName}";
            this.KeyPreview      = true;
            this.KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // === HEADER ===
            Panel header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = BgCard };
            header.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(Gold), 0, 0, header.Width, 4);
                e.Graphics.DrawLine(new Pen(Border), 0, header.Height - 1, header.Width, header.Height - 1);
            };

            header.Controls.Add(new Label
            {
                Text = "FoodX", Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Gold, AutoSize = true, Location = new Point(16, 18)
            });

            Label lblTable = new Label
            {
                Text = $"Stol: {_tableName}  •  " + (_existingOrderId > 0 ? $"Buyurtma #{_existingOrderId}" : "Yangi buyurtma"),
                Font = new Font("Segoe UI", 10), ForeColor = Muted, AutoSize = true
            };
            header.Controls.Add(lblTable);
            header.Resize += (s, e) =>
                lblTable.Location = new Point((header.Width - lblTable.Width) / 2, (header.Height - lblTable.Height) / 2);

            _lblStatus = new Label
            {
                Text = "", Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Success, AutoSize = true
            };
            header.Controls.Add(_lblStatus);
            header.Resize += (s, e) =>
                _lblStatus.Location = new Point(header.Width - _lblStatus.Width - 200, (header.Height - _lblStatus.Height) / 2);

            _btnSave = new Button
            {
                Text = "Saqlash", Width = 140, Height = 38,
                FlatStyle = FlatStyle.Flat, BackColor = Success, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += async (s, e) => await SaveOrderAsync();

            Button btnClose = new Button
            {
                Text = "✕ Yopish", Width = 110, Height = 38,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(243, 244, 246), ForeColor = TextDark,
                Font = new Font("Segoe UI", 10), Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            Panel btnPanel = new Panel { Width = 270, Height = 60, BackColor = Color.Transparent };
            btnPanel.Controls.Add(_btnSave);
            btnPanel.Controls.Add(btnClose);
            _btnSave.Location  = new Point(0, 11);
            btnClose.Location  = new Point(148, 11);
            header.Controls.Add(btnPanel);
            header.Resize += (s, e) => btnPanel.Location = new Point(header.Width - 280, 0);

            // === ANA TUZILMA ===
            // Chap: kategoriyalar (180px)
            _panCategories = new Panel
            {
                Dock = DockStyle.Left, Width = 180,
                BackColor = BgCard,
                AutoScroll = true,
                Padding = new Padding(8, 8, 8, 8)
            };
            _panCategories.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), _panCategories.Width - 1, 0, _panCategories.Width - 1, _panCategories.Height);

            // O'ng: buyurtma ro'yxati (260px)
            _panOrderList = new Panel { Dock = DockStyle.Right, Width = 260, BackColor = BgCard };
            _panOrderList.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, 0, _panOrderList.Height);

            BuildOrderListUI();

            // Markaziy: taomlar
            _flpFoods = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = BgPage,
                Padding = new Padding(12),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            // Controls.Add tartibi: Fill birinchi, Right/Left keyin, Top oxirgi
            this.Controls.Add(_flpFoods);      // Fill
            this.Controls.Add(_panOrderList);   // Right
            this.Controls.Add(_panCategories);  // Left
            this.Controls.Add(header);          // Top (oxirgi = eng tepada)
        }

        private void BuildOrderListUI()
        {
            _panOrderList.Controls.Clear();

            // Jami va saqlash panel
            bool isKassir = _userRole == "kassir" || _userRole == "admin";
            int bottomH = isKassir ? 162 : 110;

            Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = bottomH, BackColor = BgCard };
            bottomPanel.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, 0, bottomPanel.Width, 0);

            _lblTotal = new Label
            {
                Text = "Jami: 0 so'm",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark, Width = 256, Height = 34,
                Location = new Point(0, 10), TextAlign = ContentAlignment.MiddleCenter
            };
            bottomPanel.Controls.Add(_lblTotal);

            Button btnSaveBottom = new Button
            {
                Text = "✓ Saqlash", Width = 240, Height = 38,
                Location = new Point(8, 50),
                FlatStyle = FlatStyle.Flat, BackColor = Success, ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnSaveBottom.FlatAppearance.BorderSize = 0;
            btnSaveBottom.Click += async (s, e) => await SaveOrderAsync();
            bottomPanel.Controls.Add(btnSaveBottom);

            if (isKassir)
            {
                Button btnPay = new Button
                {
                    Text = "To'lov qabul qilish", Width = 240, Height = 44,
                    Location = new Point(8, 96),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold), Cursor = Cursors.Hand
                };
                btnPay.FlatAppearance.BorderSize = 0;
                btnPay.Click += async (s, e) => await OpenPayAsync();
                bottomPanel.Controls.Add(btnPay);
            }

            // Buyurtma elementlari (scroll) — Fill, birinchi qo'shiladi
            var flpItems = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(250, 250, 252),
                Padding = new Padding(4)
            };
            flpItems.Name = "flpItems";

            // Sarlavha — Top, oxirgi qo'shiladi (eng tepaga chiqadi)
            Panel orderHeader = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = BgCard };
            orderHeader.Controls.Add(new Label
            {
                Text = "Buyurtma", Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            orderHeader.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Border), 0, orderHeader.Height - 1, orderHeader.Width, orderHeader.Height - 1);

            // Controls.Add tartibi: Bottom → Fill → Top
            _panOrderList.Controls.Add(bottomPanel);
            _panOrderList.Controls.Add(flpItems);
            _panOrderList.Controls.Add(orderHeader);
        }

        private async Task InitAsync()
        {
            SetStatus("Yuklanmoqda...", Muted);

            try
            {
                // Kategoriyalar va taomlarni parallel yuklash
                var catTask   = _client.GetFoodCategoriesAsync();
                var foodTask  = _client.GetFoodsAsync();
                var orderTask = _existingOrderId > 0 ? _client.GetOrderAsync(_existingOrderId) : null;

                await Task.WhenAll(catTask, foodTask, orderTask ?? Task.FromResult(""));

                // Kategoriyalar
                _categories.Clear();
                _categories.Add((-1, "Hammasi"));
                foreach (var c in MultiMonoblokClient.JsonArr(catTask.Result))
                {
                    int    id   = MultiMonoblokClient.JsonInt(c, "id");
                    string name = MultiMonoblokClient.JsonStr(c, "name");
                    if (id > 0) _categories.Add((id, name));
                }

                // Taomlar
                _allFoods.Clear();
                _allFoods.AddRange(MultiMonoblokClient.JsonArr(foodTask.Result));

                // Mavjud buyurtma elementlarini yuklash
                if (_existingOrderId > 0 && orderTask != null)
                {
                    string orderJson  = orderTask.Result;
                    string itemsJson  = MultiMonoblokClient.JsonNested(orderJson, "items");
                    foreach (var item in MultiMonoblokClient.JsonArr(itemsJson))
                    {
                        int     fid   = MultiMonoblokClient.JsonInt(item, "food_id");
                        string  fname = MultiMonoblokClient.JsonStr(item, "food_name");
                        int     qty   = MultiMonoblokClient.JsonInt(item, "quantity");
                        decimal price = MultiMonoblokClient.JsonDec(item, "selling_price");
                        string  note  = MultiMonoblokClient.JsonStr(item, "note");
                        if (fid > 0) _items[fid] = (fname, qty, price, note);
                    }
                }

                BuildCategories();
                ShowFoods(_activeCategoryId);
                RefreshOrderList();
                SetStatus("", Muted);
            }
            catch (Exception ex)
            {
                SetStatus("Xatolik: " + ex.Message, Danger);
            }
        }

        private void BuildCategories()
        {
            _panCategories.Controls.Clear();
            int y = 4;
            foreach (var (id, name) in _categories)
            {
                int catId = id;
                Button btn = new Button
                {
                    Text = name, Location = new Point(4, y),
                    Width = 160, Height = 38,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = catId == _activeCategoryId ? Gold : BgCard,
                    ForeColor = catId == _activeCategoryId ? Color.White : TextDark,
                    Font = new Font("Segoe UI", 9.5f), Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Tag = catId
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.BorderColor = Border;
                btn.Padding = new Padding(8, 0, 0, 0);
                btn.Click += (s, e) =>
                {
                    _activeCategoryId = catId;
                    BuildCategories();
                    ShowFoods(catId);
                };
                _panCategories.Controls.Add(btn);
                y += 42;
            }
        }

        private void ShowFoods(int categoryId)
        {
            _flpFoods.SuspendLayout();
            _flpFoods.Controls.Clear();

            foreach (var f in _allFoods)
            {
                int catId = MultiMonoblokClient.JsonInt(f, "food_category_id");
                if (categoryId != -1 && catId != categoryId) continue;

                _flpFoods.Controls.Add(MakeFoodCard(f));
            }

            _flpFoods.ResumeLayout();
        }

        private Control MakeFoodCard(string f)
        {
            int     foodId  = MultiMonoblokClient.JsonInt(f, "id");
            string  name    = MultiMonoblokClient.JsonStr(f, "name");
            decimal price   = MultiMonoblokClient.JsonDec(f, "selling_price");
            string  unit    = MultiMonoblokClient.JsonStr(f, "unit");
            bool unlimited  = MultiMonoblokClient.JsonStr(f, "is_unlimited") == "true";
            int  count      = MultiMonoblokClient.JsonInt(f, "count");
            bool outOfStock = !unlimited && count <= 0;

            bool inOrder = _items.ContainsKey(foodId);
            int  curQty  = inOrder ? _items[foodId].qty : 0;

            Color borderCol = inOrder ? Gold : Border;
            Color bgCol     = inOrder ? GoldBg : BgCard;

            Panel card = new Panel
            {
                Width = 158, Height = 116,
                Margin = new Padding(6), BackColor = bgCol, Cursor = outOfStock ? Cursors.No : Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundRect(card.ClientRectangle, 10))
                {
                    e.Graphics.FillPath(new SolidBrush(card.BackColor), path);
                    using (var pen = new Pen(borderCol, inOrder ? 2f : 1.5f))
                        e.Graphics.DrawPath(pen, path);
                }
            };
            card.Name = "food_" + foodId;

            // Tanlangan badge
            if (inOrder)
            {
                Panel badge = new Panel { Width = 28, Height = 20, BackColor = Gold, Location = new Point(126, 4) };
                badge.Controls.Add(new Label
                {
                    Text = curQty.ToString(), Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = Color.White, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                });
                card.Controls.Add(badge);
            }

            // Tugdi badge
            if (outOfStock)
                card.Controls.Add(new Label
                {
                    Text = "TUGDI", Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Danger, AutoSize = true, Location = new Point(4, 4)
                });

            // Taom nomi
            card.Controls.Add(new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = outOfStock ? Muted : TextDark,
                Width = 150, Height = 42,
                Location = new Point(4, 20),
                TextAlign = ContentAlignment.MiddleCenter
            });

            // Narx
            card.Controls.Add(new Label
            {
                Text = FormatMoney(price),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = outOfStock ? Muted : Gold,
                Width = 150, Height = 20,
                Location = new Point(4, 65),
                TextAlign = ContentAlignment.MiddleCenter
            });

            // Birlik
            if (!string.IsNullOrEmpty(unit) && unit != "dona")
                card.Controls.Add(new Label
                {
                    Text = unit, Font = new Font("Segoe UI", 8), ForeColor = Muted,
                    Width = 150, Height = 16, Location = new Point(4, 88),
                    TextAlign = ContentAlignment.MiddleCenter
                });

            if (!outOfStock)
            {
                EventHandler onClick = (s, e) =>
                {
                    if (_items.ContainsKey(foodId))
                    {
                        var old = _items[foodId];
                        _items[foodId] = (old.name, old.qty + 1, old.price, old.note);
                    }
                    else
                    {
                        _items[foodId] = (name, 1, price, "");
                    }
                    RefreshFoodCard(foodId);
                    RefreshOrderList();
                };
                card.Click += onClick;
                foreach (Control c in card.Controls) c.Click += onClick;
            }

            return card;
        }

        private void RefreshFoodCard(int foodId)
        {
            // Karta topib qayta yaratamiz
            string tag = "food_" + foodId;
            Control old = null;
            foreach (Control c in _flpFoods.Controls)
                if (c.Name == tag) { old = c; break; }

            if (old == null) return;

            // Taomni _allFoods dan topib yangi karta yaratamiz
            string foodJson = null;
            foreach (var f in _allFoods)
                if (MultiMonoblokClient.JsonInt(f, "id") == foodId) { foodJson = f; break; }

            if (foodJson == null) return;

            int idx = _flpFoods.Controls.IndexOf(old);
            _flpFoods.Controls.Remove(old);
            var newCard = MakeFoodCard(foodJson);
            _flpFoods.Controls.Add(newCard);
            _flpFoods.Controls.SetChildIndex(newCard, idx);
        }

        private void RefreshOrderList()
        {
            var flpItems = _panOrderList.Controls["flpItems"] as FlowLayoutPanel;
            if (flpItems == null) return;

            flpItems.SuspendLayout();
            flpItems.Controls.Clear();

            decimal total = 0;
            foreach (var kv in _items)
            {
                int foodId = kv.Key;
                var (fname, qty, price, note) = kv.Value;
                total += qty * price;

                Panel row = new Panel
                {
                    Width = 252, Height = 44,
                    BackColor = BgCard, Margin = new Padding(0, 1, 0, 0)
                };
                row.Paint += (s, e) =>
                    e.Graphics.DrawLine(new Pen(Border), 0, row.Height - 1, row.Width, row.Height - 1);

                // Taom nomi + narx
                row.Controls.Add(new Label
                {
                    Text = fname,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = TextDark, Width = 160, Height = 20,
                    Location = new Point(6, 4)
                });
                row.Controls.Add(new Label
                {
                    Text = FormatMoney(qty * price),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = Gold, Width = 160, Height = 16,
                    Location = new Point(6, 24)
                });

                // [-] [qty] [+]
                int fid = foodId;
                Button btnMinus = MakeQtyBtn("-", Danger, new Point(162, 10));
                btnMinus.Click += (s, e) =>
                {
                    if (_items.ContainsKey(fid))
                    {
                        var old = _items[fid];
                        if (old.qty > 1) _items[fid] = (old.name, old.qty - 1, old.price, old.note);
                        else _items.Remove(fid);
                        RefreshFoodCard(fid);
                        RefreshOrderList();
                    }
                };

                Label lblQty = new Label
                {
                    Text = qty.ToString(),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = TextDark, Width = 24, Height = 24,
                    Location = new Point(192, 10), TextAlign = ContentAlignment.MiddleCenter
                };

                Button btnPlus = MakeQtyBtn("+", Success, new Point(216, 10));
                btnPlus.Click += (s, e) =>
                {
                    if (_items.ContainsKey(fid))
                    {
                        var old = _items[fid];
                        _items[fid] = (old.name, old.qty + 1, old.price, old.note);
                        RefreshFoodCard(fid);
                        RefreshOrderList();
                    }
                };

                row.Controls.Add(btnMinus);
                row.Controls.Add(lblQty);
                row.Controls.Add(btnPlus);
                flpItems.Controls.Add(row);
            }

            if (_items.Count == 0)
            {
                flpItems.Controls.Add(new Label
                {
                    Text = "Buyurtma bo'sh.\nTaom qo'shish uchun\nchap tarafdan bosing.",
                    Font = new Font("Segoe UI", 9), ForeColor = Muted,
                    AutoSize = true, Margin = new Padding(20)
                });
            }

            _lblTotal.Text = "Jami: " + FormatMoney(total);
            flpItems.ResumeLayout();
        }

        private async Task OpenPayAsync()
        {
            int orderId = _existingOrderId;
            if (orderId <= 0)
            {
                SetStatus("Avval buyurtmani saqlang!", Danger);
                return;
            }

            // Jami summani hisoblash
            decimal total = 0;
            foreach (var kv in _items) total += kv.Value.qty * kv.Value.price;

            string payJson;
            try { payJson = await _client.GetPaymentsAsync(); }
            catch { SetStatus("To'lov usullari yuklanmadi", Danger); return; }

            var payments = MultiMonoblokClient.JsonArr(payJson);
            if (payments.Count == 0) { SetStatus("To'lov usullari topilmadi", Danger); return; }

            using (var dlg = new MultiPayForm(_client, orderId, total, payments))
            {
                if (dlg.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    SetStatus("✓ To'lov qabul qilindi!", Success);
                    await Task.Delay(800);
                    this.Close();
                }
            }
        }

        private async Task SaveOrderAsync()
        {
            if (_items.Count == 0) { SetStatus("Biror taom qo'shing!", Danger); return; }

            _btnSave.Enabled = false;
            _btnSave.Text    = "Saqlanmoqda...";
            SetStatus("", Muted);

            try
            {
                // JSON yasash
                var sb = new StringBuilder();
                sb.Append("{\"items\":[");
                bool first = true;
                foreach (var kv in _items)
                {
                    if (!first) sb.Append(",");
                    sb.Append($"{{\"foodId\":{kv.Key},\"quantity\":{kv.Value.qty},\"note\":\"{MultiMonoblokClient.Esc(kv.Value.note)}\"}}");
                    first = false;
                }
                sb.Append("]}");

                if (_existingOrderId > 0)
                {
                    // Mavjud buyurtma yangilash — yopmasdan formni yangilaymiz
                    await _client.UpdateOrderItemsAsync(_existingOrderId, sb.ToString());
                    SetStatus("✓ Saqlandi!", Success);

                    // API dan yangilangan buyurtmani qayta yuklaymiz
                    await ReloadOrderFromApiAsync();
                }
                else
                {
                    // Yangi buyurtma: placeId ham qo'shamiz
                    string body = $"{{\"placeId\":{_tableId},\"items\":[";
                    bool f2 = true;
                    foreach (var kv in _items)
                    {
                        if (!f2) body += ",";
                        body += $"{{\"foodId\":{kv.Key},\"quantity\":{kv.Value.qty},\"note\":\"{MultiMonoblokClient.Esc(kv.Value.note)}\"}}";
                        f2 = false;
                    }
                    body += "]}";
                    string json = await _client.CreateOrderAsync(body);
                    string msg  = MultiMonoblokClient.JsonStr(json, "message");
                    SetStatus("✓ " + (string.IsNullOrEmpty(msg) ? "Saqlandi!" : msg), Success);
                    await Task.Delay(800);
                    this.Close();
                }
            }
            catch (MultiApiException ex)
            {
                string msg = MultiMonoblokClient.JsonStr(ex.Body, "message");
                SetStatus("✗ " + (string.IsNullOrEmpty(msg) ? $"HTTP {ex.StatusCode}" : msg), Danger);
            }
            catch (Exception ex)
            {
                SetStatus("✗ " + ex.Message, Danger);
            }
            finally
            {
                _btnSave.Enabled = true;
                _btnSave.Text    = "Saqlash";
            }
        }

        private async Task ReloadOrderFromApiAsync()
        {
            try
            {
                string orderJson = await _client.GetOrderAsync(_existingOrderId);
                string itemsJson = MultiMonoblokClient.JsonNested(orderJson, "items");
                _items.Clear();
                foreach (var item in MultiMonoblokClient.JsonArr(itemsJson))
                {
                    int     fid   = MultiMonoblokClient.JsonInt(item, "food_id");
                    string  fname = MultiMonoblokClient.JsonStr(item, "food_name");
                    int     qty   = MultiMonoblokClient.JsonInt(item, "quantity");
                    decimal price = MultiMonoblokClient.JsonDec(item, "selling_price");
                    string  note  = MultiMonoblokClient.JsonStr(item, "note");
                    if (fid > 0) _items[fid] = (fname, qty, price, note);
                }
                ShowFoods(_activeCategoryId);
                RefreshOrderList();
            }
            catch { /* yangilash xatosi — hozirgi holat qoladi */ }
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = color;
        }

        private static Button MakeQtyBtn(string text, Color bg, Point loc)
        {
            var b = new Button
            {
                Text = text, Width = 24, Height = 24, Location = loc,
                FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private static string FormatMoney(decimal amount)
        {
            return string.Format("{0:N0}", amount).Replace(",", " ") + " so'm";
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
