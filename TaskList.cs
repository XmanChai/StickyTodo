using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>侧边栏导航项。</summary>
    public class SidebarNode
    {
        public string ViewId = string.Empty;
        public string Text = string.Empty;
        public int IconKind;          // 图标编号见 Icons（-1 = 矢量太阳）
        public Color Accent = Design.Ink;
        public bool IsHeader;
        public bool IsNewList;
        public TodoList List;
        public int Count;
        public Rectangle Bounds;
    }

    /// <summary>
    /// 左侧边栏：账号信息 + 内置视图 + 列表分组。
    /// 全部自绘，鼠标悬停/选中态由控件自己处理。
    /// </summary>
    public class SidebarPanel : Panel
    {
        private readonly List<SidebarNode> _nodes = new List<SidebarNode>();
        private int _hover = -1;
        private string _activeView = ViewIds.MyDay;
        private Rectangle _newListRect;
        private bool _hoverNewList;
        private bool _hoverLogo;

        public event EventHandler ViewSelected;
        public event EventHandler NewListRequested;
        public event EventHandler ListContextRequested;

        /// <summary>点在侧栏空白处（没点到任何一项）：收起态用它来收起滑出的菜单。</summary>
        public event EventHandler BlankClicked;

        /// <summary>点顶部数据库图标：打开数据存储目录。</summary>
        public event EventHandler StorageClicked;

        /// <summary>顶部数据库图标（圆形底）的位置。</summary>
        public static Rectangle LogoRect
        {
            get { return new Rectangle(16, 14, 44, 44); }
        }

        public SidebarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Design.Sidebar;
            Font = UiFont.Get(9.5f);
        }

        public string ActiveView
        {
            get { return _activeView; }
            set
            {
                if (_activeView == value) return;
                _activeView = value;
                Invalidate();
            }
        }

        public List<SidebarNode> Nodes
        {
            get { return _nodes; }
        }

        /// <summary>重建导航项列表。</summary>
        public void Build(Store store)
        {
            _nodes.Clear();

            // 内置视图的主色取自「视图样式」，所以每个版块换背景时侧栏颜色一起变
            Add(ViewIds.MyDay, "今天", 6, ViewColor(store, ViewIds.MyDay), false);   // 6 = 太阳字形
            Add(ViewIds.Important, "重要", 1, ViewColor(store, ViewIds.Important), false);
            Add(ViewIds.Done, "任务", 5, ViewColor(store, ViewIds.Done), false);

            SidebarNode header = new SidebarNode();
            header.IsHeader = true;
            header.Text = "列表";
            _nodes.Add(header);

            foreach (TodoList list in store.Lists)
            {
                SidebarNode node = new SidebarNode();
                node.ViewId = list.Id;
                node.Text = list.Name;
                node.IconKind = 5;
                node.Accent = list.Color;
                node.List = list;
                int active, done;
                store.CountView(list.Id, out active, out done);
                node.Count = active;
                _nodes.Add(node);
            }

            LayoutNodes();
            Invalidate();
        }

        private static Color ViewColor(Store store, string viewId)
        {
            Color color;
            int kind;
            string value;
            store.ResolveViewStyle(viewId, out color, out kind, out value);
            return color;
        }

        private void Add(string viewId, string text, int iconKind, Color accent, bool header)
        {
            SidebarNode node = new SidebarNode();
            node.ViewId = viewId;
            node.Text = text;
            node.IconKind = iconKind;
            node.Accent = accent;
            node.IsHeader = header;
            _nodes.Add(node);
        }

        private void LayoutNodes()
        {
            int y = 76;
            for (int i = 0; i < _nodes.Count; i++)
            {
                SidebarNode node = _nodes[i];
                if (node.IsHeader)
                {
                    node.Bounds = new Rectangle(20, y + 8, Math.Max(10, ClientSize.Width - 40), 20);
                    y += 34;
                }
                else
                {
                    node.Bounds = new Rectangle(12, y, Math.Max(10, ClientSize.Width - 24), Design.NavItemHeight);
                    y += Design.NavItemHeight + 2;
                }
            }

            // 「新建列表」固定在底部
            int bottom = Math.Max(y, ClientSize.Height - 48);
            _newListRect = new Rectangle(12, bottom, Math.Max(10, ClientSize.Width - 24), Design.NavItemHeight);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutNodes();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush back = new SolidBrush(Design.Sidebar))
                g.FillRectangle(back, ClientRectangle);

            // 账号区：圆形底 + 数据库图标 + 「我的存储」（点一下打开数据目录）
            Rectangle logo = LogoRect;
            Design.FillRound(g, logo, logo.Width / 2, Color.FromArgb(48, 135, 100, 184));
            Icons.Database(g, new Rectangle(logo.X + 10, logo.Y + 10, logo.Width - 20, logo.Height - 20),
                Design.BrandPurple);
            Design.DrawText(g, "我的存储", UiFont.Get(11.5f, FontStyle.Bold), Design.Ink,
                new Rectangle(72, 16, ClientSize.Width - 84, 24), ContentAlignment.MiddleLeft);
            Design.DrawText(g, "本地存储 · 无需登录", UiFont.Get(9f), Design.InkFaint,
                new Rectangle(72, 41, ClientSize.Width - 84, 19), ContentAlignment.MiddleLeft);

            for (int i = 0; i < _nodes.Count; i++)
            {
                // 「列表」分组上方补一条分格线
                if (_nodes[i].IsHeader) DrawSeparator(g, _nodes[i].Bounds.Y - 7);
                DrawNode(g, _nodes[i], i);
            }

            DrawNewList(g);
        }

        private void DrawSeparator(Graphics g, int y)
        {
            if (y <= 4 || y >= ClientSize.Height - 4) return;
            using (Pen pen = new Pen(Color.FromArgb(221, 220, 228)))
                g.DrawLine(pen, 10, y, Math.Max(12, ClientSize.Width - 10), y);
        }

        private void DrawNewList(Graphics g)
        {
            if (_hoverNewList)
                Design.FillRound(g, _newListRect, 6, Design.Hover);

            Icons.Plus(g, new Point(_newListRect.X + 20, _newListRect.Y + _newListRect.Height / 2 + 2),
                14, Design.BrandPurple);
            Design.DrawText(g, "新建列表", UiFont.Get(9.5f), Design.Ink,
                new Rectangle(_newListRect.X + 38, _newListRect.Y,
                    _newListRect.Width - 45, _newListRect.Height), ContentAlignment.MiddleLeft);
        }

        private void DrawNode(Graphics g, SidebarNode node, int index)
        {
            if (node.IsHeader)
            {
                Design.DrawText(g, node.Text, UiFont.Get(8f), Design.InkSoft, node.Bounds, ContentAlignment.MiddleLeft);
                return;
            }

            bool active = !node.IsNewList && node.ViewId == _activeView;
            if (active)
            {
                Design.FillRound(g, node.Bounds, 6, Design.PillActive);
                // 选中指示：最左侧一条竖条（跟随该视图的颜色）
                Design.FillRound(g, new Rectangle(4, node.Bounds.Y + 5, 3, node.Bounds.Height - 10), 2, node.Accent);
            }
            else if (index == _hover)
            {
                Design.FillRound(g, node.Bounds, 6, Design.Hover);
            }

            // 图标：字形按墨迹居中，而文字（TextRenderer）按行盒居中、墨迹比行盒中心低约 2px，
            // 所以这里把图标也下移 2px，和文字在视觉上真正对齐
            Rectangle icon = new Rectangle(node.Bounds.X + 14,
                node.Bounds.Y + (node.Bounds.Height - 16) / 2 + 2, 16, 16);
            DrawIcon(g, node, icon);

            // 文字统一用深色（与参考图一致），颜色只体现在图标上——
            // 否则某个视图换成浅色主色后，侧栏文字会发白看不清
            Color textColor = Design.Ink;
            Font font = UiFont.Get(9.5f, active ? FontStyle.Bold : FontStyle.Regular);
            int textLeft = node.Bounds.X + 38;
            int reserve = node.Count > 0 ? 34 : 4;
            Design.DrawText(g, node.Text, font, textColor,
                new Rectangle(textLeft, node.Bounds.Y, node.Bounds.Width - 38 - reserve, node.Bounds.Height),
                ContentAlignment.MiddleLeft);

            if (node.Count > 0)
            {
                Design.DrawText(g, node.Count.ToString(), UiFont.Get(8.5f), Design.InkFaint,
                    new Rectangle(node.Bounds.Right - 34, node.Bounds.Y, 28, node.Bounds.Height),
                    ContentAlignment.MiddleRight);
            }
        }

        private static void DrawIcon(Graphics g, SidebarNode node, Rectangle box)
        {
            if (node.IsNewList)
            {
                Icons.Plus(g, new Point(box.X + box.Width / 2, box.Y + box.Height / 2), 14, node.Accent);
                return;
            }
            // 自定义列表用列表自己选的图标，内置视图用固定图标
            int kind = node.List != null ? node.List.IconKind : node.IconKind;
            Icons.ByKind(g, box, kind, node.Accent);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].IsHeader) continue;
                if (_nodes[i].Bounds.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.Location);
            bool overNew = _newListRect.Contains(e.Location);
            bool overLogo = LogoRect.Contains(e.Location);

            if (hit != _hover || overNew != _hoverNewList || overLogo != _hoverLogo)
            {
                _hover = hit;
                _hoverNewList = overNew;
                _hoverLogo = overLogo;
                Cursor = (hit >= 0 || overNew || overLogo) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1 || _hoverNewList || _hoverLogo)
            {
                _hover = -1;
                _hoverNewList = false;
                _hoverLogo = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            // 顶部数据库图标：打开数据存储目录
            if (LogoRect.Contains(e.Location))
            {
                if (e.Button != MouseButtons.Left) return;
                EventHandler storage = StorageClicked;
                if (storage != null) storage(this, EventArgs.Empty);
                return;
            }

            if (_newListRect.Contains(e.Location))
            {
                if (e.Button != MouseButtons.Left) return;
                EventHandler handler = NewListRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }

            int hit = HitTest(e.Location);
            if (hit < 0)
            {
                if (e.Button == MouseButtons.Left)
                {
                    EventHandler blank = BlankClicked;
                    if (blank != null) blank(this, EventArgs.Empty);
                }
                return;
            }
            SidebarNode node = _nodes[hit];

            if (e.Button == MouseButtons.Right && node.List != null)
            {
                EventHandler handler = ListContextRequested;
                if (handler != null) handler(node, EventArgs.Empty);
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            ActiveView = node.ViewId;
            EventHandler selected = ViewSelected;
            if (selected != null) selected(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 任务列表：白卡任务行、圆形勾选框、到期日、重要星标、行尾更多按钮。
    /// 点击各处直接触发对应动作，不做多选。背景由外部绘制（支持纯色/渐变/图片）。
    /// </summary>
    public class TaskListPanel : Panel
    {
        private const int CardGap = 8;

        private const int GroupHeaderHeight = 34;
        private const int Step = Design.RowHeight + CardGap;

        /// <summary>列表顶部留白：跟标题区（副标题）拉开距离。</summary>
        private const int TopPad = 14;

        /// <summary>显示用的扁平列表：未完成组在前，已完成组在后。</summary>
        private readonly List<TodoItem> _items = new List<TodoItem>();
        private readonly List<TodoItem> _activeSrc = new List<TodoItem>();
        private readonly List<TodoItem> _doneSrc = new List<TodoItem>();

        private Font _font = UiFont.Get(9.5f);
        private Font _doneFont = UiFont.Get(9.5f, FontStyle.Strikeout);
        private int _hover = -1;
        private int _selected = -1;
        private int _scrollY;

        /// <summary>_items 中前多少条属于「未完成」组。</summary>
        private int _activeCount;
        private string _activeGroupName = string.Empty;
        private string _doneGroupName = string.Empty;
        private bool _activeOpen = true;
        private bool _doneOpen = true;
        private int _activeHeaderH;
        private int _doneHeaderH;
        private int _contentHeight;

        /// <summary>背景绘制回调：(Graphics, 布局基准矩形) => void。</summary>
        public event Action<Graphics, Rectangle> BackgroundPainter;

        /// <summary>压暗遮罩的透明度（0 = 不画）。</summary>
        public int OverlayAlpha { get; set; }

        /// <summary>背景布局基准矩形（本控件坐标系）。空则用自身客户区。
        /// 由主窗口填入「内容区在本控件里的位置」，这样图像/渐变与标题区能接上。</summary>
        public Rectangle BackgroundMapping { get; set; }

        public event EventHandler ItemToggled;
        public event EventHandler ItemActivated;
        public event EventHandler ItemContextRequested;
        public event EventHandler ItemDeleteRequested;

        /// <summary>点分组标题：切换展开/收起（GroupEventArgs.DoneGroup 区分是哪一组）。</summary>
        public event EventHandler GroupToggled;

        /// <summary>右键分组标题。</summary>
        public event EventHandler GroupMenuRequested;

        /// <summary>右键列表空白处（用于「清空全部 / 清空已完成」）。</summary>
        public event EventHandler BackgroundContextRequested;

        /// <summary>遮罩显示期间点到了列表区域：交给主窗口收起滑出的菜单。</summary>
        public event EventHandler MaskClicked;

        public TaskListPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Design.ContentBack;
            TabStop = true;
        }

        /// <summary>勾选完成的颜色（跟随当前列表主题色）。</summary>
        public Color AccentColor { get; set; }

        /// <summary>分组标题等文字的颜色（背景变化时由外部调整）。</summary>
        public Color OnBackgroundInk { get; set; }

        /// <summary>空状态卡片标题；为空则不画空状态。</summary>
        public string EmptyTitle { get; set; }

        /// <summary>空状态卡片说明（可换行）。</summary>
        public string EmptyHint { get; set; }

        /// <summary>
        /// 设置两组内容。activeHeader / doneHeader 传空字符串则不画该组标题。
        /// 普通视图只传「已完成」；「已完成」视图会传两组（未完成 / 已完成）。
        /// </summary>
        public void SetGroups(List<TodoItem> active, List<TodoItem> done, string activeHeader, string doneHeader)
        {
            _activeSrc.Clear();
            if (active != null) _activeSrc.AddRange(active);
            _doneSrc.Clear();
            if (done != null) _doneSrc.AddRange(done);

            _activeGroupName = activeHeader == null ? string.Empty : activeHeader;
            _doneGroupName = doneHeader == null ? string.Empty : doneHeader;

            _hover = -1;
            _selected = -1;
            _scrollY = 0;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            _activeHeaderH = string.IsNullOrEmpty(_activeGroupName) ? 0 : GroupHeaderHeight;
            _doneHeaderH = string.IsNullOrEmpty(_doneGroupName) ? 0 : GroupHeaderHeight;

            _items.Clear();
            if (_activeOpen) _items.AddRange(_activeSrc);
            _activeCount = _items.Count;
            if (_doneOpen) _items.AddRange(_doneSrc);

            int activeRun = _activeOpen ? _activeSrc.Count : 0;
            int doneRun = _doneOpen ? _doneSrc.Count : 0;
            _contentHeight = TopPad + _activeHeaderH + _doneHeaderH + (activeRun + doneRun) * Step + 16;
            Invalidate();
        }

        /// <summary>展开/收起某一组（doneGroup = true 表示「已完成」组）。</summary>
        public void ToggleGroup(bool doneGroup)
        {
            if (doneGroup) _doneOpen = !_doneOpen;
            else _activeOpen = !_activeOpen;
            RefreshLayout();
        }

        /// <summary>滚动偏移（自己维护；背景固定不随内容滚动，与 To Do 一致）。</summary>
        private int ScrollOffset
        {
            get { return _scrollY; }
        }

        private int RowWidth
        {
            get { return Math.Max(40, ClientSize.Width - 2); }
        }

        private int ActiveRun
        {
            get { return _activeOpen ? _activeSrc.Count : 0; }
        }

        /// <summary>当前选中的任务（没有选中时为 null）。</summary>
        public TodoItem SelectedItem
        {
            get { return _selected >= 0 && _selected < _items.Count ? _items[_selected] : null; }
        }

        private int ItemTop(int index)
        {
            int y = TopPad + _activeHeaderH;
            if (index < _activeCount) return y + index * Step - _scrollY;
            y += ActiveRun * Step + _doneHeaderH;
            return y + (index - _activeCount) * Step - _scrollY;
        }

        private Rectangle CardRect(int index)
        {
            return new Rectangle(0, ItemTop(index), RowWidth, Design.RowHeight);
        }

        private Rectangle ActiveHeaderRect()
        {
            return new Rectangle(0, TopPad - _scrollY, RowWidth, _activeHeaderH);
        }

        private Rectangle DoneHeaderRect()
        {
            int y = TopPad + _activeHeaderH + ActiveRun * Step - _scrollY;
            return new Rectangle(0, y, RowWidth, _doneHeaderH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Action<Graphics, Rectangle> painter = BackgroundPainter;
            if (painter != null)
            {
                Rectangle mapping = (BackgroundMapping.Width > 0 && BackgroundMapping.Height > 0)
                    ? BackgroundMapping : ClientRectangle;
                painter(g, mapping);
            }
            else
            {
                using (SolidBrush back = new SolidBrush(Design.ContentBack))
                    g.FillRectangle(back, ClientRectangle);
            }

            if (_activeSrc.Count == 0 && _doneSrc.Count == 0 && !string.IsNullOrEmpty(EmptyTitle))
            {
                DrawEmptyState(g);
            }
            else
            {
                if (_activeHeaderH > 0)
                    DrawGroupHeader(g, ActiveHeaderRect(), _activeGroupName, _activeSrc.Count, _activeOpen);
                if (_doneHeaderH > 0)
                    DrawGroupHeader(g, DoneHeaderRect(), _doneGroupName, _doneSrc.Count, _doneOpen);

                for (int i = 0; i < _items.Count; i++)
                    DrawCard(g, i, _items[i]);
            }

            // 压暗遮罩（收起态滑出侧栏时用）：画在最后，盖住列表内容
            if (OverlayAlpha > 0)
            {
                using (SolidBrush mask = new SolidBrush(Color.FromArgb(OverlayAlpha, 0, 0, 0)))
                    g.FillRectangle(mask, ClientRectangle);
            }
        }

        /// <summary>空状态卡片：半透明圆角底 + 插画 + 标题 + 说明（参照 To Do 的 "Focus on your day"）。</summary>
        private void DrawEmptyState(Graphics g)
        {
            int cardW = 252;
            int cardH = 206;
            if (cardW > ClientSize.Width - 16) cardW = Math.Max(120, ClientSize.Width - 16);

            int x = Math.Max(4, (ClientSize.Width - cardW) / 2);
            int y = Math.Max(8, (ClientSize.Height - cardH) / 2 - 24);
            Rectangle card = new Rectangle(x, y, cardW, cardH);

            Design.FillRound(g, card, 8, Color.FromArgb(94, 0, 0, 0));

            Rectangle art = new Rectangle(card.X + (card.Width - 92) / 2, card.Y + 24, 92, 68);
            Icons.EmptyDay(g, art);

            Design.DrawText(g, EmptyTitle, UiFont.Get(11f, FontStyle.Bold), Color.White,
                new Rectangle(card.X + 12, art.Bottom + 12, card.Width - 24, 24), ContentAlignment.MiddleCenter);

            if (!string.IsNullOrEmpty(EmptyHint))
            {
                TextRenderer.DrawText(g, EmptyHint, UiFont.Get(8.5f),
                    new Rectangle(card.X + 22, art.Bottom + 40, card.Width - 44, 52),
                    Color.FromArgb(236, 236, 240),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top
                    | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        private void DrawGroupHeader(Graphics g, Rectangle r, string name, int count, bool open)
        {
            Color ink = OnBackgroundInk.A == 0 ? Design.InkSoft : OnBackgroundInk;
            Color faint = Color.FromArgb(170, ink);

            Design.DrawText(g, name, UiFont.Get(9f, FontStyle.Bold), ink,
                new Rectangle(r.X + 4, r.Y, r.Width - 120, r.Height), ContentAlignment.MiddleLeft);
            Size size = TextRenderer.MeasureText(name, UiFont.Get(9f, FontStyle.Bold));
            Design.DrawText(g, count.ToString(), UiFont.Get(9f), faint,
                new Rectangle(r.X + 4 + size.Width + 10, r.Y, 60, r.Height), ContentAlignment.MiddleLeft);
            Icons.Chevron(g, new Point(r.X + 4 + size.Width + 56, r.Y + r.Height / 2), open ? 0 : 1, faint);
        }

        private void DrawCard(Graphics g, int index, TodoItem item)
        {
            Rectangle r = CardRect(index);
            if (r.Bottom < -4 || r.Top > ClientSize.Height + 4) return;

            Color accent = AccentColor.A == 0 ? Design.BrandPurple : AccentColor;
            bool hover = index == _hover;

            Design.Card(g, r, Design.CardRadius, hover ? Design.CardHover : Design.CardFill, Design.CardBorder);

            Rectangle check = new Rectangle(r.X + 14, r.Y + (r.Height - 22) / 2, 22, 22);
            if (item.Done) Icons.CheckFilled(g, check, accent);
            else Icons.Ring(g, check, Design.CheckRing);

            // 右侧元信息从右往左排：⋯ → 到期日 → 星标
            int rightX = r.Right - 16;
            Icons.Dots(g, new Point(rightX - 8, r.Y + r.Height / 2), Color.FromArgb(190, 188, 194));
            rightX -= 30;

            if (item.DueDay != 0)
            {
                string due = item.DueText;
                Size dueSize = TextRenderer.MeasureText(due, UiFont.Get(8.5f));
                Color dueColor = item.IsOverdue || item.IsDueToday ? Design.DueToday : Design.InkSoft;
                Design.DrawText(g, due, UiFont.Get(8.5f), dueColor,
                    new Rectangle(rightX - dueSize.Width, r.Y, dueSize.Width, r.Height), ContentAlignment.MiddleRight);
                rightX -= dueSize.Width + 14;
            }

            if (item.Priority >= 2)
            {
                Icons.Star(g, new Rectangle(rightX - 16, r.Y + (r.Height - 16) / 2, 16, 16), Design.Important, true);
                rightX -= 24;
            }

            int titleLeft = check.Right + 12;
            Rectangle titleRect = new Rectangle(titleLeft, r.Y, Math.Max(20, rightX - titleLeft), r.Height);
            Color titleColor = item.Done ? Design.InkFaint : Design.Ink;
            Font font = item.Done ? _doneFont : _font;
            Design.DrawText(g, item.Title, font, titleColor, titleRect, ContentAlignment.MiddleLeft);
        }

        // ---------- 命中区域 ----------

        private Rectangle CheckRect(int index)
        {
            Rectangle r = CardRect(index);
            return new Rectangle(r.X + 14, r.Y + (r.Height - 22) / 2, 22, 22);
        }

        private Rectangle StarRect(int index)
        {
            Rectangle r = CardRect(index);
            return new Rectangle(r.Right - 16 - 30 - 16, r.Y + (r.Height - 16) / 2, 16, 16);
        }

        private Rectangle DotsRect(int index)
        {
            Rectangle r = CardRect(index);
            return new Rectangle(r.Right - 40, r.Y + (r.Height - 24) / 2, 32, 24);
        }

        private enum HitKind { None, ActiveItem, DoneItem, ActiveHeader, DoneHeader }

        private HitKind HitTest(Point p, out int index)
        {
            index = -1;
            if (_activeHeaderH > 0 && ActiveHeaderRect().Contains(p)) return HitKind.ActiveHeader;
            if (_doneHeaderH > 0 && DoneHeaderRect().Contains(p)) return HitKind.DoneHeader;

            for (int i = 0; i < _items.Count; i++)
            {
                if (!CardRect(i).Contains(p)) continue;
                index = i;
                return i < _activeCount ? HitKind.ActiveItem : HitKind.DoneItem;
            }
            return HitKind.None;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index;
            HitKind kind = HitTest(e.Location, out index);

            int hover = (kind == HitKind.ActiveItem || kind == HitKind.DoneItem) ? index : -1;
            if (hover != _hover)
            {
                _hover = hover;
                Invalidate();
            }
            Cursor = kind == HitKind.None ? Cursors.Default : Cursors.Hand;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1)
            {
                _hover = -1;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            // 遮罩期间（收起态滑出菜单）：点到这里算点遮罩，不处理列表里的任何操作
            if (OverlayAlpha > 0)
            {
                if (e.Button == MouseButtons.Left)
                {
                    EventHandler masked = MaskClicked;
                    if (masked != null) masked(this, EventArgs.Empty);
                }
                return;
            }

            int index;
            HitKind kind = HitTest(e.Location, out index);

            if (kind == HitKind.ActiveHeader || kind == HitKind.DoneHeader)
            {
                bool doneGroup = kind == HitKind.DoneHeader;
                if (e.Button == MouseButtons.Right)
                {
                    EventHandler menuHandler = GroupMenuRequested;
                    if (menuHandler != null) menuHandler(this, new GroupEventArgs(doneGroup));
                    return;
                }
                if (e.Button == MouseButtons.Left)
                {
                    EventHandler handler = GroupToggled;
                    if (handler != null) handler(this, new GroupEventArgs(doneGroup));
                }
                return;
            }

            if (kind == HitKind.None)
            {
                // 空白处右键 → 交给主窗口弹「清空全部 / 清空已完成」
                if (e.Button == MouseButtons.Right)
                {
                    EventHandler blank = BackgroundContextRequested;
                    if (blank != null) blank(this, EventArgs.Empty);
                }
                return;
            }

            TodoItem item = _items[index];
            _selected = index;
            Focus();

            if (e.Button == MouseButtons.Right)
            {
                ItemContextRequestedEventArgs args = new ItemContextRequestedEventArgs(item, e.Location);
                EventHandler handler = ItemContextRequested;
                if (handler != null) handler(args, EventArgs.Empty);
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            if (DotsRect(index).Contains(e.Location)
                || (item.Priority >= 2 && StarRect(index).Contains(e.Location)))
            {
                ItemContextRequestedEventArgs args = new ItemContextRequestedEventArgs(item, e.Location);
                EventHandler handler = ItemContextRequested;
                if (handler != null) handler(args, EventArgs.Empty);
                return;
            }

            if (CheckRect(index).Contains(e.Location))
            {
                EventHandler handler = ItemToggled;
                if (handler != null) handler(item, EventArgs.Empty);
                return;
            }

            ItemActivatedEventArgs activated = new ItemActivatedEventArgs(item, e.Location, false);
            EventHandler activate = ItemActivated;
            if (activate != null) activate(activated, EventArgs.Empty);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int index;
            HitKind kind = HitTest(e.Location, out index);
            if (kind != HitKind.ActiveItem && kind != HitKind.DoneItem) return;

            ItemActivatedEventArgs args = new ItemActivatedEventArgs(_items[index], e.Location, true);
            EventHandler handler = ItemActivated;
            if (handler != null) handler(args, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            int max = Math.Max(0, _contentHeight - ClientSize.Height);
            if (max <= 0)
            {
                if (_scrollY != 0) { _scrollY = 0; Invalidate(); }
                return;
            }

            int value = _scrollY - e.Delta;
            if (value < 0) value = 0;
            if (value > max) value = max;
            if (value == _scrollY) return;

            _scrollY = value;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_selected < 0 || _selected >= _items.Count) return;

            if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                DeleteRequestedArgs args = new DeleteRequestedArgs(_items[_selected]);
                EventHandler handler = ItemDeleteRequested;
                if (handler != null) handler(args, EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.Space)
            {
                e.SuppressKeyPress = true;
                EventHandler handler = ItemToggled;
                if (handler != null) handler(_items[_selected], EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.F2)
            {
                e.SuppressKeyPress = true;
                ItemActivatedEventArgs args = new ItemActivatedEventArgs(_items[_selected], Point.Empty, true);
                EventHandler handler = ItemActivated;
                if (handler != null) handler(args, EventArgs.Empty);
            }
        }
    }

    /// <summary>右键/更多按钮请求：携带被点击的任务与鼠标位置。</summary>
    public class ItemContextRequestedEventArgs : EventArgs
    {
        public TodoItem Item;
        public Point Location;

        public ItemContextRequestedEventArgs(TodoItem item, Point location)
        {
            Item = item;
            Location = location;
        }
    }

    /// <summary>点击任务行：单击打开、双击进入编辑。</summary>
    public class ItemActivatedEventArgs : EventArgs
    {
        public TodoItem Item;
        public Point Location;
        public bool DoubleClick;

        public ItemActivatedEventArgs(TodoItem item, Point location, bool doubleClick)
        {
            Item = item;
            Location = location;
            DoubleClick = doubleClick;
        }
    }

    /// <summary>键盘删除请求。</summary>
    public class DeleteRequestedArgs : EventArgs
    {
        public TodoItem Item;

        public DeleteRequestedArgs(TodoItem item)
        {
            Item = item;
        }
    }

    /// <summary>分组事件：DoneGroup 为 true 表示「已完成」组。</summary>
    public class GroupEventArgs : EventArgs
    {
        public bool DoneGroup;

        public GroupEventArgs(bool doneGroup)
        {
            DoneGroup = doneGroup;
        }
    }

    /// <summary>任务列表里的分组类型。</summary>
    public enum TaskGroupKind
    {
        None,
        Active,
        Done
    }
}
