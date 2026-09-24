using System;
using System.Drawing;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>通用文本输入对话框（新建列表 / 重命名列表）。</summary>
    public class TextInputDialog : Form
    {
        private readonly TextBox _input;

        public string Value
        {
            get { return _input.Text.Trim(); }
        }

        public TextInputDialog(string title, string caption, string initial)
        {
            Text = title;
            Font = UiFont.Get(9.5f);
            ClientSize = new Size(360, 148);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;

            Label label = new Label();
            label.Text = caption;
            label.SetBounds(18, 16, 320, 20);
            label.ForeColor = Design.InkSoft;

            _input = new TextBox();
            _input.Text = initial;
            _input.SetBounds(18, 42, 324, 24);
            _input.SelectAll();

            Button ok = new Button();
            ok.Text = "确定";
            ok.SetBounds(160, 92, 88, 30);
            ok.DialogResult = DialogResult.OK;
            ok.FlatStyle = FlatStyle.System;

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(254, 92, 88, 30);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.FlatStyle = FlatStyle.System;

            Controls.Add(label);
            Controls.Add(_input);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static string Ask(IWin32Window owner, string title, string caption, string initial)
        {
            using (TextInputDialog dialog = new TextInputDialog(title, caption, initial))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK) return dialog.Value;
            }
            return null;
        }
    }

    /// <summary>
    /// <summary>
    /// 列表编辑器：名称 + 图标（下拉框，emoji）+ 主色 + 背景。
    /// 新建列表和编辑已有列表都走这个窗口。
    /// </summary>
    public class ListEditorDialog : Form
    {
        private const int Pad = 18;
        private const int ColorCell = 28;
        private const int ColorGap = 8;

        private readonly TextBox _name;
        private readonly ComboBox _iconBox;
        private int _colorArgb;
        private int _bgKind;
        private string _bgValue;

        private Rectangle _colorArea;
        private Rectangle _bgButton;
        private int _hoverColor = -1;

        public ListEditorDialog(string title, string name, int iconKind, int colorArgb, int bgKind, string bgValue)
        {
            Text = title;
            Font = UiFont.Get(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _colorArgb = colorArgb;
            _bgKind = bgKind;
            _bgValue = bgValue == null ? string.Empty : bgValue;

            int width = 448;
            ClientSize = new Size(width, 300);

            _name = new TextBox();
            _name.Text = name;
            _name.SetBounds(Pad, 38, width - Pad * 2, 24);
            _name.SelectAll();
            Controls.Add(_name);

            // 「图标」这两个字改成 OnPaint 里画（原来用 Label 控件，会压在下拉框上把它挡住）
            _iconBox = new ComboBox();
            _iconBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _iconBox.DrawMode = DrawMode.OwnerDrawFixed;
            _iconBox.ItemHeight = 26;
            _iconBox.Font = UiFont.Get(9.5f);
            _iconBox.SetBounds(Pad + 44, 70, width - Pad * 2 - 44, 26);
            for (int i = 0; i < Icons.IconsList.Length; i++)
                _iconBox.Items.Add(Icons.KindNames[Icons.IconsList[i]]);
            _iconBox.SelectedIndex = IndexOfKind(iconKind);
            _iconBox.DrawItem += IconBox_DrawItem;
            // 选图标就立刻换（正在编辑的列表图标跟着变）
            _iconBox.SelectedIndexChanged += delegate { NotifyPreview(); };
            Controls.Add(_iconBox);

            _colorArea = new Rectangle(Pad, 152, Design.Palette.Length * (ColorCell + ColorGap) - ColorGap, ColorCell);
            _bgButton = new Rectangle(Pad, _colorArea.Bottom + 40, width - Pad * 2, 32);

            Button ok = new Button();
            ok.Text = "确定";
            ok.SetBounds(width - Pad - 176, ClientSize.Height - 40, 84, 30);
            ok.FlatStyle = FlatStyle.System;
            ok.DialogResult = DialogResult.OK;
            ok.Click += delegate { Applied = true; };

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(width - Pad - 84, ClientSize.Height - 40, 84, 30);
            cancel.FlatStyle = FlatStyle.System;
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        /// <summary>点了「确定」。</summary>
        public bool Applied { get; private set; }

        public string ListName
        {
            get
            {
                string text = _name.Text.Trim();
                return text.Length == 0 ? "新列表" : text;
            }
        }

        public int IconKind { get { return SelectedKind; } }
        public int ColorArgb { get { return _colorArgb; } }
        public int BackgroundKind { get { return _bgKind; } }
        public string BackgroundValue { get { return _bgValue; } }

        /// <summary>
        /// 外观预览回调 (iconKind, bgKind, bgValue, colorArgb)：编辑已有列表时，
        /// 选图标 / 点颜色 / 点背景都立刻作用到那个列表上（取消对话框时由调用方还原）。
        /// </summary>
        public Action<int, int, string, int> AppearancePreview;

        private void NotifyPreview()
        {
            if (AppearancePreview != null) AppearancePreview(SelectedKind, _bgKind, _bgValue, _colorArgb);
        }

        private Color CurrentColor
        {
            get
            {
                try { return Color.FromArgb(_colorArgb); }
                catch { return Design.Palette[0]; }
            }
        }

        private static int IndexOfKind(int kind)
        {
            for (int i = 0; i < Icons.IconsList.Length; i++)
                if (Icons.IconsList[i] == kind) return i;
            return 0;
        }

        private int SelectedKind
        {
            get
            {
                int index = _iconBox.SelectedIndex;
                if (index < 0 || index >= Icons.IconsList.Length) return 0;
                return Icons.IconsList[index];
            }
        }

        private void IconBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= Icons.IconsList.Length) return;

            int kind = Icons.IconsList[e.Index];
            Color color = (e.State & DrawItemState.Selected) != 0 ? Color.White : Design.Ink;
            // 图标默认用当前选中的主色画（和侧栏列表图标一致），不再一律黑色
            Color iconColor = (e.State & DrawItemState.Selected) != 0 ? Color.White : CurrentColor;

            // +2 和侧栏同理：字形按墨迹居中、文字按行盒居中，图标下移 2px 才和文字对齐
            Rectangle iconBox = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 3, e.Bounds.Height - 4, e.Bounds.Height - 4);
            Icons.ByKind(e.Graphics, iconBox, kind, iconColor);

            TextRenderer.DrawText(e.Graphics, Icons.KindNames[kind], UiFont.Get(9.5f),
                new Rectangle(iconBox.Right + 8, e.Bounds.Y, e.Bounds.Width - iconBox.Width - 12, e.Bounds.Height),
                color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        // ---------- 绘制 ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush back = new SolidBrush(Color.White))
                g.FillRectangle(back, ClientRectangle);

            Design.DrawText(g, "名称", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, 16, 200, 20), ContentAlignment.MiddleLeft);

            // 「图标」与下拉框同排，画在左边（不是控件，不会挡住下拉框）
            Design.DrawText(g, "图标", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, 73, 40, 20), ContentAlignment.MiddleLeft);

            Design.DrawText(g, "颜色", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, 124, 200, 20), ContentAlignment.MiddleLeft);

            System.Drawing.Drawing2D.SmoothingMode oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            try
            {
                for (int i = 0; i < Design.Palette.Length; i++)
                {
                    Rectangle cell = ColorCellRect(i);
                    bool selected = _colorArgb == Design.Palette[i].ToArgb();

                    Design.FillRound(g, cell, cell.Width / 2, Design.Palette[i]);

                    // 选中态：留一圈白缝 + 2px 柔和深灰圆环（抗锯齿，避免黑色硬框的锯齿感）
                    Rectangle ring = Inflate(cell, 2);
                    if (selected)
                    {
                        using (Pen gap = new Pen(Color.White, 2f))
                        using (System.Drawing.Drawing2D.GraphicsPath gapPath = RoundedPath(Inflate(cell, 1), cell.Width / 2 + 1))
                            g.DrawPath(gap, gapPath);
                    }
                    using (Pen pen = new Pen(selected ? Color.FromArgb(150, 92, 92, 104)
                                                      : Color.FromArgb(90, 166, 164, 174),
                                             selected ? 2f : 1f))
                    using (System.Drawing.Drawing2D.GraphicsPath path = RoundedPath(ring, cell.Width / 2 + 2))
                        g.DrawPath(pen, path);
                }
            }
            finally { g.SmoothingMode = oldSmoothing; }

            Design.DrawText(g, "背景", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, _colorArea.Bottom + 14, 200, 20), ContentAlignment.MiddleLeft);
            Design.Card(g, _bgButton, 6, Color.White, Design.CardBorder);
            Design.FillRound(g, new Rectangle(_bgButton.X + 8, _bgButton.Y + 8, 16, 16), 4, BackgroundPreviewColor());
            Design.DrawText(g, "背景设置…（当前：" + BackgroundKindText() + "）", UiFont.Get(9f), Design.Ink,
                new Rectangle(_bgButton.X + 32, _bgButton.Y, _bgButton.Width - 40, _bgButton.Height),
                ContentAlignment.MiddleLeft);
        }

        private static Rectangle Inflate(Rectangle r, int n)
        {
            return new Rectangle(r.X - n, r.Y - n, r.Width + n * 2, r.Height + n * 2);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Color BackgroundPreviewColor()
        {
            if (_bgKind == 1)
            {
                int index;
                if (!int.TryParse(_bgValue, out index) || index < 0 || index >= Design.Themes.Length) index = 0;
                return Design.Themes[index].From;
            }
            if (_bgKind == 2) return Color.FromArgb(150, 150, 150);
            if (_bgKind == 0) return CurrentColor;
            return Design.Shade(CurrentColor, -0.1f);
        }

        private string BackgroundKindText()
        {
            if (_bgKind == 1)
            {
                int index;
                if (int.TryParse(_bgValue, out index) && index >= 0 && index < Design.Themes.Length)
                    return "渐变 · " + Design.Themes[index].Name;
                return "渐变";
            }
            if (_bgKind == 2) return "本地图片";
            if (_bgKind == 0) return "纯色";
            return "跟随主色（默认）";
        }

        private Rectangle ColorCellRect(int index)
        {
            return new Rectangle(_colorArea.X + index * (ColorCell + ColorGap), _colorArea.Y, ColorCell, ColorCell);
        }

        // ---------- 交互 ----------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int hoverColor = -1;
            for (int i = 0; i < Design.Palette.Length; i++)
                if (ColorCellRect(i).Contains(e.Location)) { hoverColor = i; break; }

            if (hoverColor != _hoverColor)
            {
                _hoverColor = hoverColor;
                Invalidate();
            }
            Cursor = (hoverColor >= 0 || _bgButton.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // 颜色圆点：按下去就换（不用等鼠标松开）
            for (int i = 0; i < Design.Palette.Length; i++)
            {
                if (!ColorCellRect(i).Contains(e.Location)) continue;
                _colorArgb = Design.Palette[i].ToArgb();
                Invalidate();
                NotifyPreview();
                return;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            if (_bgButton.Contains(e.Location))
            {
                // 打开前先记住原值，对话框里点了就立刻换色，「取消」再还原
                int oldKind = _bgKind;
                string oldValue = _bgValue;
                int oldColor = _colorArgb;

                using (ThemePickerDialog dialog = new ThemePickerDialog(oldKind, oldValue, oldColor))
                {
                    dialog.SelectionChanged += delegate
                    {
                        _bgKind = dialog.Kind;
                        _bgValue = dialog.Value;
                        if (dialog.Kind == 0) _colorArgb = dialog.ColorArgb;
                        Invalidate();
                        NotifyPreview();   // 正在编辑的列表背景立刻跟着变
                    };

                    if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Applied) return;

                    _bgKind = oldKind;
                    _bgValue = oldValue;
                    _colorArgb = oldColor;
                    Invalidate();
                }
            }
        }
    }

    /// <summary>
    /// 背景（主题）选择：纯色 / 渐变背景图 / 本地图片。
    /// 对应微软 To Do 里右上角主题按钮打开的「背景」面板。
    /// </summary>
    public class ThemePickerDialog : Form
    {
        private const int Pad = 18;
        private const int SwatchW = 74;
        private const int SwatchH = 50;
        private const int SwatchGap = 10;
        private const int ButtonRow = 46;

        private readonly Rectangle _solidArea;
        private readonly Rectangle _themeArea;
        private readonly Rectangle _imageArea;

        private int _kind;
        private string _value;
        private int _colorArgb;

        private int _hoverIndex = -1;
        private int _hoverGroup = -1;
        private bool _hoverImage;
        private bool _hoverClear;

        public ThemePickerDialog(int kind, string value, int colorArgb)
        {
            Text = "背景";
            Font = UiFont.Get(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _kind = kind;
            _value = value == null ? string.Empty : value;
            _colorArgb = colorArgb;

            int width = 560;
            int perRow = (width - Pad * 2 + SwatchGap) / (SwatchW + SwatchGap);
            int solidRows = (Design.Palette.Length + perRow - 1) / perRow;
            int themeRows = (Design.Themes.Length + perRow - 1) / perRow;

            int y = Pad + 22;
            _solidArea = new Rectangle(Pad, y, width - Pad * 2, solidRows * (SwatchH + SwatchGap) - SwatchGap);
            y = _solidArea.Bottom + 46;
            _themeArea = new Rectangle(Pad, y, width - Pad * 2, themeRows * (SwatchH + SwatchGap) - SwatchGap);
            y = _themeArea.Bottom + 46;
            _imageArea = new Rectangle(Pad, y, width - Pad * 2, 34);

            ClientSize = new Size(width, _imageArea.Bottom + ButtonRow + 10);
        }

        public bool Applied { get; private set; }
        public int Kind { get { return _kind; } }
        public string Value { get { return _value; } }
        public int ColorArgb { get { return _colorArgb; } }

        /// <summary>鼠标一按下选中某个背景就触发，调用方据此「即时生效」。</summary>
        public event EventHandler SelectionChanged;

        private void RaiseSelection() { Invalidate(); EventHandler h = SelectionChanged; if (h != null) h(this, EventArgs.Empty); }

        private int PerRow
        {
            get { return (ClientSize.Width - Pad * 2 + SwatchGap) / (SwatchW + SwatchGap); }
        }

        private static Rectangle Cell(Rectangle area, int index, int perRow)
        {
            int col = index % perRow;
            int row = index / perRow;
            return new Rectangle(area.X + col * (SwatchW + SwatchGap),
                area.Y + row * (SwatchH + SwatchGap), SwatchW, SwatchH);
        }

        private Rectangle ClearRect
        {
            get { return new Rectangle(_imageArea.Right - 100, _imageArea.Y + 5, 92, 24); }
        }

        private Rectangle OkRect
        {
            get { return new Rectangle(ClientSize.Width - Pad - 176, ClientSize.Height - 40, 84, 30); }
        }

        private Rectangle CancelRect
        {
            get { return new Rectangle(ClientSize.Width - Pad - 84, ClientSize.Height - 40, 84, 30); }
        }

        // ---------- 绘制 ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush back = new SolidBrush(Color.White))
                g.FillRectangle(back, ClientRectangle);

            Design.DrawText(g, "纯色", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, Pad, 200, 20), ContentAlignment.MiddleLeft);

            int perRow = PerRow;
            for (int i = 0; i < Design.Palette.Length; i++)
            {
                Rectangle cell = Cell(_solidArea, i, perRow);
                bool selected = _kind == 0 && _colorArgb == Design.Palette[i].ToArgb();
                DrawSwatch(g, cell, Design.Palette[i], null, selected, _hoverGroup == 0 && _hoverIndex == i);
            }

            Design.DrawText(g, "背景图（渐变）", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, _solidArea.Bottom + 18, 220, 20), ContentAlignment.MiddleLeft);

            for (int i = 0; i < Design.Themes.Length; i++)
            {
                Rectangle cell = Cell(_themeArea, i, perRow);
                bool selected = _kind == 1 && _value == i.ToString();
                DrawSwatch(g, cell, Design.Themes[i].From, Design.Themes[i].To, selected,
                    _hoverGroup == 1 && _hoverIndex == i);
            }

            Design.DrawText(g, "使用本地图片", UiFont.Get(9f, FontStyle.Bold), Design.InkSoft,
                new Rectangle(Pad, _themeArea.Bottom + 18, 200, 20), ContentAlignment.MiddleLeft);

            bool hasImage = _kind == 2;
            Design.Card(g, _imageArea, 6, hasImage ? Design.Hover : Color.White, Design.CardBorder);
            Design.DrawText(g,
                hasImage ? "已选择：" + ShortPath(_value) : "点此选择图片文件（jpg / png / bmp）…",
                UiFont.Get(9f), hasImage ? Design.Ink : Design.InkFaint,
                new Rectangle(_imageArea.X + 12, _imageArea.Y, _imageArea.Width - 120, _imageArea.Height),
                ContentAlignment.MiddleLeft);

            if (hasImage)
            {
                Rectangle clear = ClearRect;
                Design.Card(g, clear, 5, _hoverClear ? Design.Hover : Color.White, Design.CardBorder);
                Design.DrawText(g, "改用默认", UiFont.Get(8.5f), Design.InkSoft, clear, ContentAlignment.MiddleCenter);
            }

            Rectangle ok = OkRect;
            Rectangle cancel = CancelRect;
            Design.FillRound(g, ok, 5, Design.BrandPurple);
            Design.DrawText(g, "应用", UiFont.Get(9.5f), Color.White, ok, ContentAlignment.MiddleCenter);
            Design.DrawText(g, "取消", UiFont.Get(9.5f), Design.InkSoft, cancel, ContentAlignment.MiddleCenter);
        }

        private static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try
            {
                string name = System.IO.Path.GetFileName(path);
                return name.Length > 26 ? name.Substring(0, 24) + "…" : name;
            }
            catch { return path; }
        }

        private void DrawSwatch(Graphics g, Rectangle cell, Color from, Color? to, bool selected, bool hover)
        {
            Rectangle r = cell;
            if (hover) r.Inflate(1, 1);

            if (to.HasValue)
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush brush =
                    new System.Drawing.Drawing2D.LinearGradientBrush(r, from, to.Value, 45f))
                    g.FillRectangle(brush, r);
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(from))
                    g.FillRectangle(brush, r);
            }

            using (Pen pen = new Pen(selected ? Design.BrandPurple : Design.CardBorder, selected ? 2.4f : 1f))
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        // ---------- 交互 ----------

        private bool HitSwatch(Point p, out int group, out int index)
        {
            group = -1;
            index = -1;

            for (int i = 0; i < Design.Palette.Length; i++)
                if (Cell(_solidArea, i, PerRow).Contains(p)) { group = 0; index = i; return true; }

            for (int i = 0; i < Design.Themes.Length; i++)
            {
                Rectangle cell = Cell(_themeArea, i, PerRow);
                if (cell.Contains(p)) { group = 1; index = i; return true; }
            }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int group, index;
            bool hit = HitSwatch(e.Location, out group, out index);
            bool overImage = _imageArea.Contains(e.Location);
            bool overClear = _kind == 2 && ClearRect.Contains(e.Location);
            bool overButton = OkRect.Contains(e.Location) || CancelRect.Contains(e.Location);

            if (group != _hoverGroup || index != _hoverIndex || overImage != _hoverImage || overClear != _hoverClear)
            {
                _hoverGroup = group;
                _hoverIndex = index;
                _hoverImage = overImage;
                _hoverClear = overClear;
                Invalidate();
            }
            Cursor = (hit || overImage || overButton) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            // 鼠标一按下就选中并立刻生效（「应用」只负责收尾关闭）
            int group, index;
            if (HitSwatch(e.Location, out group, out index))
            {
                if (group == 0)
                {
                    _kind = 0;
                    _value = string.Empty;
                    _colorArgb = Design.Palette[index].ToArgb();
                }
                else
                {
                    _kind = 1;
                    _value = index.ToString();
                }
                RaiseSelection();
                return;
            }

            if (_kind == 2 && ClearRect.Contains(e.Location))
            {
                _kind = 0;
                _value = string.Empty;
                _colorArgb = Design.BrandPurple.ToArgb();
                RaiseSelection();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            if (OkRect.Contains(e.Location))
            {
                Applied = true;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            if (CancelRect.Contains(e.Location))
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (_imageArea.Contains(e.Location))
            {
                if (_kind == 2 && ClearRect.Contains(e.Location)) return;   // 已在 MouseDown 里生效
                BrowseImage();
            }
        }

        private void BrowseImage()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择背景图片";
                dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                _kind = 2;
                _value = dialog.FileName;
                RaiseSelection();
            }
        }
    }

    /// <summary>到期日选择：今天 / 明天 / 下周 / 自定义，或清除。</summary>
    public class DatePickerDialog : Form
    {
        private readonly MonthCalendar _calendar;
        private int _day;

        public int SelectedDay
        {
            get { return _day; }
        }

        public DatePickerDialog(int currentDay)
        {
            Text = "到期日";
            Font = UiFont.Get(9.5f);
            ClientSize = new Size(336, 356);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;

            _day = currentDay;

            Button today = MakeButton("今天", 14, 12);
            today.Click += delegate { Finish(TodoItem.Today); };

            Button tomorrow = MakeButton("明天", 92, 12);
            tomorrow.Click += delegate { Finish(NextDay(1)); };

            Button nextWeek = MakeButton("下周", 170, 12);
            nextWeek.Click += delegate { Finish(NextDay(7)); };

            Button clear = MakeButton("清除", 248, 12);
            clear.Width = 78;
            clear.Click += delegate { Finish(0); };

            _calendar = new MonthCalendar();
            _calendar.SetBounds(14, 54, 308, 200);
            _calendar.MaxSelectionCount = 1;
            try
            {
                DateTime start = currentDay != 0 ? TodoItem.FromDay(currentDay) : DateTime.Today;
                if (start == DateTime.MinValue) start = DateTime.Today;
                _calendar.SetDate(start);
            }
            catch { }

            Button ok = new Button();
            ok.Text = "确定";
            ok.SetBounds(144, 314, 84, 30);
            ok.FlatStyle = FlatStyle.System;
            ok.Click += delegate { Finish(TodoItem.ToDay(_calendar.SelectionStart)); };

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(236, 314, 84, 30);
            cancel.FlatStyle = FlatStyle.System;
            cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(today);
            Controls.Add(tomorrow);
            Controls.Add(nextWeek);
            Controls.Add(clear);
            Controls.Add(_calendar);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private static int NextDay(int offset)
        {
            return TodoItem.ToDay(DateTime.Today.AddDays(offset));
        }

        private Button MakeButton(string text, int x, int y)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, 74, 28);
            button.FlatStyle = FlatStyle.System;
            return button;
        }

        private void Finish(int day)
        {
            _day = day;
            DialogResult = DialogResult.OK;
            Close();
        }

        public static int Ask(IWin32Window owner, int currentDay, out bool changed)
        {
            changed = false;
            using (DatePickerDialog dialog = new DatePickerDialog(currentDay))
            {
                if (dialog.ShowDialog(owner) == DialogResult.OK)
                {
                    changed = true;
                    return dialog.SelectedDay;
                }
            }
            return currentDay;
        }
    }
}
