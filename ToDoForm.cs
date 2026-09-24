using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>
    /// 主窗口：左侧边栏（今天 / 重要 / 任务 / 列表）+ 右侧任务区。
    /// 参照微软 To Do 的界面组织；所有数据都在本地 todos.json。
    /// </summary>
    public class ToDoForm : Form
    {
        private const int Edge = 6;
        private const int AddBarHeight = 46;

        private readonly Store _store;
        private readonly string _userName = Environment.UserName;

        private SidebarPanel _sidebar;
        private TaskListPanel _tasks;
        private TextBox _newTask;
        private bool _newTaskActive;
        /// <summary>输入条右侧星星是否点亮（点亮的任务会带重要标记）。</summary>
        private bool _newTaskImportant;
        /// <summary>点星星引发的一次性失焦：这次失焦不提交、不清空已输入的内容。</summary>
        private bool _suppressCommit;
        /// <summary>收起（PIN）态：没有侧栏、窄条、无标题栏、永远置顶。</summary>
        private bool _pinned;
        private Rectangle _pinRestoreBounds;
        private Size _pinRestoreMin = new Size(760, 480);
        private Size _pinRestoreMax = Size.Empty;
        private bool _pinRestoreTopMost;
        private FormWindowState _pinRestoreState = FormWindowState.Normal;
        private FormBorderStyle _pinRestoreBorder = FormBorderStyle.Sizable;
        private Timer _flyoutTimer;
        private bool _flyoutOpen;

        /// <summary>侧栏滑出时压暗内容的遮罩透明度。</summary>
        private const int OverlayAlphaValue = 110;
        private string _viewId = ViewIds.MyDay;
        private bool _forceClose;
        private TodoItem _editingItem;

        // 标题区文字缓存（RefreshContent 里算好，OnPaint 直接用，避免每次重绘重复计算）
        private string _headerTitle = string.Empty;
        private string _headerSubtitle = string.Empty;

        // 当前视图的外观缓存（主色 + 背景）
        private Color _viewColor = Design.BrandPurple;
        private int _bgKind = -1;
        private string _bgValue = string.Empty;

        public ToDoForm(Store store)
        {
            _store = store;
            Build();
            _store.Changed += Store_Changed;
            ApplySettings();

            _viewId = string.IsNullOrEmpty(_store.Settings.LastView) ? ViewIds.MyDay : _store.Settings.LastView;
            // 必须先建好侧栏节点，否则 FindNode 查不到、会上次视图失效而永远兜底到「今天」
            _sidebar.Build(_store);
            if (FindNode(_viewId) == null) _viewId = ViewIds.MyDay;
            _sidebar.ActiveView = _viewId;
            RefreshContent();
        }

        // ---------- 构建 ----------

        private void Build()
        {
            Text = "便签 TODO";
            Font = UiFont.Get(9.5f);
            ClientSize = new Size(1000, 640);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.Manual;
            BackColor = Design.Sidebar;   // 内容区左上角圆角挖掉的那一小块会露出这个色（与侧栏一致）
            KeyPreview = true;
            DoubleBuffered = true;
            // 最大化/缩放时强制整窗重绘，否则旧位置的图标与文字会残留（"复制一份"）
            SetStyle(ControlStyles.ResizeRedraw, true);
            try { Icon = IconFactory.Create(0); }
            catch { }

            _sidebar = new SidebarPanel();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = Design.SidebarWidth;
            _sidebar.ViewSelected += delegate
            {
                OnViewChanged(_sidebar.ActiveView);
                if (_pinned && _flyoutOpen) ToggleFlyout();   // 收起态滑出的侧栏：选完视图就收回
            };
            // 点在侧栏空白处（含被侧栏盖住的 ☰ 位置）也收起
            _sidebar.BlankClicked += delegate { if (_pinned && _flyoutOpen) ToggleFlyout(); };
            // 点顶部数据库图标 → 打开数据存储目录
            _sidebar.StorageClicked += delegate { OpenStorageFolder(); };
            _sidebar.NewListRequested += delegate
            {
                if (_pinned && _flyoutOpen) ToggleFlyout();   // 收起态：菜单收起后再开新建列表
                NewList();
            };
            _sidebar.ListContextRequested += Sidebar_ListContext;

            _tasks = new TaskListPanel();
            // 注意：不能用 Dock —— 停靠布局会覆盖 LayoutChildren 里的 SetBounds，
            // 导致列表控件铺满整块区域、把标题栏与添加条盖住。改为绝对定位。
            _tasks.ItemToggled += delegate (object s, EventArgs e) { ToggleTask(s as TodoItem); };
            _tasks.ItemActivated += Tasks_ItemActivated;
            _tasks.ItemContextRequested += Tasks_Context;
            _tasks.ItemDeleteRequested += delegate (object s, EventArgs e)
            {
                DeleteRequestedArgs args = s as DeleteRequestedArgs;
                if (args != null) DeleteTask(args.Item);
            };
            _tasks.GroupToggled += delegate (object s, EventArgs e)
            {
                GroupEventArgs args = e as GroupEventArgs;
                _tasks.ToggleGroup(args != null && args.DoneGroup);
            };
            _tasks.GroupMenuRequested += Group_Menu;
            _tasks.BackgroundContextRequested += Tasks_BackgroundMenu;
            // 点遮罩（遮罩主要盖在列表控件上，由它转发过来）→ 收起滑出的菜单
            _tasks.MaskClicked += delegate { if (_pinned && _flyoutOpen) ToggleFlyout(); };
            _tasks.BackgroundPainter += PaintTasksBackground;

            _newTask = new TextBox();
            _newTask.BorderStyle = BorderStyle.None;
            _newTask.BackColor = Design.CardFill;
            _newTask.ForeColor = Design.Ink;
            _newTask.Font = UiFont.Get(11f);
            _newTask.Visible = false;
            _newTask.KeyDown += NewTask_KeyDown;
            _newTask.LostFocus += delegate { if (_suppressCommit) return; CommitNewTask(); };

            // 拖拽：把别的窗口里的文本 / 图片 / 文件拖进来
            EnableDrop(this);
            EnableDrop(_newTask);
            EnableDrop(_tasks);
            EnableDrop(_sidebar);

            Controls.Add(_tasks);
            Controls.Add(_newTask);
            Controls.Add(_sidebar);
            _sidebar.SendToBack();
        }

        // ---------- 对外 ----------

        /// <summary>显示窗口并激活（开机 / 托盘 / 单实例唤醒）。</summary>
        public void ShowAndActivate(bool focusNewTask)
        {
            ApplySettings();

            AppSettings settings = _store.Settings;
            Point wanted;
            if (IsUsableLocation(settings.WindowX, settings.WindowY))
                wanted = new Point(settings.WindowX, settings.WindowY);
            else
                wanted = DefaultLocation();

            Location = wanted;

            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Native.ActivateWindow(Handle);

            // 启动后强制整窗重绘一次：否则偶尔会有一块区域要等到第一次缩放/最大化才被画出来
            Refresh();

            if (focusNewTask) BeginEditNewTask();
        }

        public void HideToTray()
        {
            SaveBounds();
            Hide();
        }

        public void ForceClose()
        {
            _forceClose = true;
            SaveBounds();
            Close();
        }

        public void SaveBounds()
        {
            if (WindowState != FormWindowState.Normal) return;

            // 收起（PIN）态下记的是收起前的正常尺寸，免得下次启动变成窄条
            Rectangle bounds = _pinned ? _pinRestoreBounds : Bounds;

            AppSettings settings = _store.Settings;
            settings.WindowX = bounds.Left;
            settings.WindowY = bounds.Top;
            settings.WindowW = bounds.Width;
            settings.WindowH = bounds.Height;
            settings.LastView = _viewId;
            _store.Save();
        }

        private Point DefaultLocation()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = Math.Min(1000, Math.Max(760, wa.Width - 120));
            int h = Math.Min(660, Math.Max(480, wa.Height - 100));
            Width = w;
            Height = h;

            int x = wa.Left + (wa.Width - w) / 2;
            int y = wa.Top + (wa.Height - h) / 2;
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            if (x + w > wa.Right) x = wa.Right - w;
            if (y + h > wa.Bottom) y = wa.Bottom - h;
            return new Point(x, y);
        }

        private static bool IsUsableLocation(int x, int y)
        {
            if (x < 0 || y < 0) return false;   // -1 是「还没保存过」的默认值
            try
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    Rectangle wa = screen.WorkingArea;
                    if (wa.Width <= 0 || wa.Height <= 0) continue;
                    if (x + 80 > wa.Left && x + 80 < wa.Right && y + 20 > wa.Top && y + 20 < wa.Bottom)
                        return true;
                }
            }
            catch { }
            return false;
        }

        private void ApplySettings()
        {
            AppSettings settings = _store.Settings;
            TopMost = settings.OnTop;

            float opacity = settings.Opacity / 100f;
            if (opacity < 0.3f) opacity = 0.3f;
            if (opacity > 1f) opacity = 1f;
            try { Opacity = opacity; }
            catch { }

            if (settings.WindowW >= MinimumSize.Width && settings.WindowH >= MinimumSize.Height)
            {
                Size size = new Size(settings.WindowW, settings.WindowH);
                if (Size != size) Size = size;
            }
        }

        // ---------- 视图与标题 ----------

        private SidebarNode FindNode(string viewId)
        {
            foreach (SidebarNode node in _sidebar.Nodes)
                if (!node.IsHeader && !node.IsNewList && node.ViewId == viewId) return node;
            return null;
        }

        private void OnViewChanged(string viewId)
        {
            CommitNewTask();   // 切视图时收起输入框，恢复默认显示
            _viewId = viewId;
            _store.Settings.LastView = viewId;
            _store.Save();
            RefreshContent();
        }

        /// <summary>外部切换视图（托盘菜单用）。</summary>
        public void ActivateView(string viewId)
        {
            if (FindNode(viewId) == null) return;
            _viewId = viewId;
            _sidebar.ActiveView = viewId;
            _store.Settings.LastView = viewId;
            _store.Save();
            RefreshContent();
        }

        private void ViewInfo(out string title, out string subtitle, out Color color)        {
            if (_viewId == ViewIds.MyDay)
            {
                title = "今天";
                subtitle = TodayText();   // 标题下方显示今天日期
                color = Design.MyDayColor;
                return;
            }
            if (_viewId == ViewIds.Important)
            {
                title = "重要";
                subtitle = string.Empty;   // 这两项不显示副标题小字
                color = Design.ImportantColor;
                return;
            }
            if (_viewId == ViewIds.Planned)
            {
                title = "计划";
                subtitle = string.Empty;
                color = Design.PlannedColor;
                return;
            }
            if (_viewId == ViewIds.Done)
            {
                title = "任务";
                subtitle = string.Empty;
                color = Design.DoneColor;
                return;
            }

            TodoList list = _store.FindList(_viewId);
            if (list != null)
            {
                title = list.Name;
                subtitle = string.Empty;   // 列表标题下不再显示小字
                color = list.Color;
                return;
            }

            title = "任务";
            subtitle = string.Empty;
            color = Design.BrandPurple;
        }

        // ---------- 内容刷新 ----------

        private void Store_Changed(object sender, EventArgs e)
        {
            RefreshContent();
            TopMost = _store.Settings.OnTop;
        }

        public void RefreshContent()
        {
            // 先解析当前视图的外观（每个版块都能有自己的背景）
            _store.ResolveViewStyle(_viewId, out _viewColor, out _bgKind, out _bgValue);

            _sidebar.Build(_store);
            _sidebar.ActiveView = _viewId;

            string title, subtitle;
            Color color;
            ViewInfo(out title, out subtitle, out color);

            List<TodoItem> active;
            List<TodoItem> done;
            string activeHeader;
            string doneHeader;

            if (_viewId == ViewIds.Done)
            {
                // 「已完成」视图：按状态分两组，覆盖所有列表
                active = _store.Query(ViewIds.Tasks, false);
                done = _store.Query(ViewIds.Tasks, true);
                activeHeader = active.Count > 0 ? "未完成" : string.Empty;
                doneHeader = done.Count > 0 ? "已完成" : string.Empty;
            }
            else
            {
                active = _store.Query(_viewId, false);
                done = _store.Query(_viewId, true);
                activeHeader = string.Empty;
                doneHeader = done.Count > 0 ? "已完成" : string.Empty;
            }

            _tasks.AccentColor = _viewColor;
            _headerTitle = title;
            _headerSubtitle = subtitle;

            _tasks.SetGroups(active, done, activeHeader, doneHeader);
            _tasks.OnBackgroundInk = Design.ContrastOn(HeaderSampleColor());

            ApplyEmptyState();

            LayoutChildren();
            Invalidate();
        }

        /// <summary>空状态文案：收起（PIN）态的小窗里不显示。</summary>
        private void ApplyEmptyState()
        {
            if (_pinned)
            {
                _tasks.EmptyTitle = string.Empty;
                _tasks.EmptyHint = string.Empty;
                return;
            }

            string title, hint;
            EmptyStateFor(_viewId, out title, out hint);
            _tasks.EmptyTitle = title;
            _tasks.EmptyHint = hint;
        }

        /// <summary>各视图的空状态文案（列表为空时显示在中间卡片上）。</summary>
        private static void EmptyStateFor(string viewId, out string title, out string hint)
        {
            if (viewId == ViewIds.MyDay)
            {
                title = "专注今天";
                hint = "把今天要做的事放进来，每天会自动刷新。";
                return;
            }
            if (viewId == ViewIds.Important)
            {
                title = "还没有重要的事";
                hint = "给任务点上星标，就会汇总到这里。";
                return;
            }
            if (viewId == ViewIds.Planned)
            {
                title = "还没有安排时间的事";
                hint = "给任务设置到期日，就能在这里按时间查看。";
                return;
            }
            if (viewId == ViewIds.Done)
            {
                title = "还没有任务";
                hint = "在任意列表添加任务后，都会汇总到这里。";
                return;
            }

            title = "这个列表还是空的";
            hint = "在下方输入任务，按回车即可添加。";
        }

        private void LayoutChildren()
        {
            // 构造期间设置 ClientSize 会触发 Resize，此时子控件还没建好
            if (_sidebar == null || _tasks == null || _newTask == null) return;

            int pad = Design.ContentPad;
            int contentX = ContentX + pad;
            int contentW = Math.Max(120, ClientSize.Width - contentX - pad);

            int listTop = HeaderHeightPx + 8;
            int listBottom = AddBarRect().Top - 10;

            _tasks.SetBounds(contentX, listTop, contentW,
                Math.Max(60, listBottom - listTop));

            // 背景映射：内容区在列表控件坐标系里的位置（保证与标题区背景接得上）
            Rectangle mapping = ContentRect();
            mapping.Offset(-_tasks.Left, -_tasks.Top);
            _tasks.BackgroundMapping = mapping;

            if (_newTask.Visible)
            {
                Rectangle bar = AddBarRect();
                // 输入框底色与输入条同为白色，聚焦时不会出现"窄窄一条"；
                // 右端留出 44px 给「重要」星星，避免文本框盖住它。
                _newTask.SetBounds(bar.X + 48, bar.Y + (bar.Height - _newTask.PreferredHeight) / 2,
                    Math.Max(40, bar.Width - 48 - 44), _newTask.PreferredHeight);
            }
        }

        /// <summary>底部「添加任务」条：与任务行同宽、四周留边（不贯穿底部）。</summary>
        private Rectangle AddBarRect()
        {
            int pad = Design.ContentPad;
            int contentX = ContentX + pad;
            int contentW = Math.Max(120, ClientSize.Width - contentX - pad);
            int h = AddBarHeight;
            return new Rectangle(contentX, Math.Max(0, ClientSize.Height - 14 - h), contentW, h);
        }

        /// <summary>添加任务条最右侧的「重要」星星按钮。</summary>
        private Rectangle AddBarStarRect()
        {
            Rectangle bar = AddBarRect();
            return new Rectangle(bar.Right - 40, bar.Y + (bar.Height - 26) / 2, 26, 26);
        }

        private Rectangle HeaderRect()
        {
            int contentX = ContentX;
            return new Rectangle(contentX, 0, Math.Max(60, ClientSize.Width - contentX), HeaderHeightPx);
        }

        /// <summary>
        /// 内容区左边界：收起（PIN）态没有侧栏（滑出的侧栏是浮层），从 0 开始；正常态就是侧栏右边缘。
        /// 注意：这里不能用 _sidebar.Visible 判断 —— 窗口还没 Show 出来时 Visible 恒为 false，
        /// 启动时会误判成「没有侧栏」，把内容区摆到左边去（表现为启动后有一块矩形错位）。
        /// </summary>
        private int ContentX
        {
            get { return (_pinned || _sidebar == null) ? 0 : _sidebar.Right; }
        }

        /// <summary>标题区高度：正常 72，收起态高一些（左上角有 ☰，标题在它下面）。</summary>
        private int HeaderHeightPx
        {
            get { return _pinned ? 104 : Design.HeaderHeight; }
        }

        /// <summary>右侧内容区整块范围：背景映射以它为基准，保证标题区/列表区/添加条区连续。</summary>
        private Rectangle ContentRect()
        {
            int contentX = ContentX;
            return new Rectangle(contentX, 0, Math.Max(60, ClientSize.Width - contentX), ClientSize.Height);
        }

        /// <summary>标题区右上角两个图标：[0] = 置顶（PIN，带底块），[1] = 三个点（透明底、白色）。</summary>
        private Rectangle[] HeaderIconRects()
        {
            Rectangle header = HeaderRect();
            const int size = 28;
            const int gap = 10;
            int y;
            if (_pinned)
            {
                int lineH = TextRenderer.MeasureText(_headerTitle, UiFont.Get(22f, FontStyle.Bold)).Height;
                y = header.Y + 50 + (lineH - size) / 2;
            }
            else
            {
                y = header.Y + (header.Height - size) / 2;
            }

            Rectangle dots = new Rectangle(header.Right - 20 - size, y, size, size);
            Rectangle pin = new Rectangle(dots.Left - gap - size, y, size, size);
            return new Rectangle[] { pin, dots };
        }

        /// <summary>收起态左上角的 ☰。</summary>
        private Rectangle PinMenuRect()
        {
            Rectangle header = HeaderRect();
            return new Rectangle(header.X + 20, header.Y + 16, 24, 24);
        }

        /// <summary>收起态右上角的关闭按钮。</summary>
        private Rectangle PinCloseRect()
        {
            Rectangle header = HeaderRect();
            return new Rectangle(header.Right - 20 - 24, header.Y + 16, 24, 24);
        }

        /// <summary>收起态的拖拽区：☰ 所在那一整行（去掉 ☰ 与关闭按钮）。</summary>
        private Rectangle PinDragRect()
        {
            Rectangle header = HeaderRect();
            return new Rectangle(header.X + 4, header.Y + 4, Math.Max(20, header.Width - 8), 48);
        }

        // ---------- 绘制（标题栏 + 添加任务条） ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 铺内容区背景。列表控件会自己画它那一块，所以这里把内容区扣掉列表所占的矩形再画，
            // 保证：① 每个像素只画一次（否则淡雾会叠深）② 列表左右两侧的竖带也铺上背景。
            Rectangle content = ContentRect();
            Region oldClip = g.Clip;
            using (Region region = new Region(content))
            {
                region.Exclude(new Rectangle(_tasks.Left, _tasks.Top,
                    Math.Max(0, _tasks.Width), Math.Max(0, _tasks.Height)));

                g.SetClip(region, System.Drawing.Drawing2D.CombineMode.Intersect);
                PaintListBackground(g, content, content);

                if (oldClip != null) g.Clip = oldClip;
                else g.ResetClip();
            }

            // 左上圆角：用抗锯齿的路径把圆角外侧那一小块盖成底色，边缘才不会有锯齿
            // （收起态是无边框小窗，角就是方的，不做挖角）
            if (!_pinned) PaintCornerCutout(g, content, 14);

            Rectangle header = HeaderRect();

            Color ink = Design.ContrastOn(HeaderSampleColor());
            Font titleFont = UiFont.Get(22f, FontStyle.Bold);
            int titleLineH = TextRenderer.MeasureText(_headerTitle, titleFont).Height;
            // 正常态：标题顶在 12；收起态：左上角先放一个 ☰，标题挪到它下面
            int titleTop = _pinned ? header.Y + 50 : header.Y + 12;
            int titleLeft;

            if (_pinned)
            {
                Icons.Menu(g, PinMenuRect(), ink);
                titleLeft = header.X + 20;
            }
            else
            {
                const int iconSize = 32;
                Rectangle headIcon = new Rectangle(header.X + 30,
                    titleTop + titleLineH / 2 - iconSize / 2, iconSize, iconSize);
                Icons.ByKind(g, headIcon, HeaderIconKind(), ink);
                titleLeft = headIcon.Right + 12;
            }

            Design.DrawText(g, _headerTitle, titleFont, ink,
                new Rectangle(titleLeft, titleTop, Math.Max(60, header.Right - 16 - titleLeft), titleLineH),
                ContentAlignment.MiddleLeft);
            if (!string.IsNullOrEmpty(_headerSubtitle))
            {
                Color soft = Color.FromArgb(215, ink);
                Design.DrawText(g, _headerSubtitle, UiFont.Get(9.5f), soft,
                    new Rectangle(titleLeft + 2, titleTop + titleLineH + 5, Math.Max(60, header.Right - 16 - titleLeft), 16),
                    ContentAlignment.MiddleLeft);
            }

            // 右上角两个图标：置顶（PIN）+ 齿轮（透明底、白色）
            // PIN：没激活时只有图标，激活（收起态）后才带底块
            Rectangle[] icons = HeaderIconRects();
            Color glyph = Color.FromArgb(46, 46, 48);
            if (_pinned)
            {
                Icons.IconChip(g, icons[0], Color.FromArgb(235, 255, 255, 255));
                Icons.ByKind(g, Deflate(icons[0], 5), Icons.PinKind, Design.BrandPurple);
            }
            else
            {
                Icons.ByKind(g, Deflate(icons[0], 2), Icons.PinKind, Color.White);
            }
            Icons.Gear(g, Deflate(icons[1], 2), Color.White);

            // 收起态顶部那一行：☰ / 关闭（这一行除两个按钮外都能拖着窗口走）
            if (_pinned)
            {
                Icons.Menu(g, PinMenuRect(), ink);
                Icons.Close(g, PinCloseRect(), ink);
            }

            // 收起态滑出侧栏时，右侧内容压一层遮罩
            if (_pinned && _flyoutOpen)
            {
                Region oldClip2 = g.Clip;
                using (Region shade = new Region(ClientRectangle))
                {
                    shade.Exclude(_tasks.Bounds);
                    g.SetClip(shade, System.Drawing.Drawing2D.CombineMode.Intersect);
                    using (SolidBrush mask = new SolidBrush(Color.FromArgb(OverlayAlphaValue, 0, 0, 0)))
                        g.FillRectangle(mask, ClientRectangle);
                    if (oldClip2 != null) g.Clip = oldClip2;
                    else g.ResetClip();
                }
            }

            // 添加任务条：与任务行同宽的纯白圆角条（不是通栏），左侧圆圈与任务行的勾选框一致。
            // 输入框底色同为白色，聚焦后整条看上去都是输入区，不会出现"窄窄一条"。
            // 最右侧是一颗星，用来把即将添加的任务标记为重要。
            Rectangle bar = AddBarRect();
            using (Pen sep = new Pen(Color.FromArgb(70, 255, 255, 255)))
                g.DrawLine(sep, content.Left, bar.Top - 10, content.Right, bar.Top - 10);

            Design.FillRound(g, bar, 8, Design.CardFill);
            Icons.Ring(g, new Rectangle(bar.X + 16, bar.Y + (bar.Height - 22) / 2, 22, 22), Design.CheckRing);

            Rectangle star = AddBarStarRect();
            Icons.Star(g, star, _newTaskImportant ? Design.Important : Color.FromArgb(176, 174, 182), _newTaskImportant);

            if (!_newTaskActive)
            {
                Design.DrawText(g, "添加任务", UiFont.Get(11f), Color.FromArgb(140, 138, 146),
                    new Rectangle(bar.X + 48, bar.Y, Math.Max(60, bar.Width - 100), bar.Height),
                    ContentAlignment.MiddleLeft);
            }
        }

        /// <summary>把内容区左上角圆角外侧的小三角用窗体底色盖掉（抗锯齿填充，无锯齿）。</summary>
        private void PaintCornerCutout(Graphics g, Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            // 外沿故意超出客户区 s 像素：GDI+ 开抗锯齿时，若路径的边正好压在客户区第一行/第一列上，
            // 该行(列)只会被覆盖约半格，看上去就是一条淡淡的“边框”。外扩后这两条边落在画面外，
            // 第一行/第一列变成实心，只有圆弧处保留抗锯齿的柔边。
            const int s = 2;
            using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(r.Left, r.Top, d, d, 180, 90);
                path.AddLine(r.Left + radius, r.Top, r.Left + radius, r.Top - s);
                path.AddLine(r.Left + radius, r.Top - s, r.Left - s, r.Top - s);
                path.AddLine(r.Left - s, r.Top - s, r.Left - s, r.Top + radius + s);
                path.CloseFigure();

                System.Drawing.Drawing2D.SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    g.FillPath(brush, path);
                }
                g.SmoothingMode = old;
            }
        }

        /// <summary>标题区图标：内置视图用固定图标，自定义列表用列表自己选的图标。</summary>
        private int HeaderIconKind()
        {
            if (_viewId == ViewIds.MyDay) return 6;   // 太阳字形
            if (_viewId == ViewIds.Important) return 1;
            if (_viewId == ViewIds.Planned) return 2;
            if (_viewId == ViewIds.Done) return 5;
            TodoList list = _store.FindList(_viewId);
            return list != null ? list.IconKind : 0;
        }

        private static Rectangle Deflate(Rectangle r, int pad)
        {
            return new Rectangle(r.X + pad, r.Y + pad,
                Math.Max(2, r.Width - pad * 2), Math.Max(2, r.Height - pad * 2));
        }

        private static Point Center(Rectangle r)
        {
            return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        }

        /// <summary>「9月24日, 星期四」这样的今天日期文案。</summary>
        private static readonly string[] WeekNames = new string[]
        {
            "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"
        };

        private static string TodayText()
        {
            DateTime today = DateTime.Today;
            return string.Format("{0}月{1}日, {2}", today.Month, today.Day, WeekNames[(int)today.DayOfWeek]);
        }

        // ---------- 背景（主题） ----------

        /// <summary>当前视图用于背景的基色。</summary>
        private Color BaseBackgroundColor()
        {
            return _viewColor;
        }

        /// <summary>背景顶部采样色，用于决定标题文字与图标的对比色。</summary>
        private Color HeaderSampleColor()
        {
            if (_bgKind == 1) return Design.Themes[ParseThemeIndex(_bgValue)].From;
            if (_bgKind <= -1)
            {
                Color from, to;
                GradientFromColor(_viewColor, out from, out to);
                return from;
            }
            return _viewColor;
        }

        /// <summary>由主题色推出一组同色系渐变的起止色。</summary>
        private static void GradientFromColor(Color baseColor, out Color from, out Color to)
        {
            from = Design.Shade(baseColor, -0.20f);
            to = Design.Shade(baseColor, 0.30f);
        }

        private static int ParseThemeIndex(string value)
        {
            int index;
            if (int.TryParse(value, out index) && index >= 0 && index < Design.Themes.Length) return index;
            return 0;
        }

        private TodoList CurrentList()
        {
            return _store.FindList(_viewId);
        }

        /// <summary>
        /// 绘制背景：纯色 / 渐变 / 图片，并叠一层淡雾保证可读。
        /// fill 是要填的区域，layout 是背景的布局基准（两者都在当前绘制目标的坐标系里）。
        /// 各区域互不重叠，避免同一处被叠两层雾。
        /// </summary>
        private void PaintListBackground(Graphics g, Rectangle fill, Rectangle layout)
        {
            if (fill.Width <= 0 || fill.Height <= 0) return;
            if (layout.Width <= 0 || layout.Height <= 0) layout = fill;

            Region saved = g.Clip;
            g.SetClip(fill, System.Drawing.Drawing2D.CombineMode.Intersect);
            try
            {
                if (_bgKind == 2 && !string.IsNullOrEmpty(_bgValue))
                {
                    Image image = LoadBackgroundImage(_bgValue);
                    if (image != null)
                    {
                        Rectangle src = FitRect(image.Width, image.Height, layout);
                        g.DrawImage(image, layout, src, GraphicsUnit.Pixel);
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(52, 0, 0, 0)))
                            g.FillRectangle(brush, layout);
                        return;
                    }
                }

                if (_bgKind == 1)
                {
                    Design.BackgroundTheme theme = Design.Themes[ParseThemeIndex(_bgValue)];
                    PaintGradient(g, layout, theme.From, theme.To);
                    using (SolidBrush brush = new SolidBrush(Design.ScrimOn(theme.From)))
                        g.FillRectangle(brush, layout);
                    return;
                }

                if (_bgKind <= -1)
                {
                    // 未设置背景：按当前视图主色做同色系渐变
                    Color from, to;
                    GradientFromColor(_viewColor, out from, out to);
                    PaintGradient(g, layout, from, to);
                    using (SolidBrush brush = new SolidBrush(Design.ScrimOn(from)))
                        g.FillRectangle(brush, layout);
                    return;
                }

                using (SolidBrush brush = new SolidBrush(_viewColor))
                    g.FillRectangle(brush, layout);
                using (SolidBrush brush = new SolidBrush(Design.ScrimOn(_viewColor)))
                    g.FillRectangle(brush, layout);
            }
            finally
            {
                if (saved != null) g.Clip = saved;
                else g.ResetClip();
            }
        }

        /// <summary>列表控件请求重绘背景时：填自己的整块客户区，布局基准用主窗口算好的映射矩形。</summary>
        private void PaintTasksBackground(Graphics g, Rectangle mapping)
        {
            PaintListBackground(g, _tasks.ClientRectangle, mapping);
        }

        private static void PaintGradient(Graphics g, Rectangle area, Color from, Color to)
        {
            if (area.Width <= 1 || area.Height <= 1) return;
            using (System.Drawing.Drawing2D.LinearGradientBrush brush =
                new System.Drawing.Drawing2D.LinearGradientBrush(area, from, to, 35f))
                g.FillRectangle(brush, area);
        }

        private Image _cachedImage;
        private string _cachedImagePath;

        private Image LoadBackgroundImage(string path)
        {
            if (_cachedImage != null && _cachedImagePath == path) return _cachedImage;
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                if (_cachedImage != null) { _cachedImage.Dispose(); _cachedImage = null; }
                using (Image raw = Image.FromFile(path))
                {
                    _cachedImage = new Bitmap(raw);
                    _cachedImagePath = path;
                }
                return _cachedImage;
            }
            catch
            {
                _cachedImage = null;
                _cachedImagePath = null;
                return null;
            }
        }

        /// <summary>按「等比填充」算出要截取的原图区域。</summary>
        private static Rectangle FitRect(int imageW, int imageH, Rectangle target)
        {
            if (imageW <= 0 || imageH <= 0) return new Rectangle(0, 0, 1, 1);

            double scale = Math.Max((double)target.Width / imageW, (double)target.Height / imageH);
            int w = Math.Max(1, (int)(target.Width / scale));
            int h = Math.Max(1, (int)(target.Height / scale));
            if (w > imageW) w = imageW;
            if (h > imageH) h = imageH;
            return new Rectangle((imageW - w) / 2, (imageH - h) / 2, w, h);
        }

        /// <summary>打开背景设置。自定义列表与内置视图都能设（内置视图存在 settings.viewStyles 里）。</summary>
        private void OpenThemePicker()
        {
            TodoList list = _store.FindList(_viewId);

            // 打开前的原始背景，取消时用它还原
            int oldKind = _bgKind;
            string oldValue = _bgValue;
            int oldColor = _viewColor.ToArgb();

            using (ThemePickerDialog dialog = new ThemePickerDialog(oldKind, oldValue, oldColor))
            {
                // 对话框里鼠标一按下就生效：立即写回并刷新（父窗口就在对话框后面，能直接看到）
                bool colorTouched = false;
                dialog.SelectionChanged += delegate
                {
                    if (dialog.Kind == 0) colorTouched = true;   // 选纯色会一并改主色
                    ApplyBackground(list, dialog.Kind, dialog.Value, dialog.ColorArgb, false);
                };

                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Applied)
                    return;   // 已经实时生效，「应用」只负责收尾关闭

                ApplyBackground(list, oldKind, oldValue, oldColor, colorTouched);   // 取消 → 还原
            }
        }

        /// <summary>把背景设置写进列表 / 内置视图，并让界面立刻刷新。</summary>
        private void ApplyBackground(TodoList list, int kind, string value, int colorArgb, bool alsoColor)
        {
            if (list != null)
            {
                _store.SetListBackground(list, kind, value);
                if (kind == 0 || alsoColor) _store.SetListColor(list, IndexOfPalette(colorArgb));
            }
            else
            {
                _store.SetViewBackground(_viewId, kind, value);
                if (kind == 0 || alsoColor) _store.SetViewColor(_viewId, colorArgb);
            }

            // 背景换过之后，图片缓存作废
            if (_cachedImage != null && _cachedImagePath != (value == null ? string.Empty : value))
            {
                _cachedImage.Dispose();
                _cachedImage = null;
                _cachedImagePath = null;
            }

            _store.RaiseChanged();
        }

        private static int IndexOfPalette(int argb)
        {
            for (int i = 0; i < Design.Palette.Length; i++)
                if (Design.Palette[i].ToArgb() == argb) return i;
            return 0;
        }

        // ---------- 新增任务 ----------

        private void BeginEditNewTask()
        {
            _newTaskActive = true;
            _newTask.Visible = true;
            _newTask.Text = string.Empty;
            LayoutChildren();
            _newTask.Focus();
            Invalidate();
        }

        private void CommitNewTask()
        {
            if (!_newTaskActive) return;
            _newTaskActive = false;

            string text = _newTask.Text.Trim();
            _newTask.Visible = false;
            _newTask.Text = string.Empty;

            if (text.Length > 0)
            {
                string listId = IsCustomView() ? _viewId : _store.DefaultListId;
                // 输入条右侧的星星亮着 → 新任务直接带重要标记
                TodoItem item = _store.Add(text, listId, _newTaskImportant ? 2 : 0);

                if (_viewId == ViewIds.MyDay) _store.SetMyDay(item, TodoItem.Today);
                else if (_viewId == ViewIds.Important) _store.SetImportant(item, true);
                else if (_viewId == ViewIds.Planned) _store.SetDueDay(item, TodoItem.Today);
            }

            Invalidate();
        }

        private bool IsCustomView()
        {
            return _viewId != ViewIds.MyDay && _viewId != ViewIds.Important
                && _viewId != ViewIds.Planned && _viewId != ViewIds.Done;
        }

        private void NewTask_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitNewTask();
                BeginEditNewTask();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _newTaskActive = false;
                _newTask.Visible = false;
                _newTask.Text = string.Empty;
                Invalidate();
            }
        }

        // ---------- 任务操作 ----------

        private void ToggleTask(TodoItem item)
        {
            if (item == null) return;
            _store.SetDone(item, !item.Done);
        }

        private void Tasks_ItemActivated(object sender, EventArgs e)
        {
            ItemActivatedEventArgs args = e as ItemActivatedEventArgs;
            if (args == null || args.Item == null) return;
            if (args.DoubleClick) BeginRename(args.Item, args.Location);
            else ShowDetails(args.Item, args.Location);
        }

        private void BeginRename(TodoItem item, Point at)
        {
            if (item == null) return;

            _editingItem = item;
            _newTaskActive = false;
            _newTask.Visible = true;
            _newTask.Text = item.Title;
            _newTask.SelectAll();
            _newTask.Focus();
            _newTask.Tag = "rename";

            _newTask.KeyDown -= RenameKeyDown;
            _newTask.KeyDown += RenameKeyDown;
            _newTask.LostFocus -= RenameLostFocus;
            _newTask.LostFocus += RenameLostFocus;

            LayoutChildren();
            _newTask.SetBounds(ContentX + Design.ContentPad + 40,
                _newTask.Top, Math.Max(60, _tasks.Width - 160), _newTask.PreferredHeight);
            Invalidate();
        }

        private void RenameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                CommitRename();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CancelRename();
            }
        }

        private void RenameLostFocus(object sender, EventArgs e)
        {
            CommitRename();
        }

        private void CommitRename()
        {
            if (_editingItem == null) return;
            TodoItem item = _editingItem;
            _editingItem = null;

            string text = _newTask.Text.Trim();
            _newTask.Visible = false;
            _newTask.Tag = null;
            _newTask.KeyDown -= RenameKeyDown;
            _newTask.LostFocus -= RenameLostFocus;

            if (text.Length > 0) _store.Update(item, text);
            else Invalidate();
        }

        private void CancelRename()
        {
            _editingItem = null;
            _newTask.Visible = false;
            _newTask.Tag = null;
            _newTask.KeyDown -= RenameKeyDown;
            _newTask.LostFocus -= RenameLostFocus;
            Invalidate();
        }

        private void Tasks_Context(object sender, EventArgs e)
        {
            ItemContextRequestedEventArgs args = e as ItemContextRequestedEventArgs;
            if (args == null || args.Item == null) return;
            ShowTaskMenu(args.Item, args.Location);
        }

        private void ShowTaskMenu(TodoItem item, Point location)
        {
            if (item == null) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiFont.Get(9.5f);

            menu.Items.Add(item.Done ? "标记为未完成" : "标记为已完成", null,
                delegate { _store.SetDone(item, !item.Done); });

            menu.Items.Add(item.Priority >= 2 ? "取消重要标记" : "标记为重要", null,
                delegate { _store.SetImportant(item, item.Priority < 2); });

            if (item.MyDay == TodoItem.Today)
            {
                menu.Items.Add("从「今天」移除", null, delegate { _store.SetMyDay(item, 0); });
            }
            else
            {
                menu.Items.Add("添加到「今天」", null, delegate { _store.SetMyDay(item, TodoItem.Today); });
            }

            menu.Items.Add(item.DueDay != 0 ? "修改到期日…" : "设置到期日…", null,
                delegate
                {
                    bool changed;
                    int day = DatePickerDialog.Ask(this, item.DueDay, out changed);
                    if (changed) _store.SetDueDay(item, day);
                });

            if (item.DueDay != 0)
            {
                menu.Items.Add("清除到期日", null, delegate { _store.SetDueDay(item, 0); });
            }

            ToolStripMenuItem move = new ToolStripMenuItem("移动到列表");
            foreach (TodoList list in _store.Lists)
            {
                TodoList target = list;
                ToolStripMenuItem child = new ToolStripMenuItem(target.Name);
                child.Checked = target.Id == item.ListId;
                child.Click += delegate { _store.MoveToList(item, target); };
                move.DropDownItems.Add(child);
            }
            menu.Items.Add(move);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除任务", null, delegate { DeleteTask(item); });

            Point screen = _tasks.PointToScreen(location);
            menu.Show(screen);
        }

        private void DeleteTask(TodoItem item)
        {
            if (item == null) return;
            if (_store.Settings.ConfirmDelete
                && MessageBox.Show(this, "确定删除这条任务？\r\n\r\n" + item.Title, "便签 TODO",
                       MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _store.Remove(item);
        }

        private void ShowDetails(TodoItem item, Point location)
        {
            if (item == null) return;
            ShowTaskMenu(item, location);
        }

        // ---------- 列表管理 ----------

        private void NewList()
        {
            int paletteIndex = _store.Lists.Count % Design.Palette.Length;
            using (ListEditorDialog dialog = new ListEditorDialog("新建列表", "新列表", 0,
                       Design.Palette[paletteIndex].ToArgb(), -1, string.Empty))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.Applied) return;

                TodoList list = _store.CreateList(dialog.ListName, paletteIndex);
                list.ColorArgb = dialog.ColorArgb;
                list.IconKind = dialog.IconKind;
                list.BackgroundKind = dialog.BackgroundKind;
                list.BackgroundValue = dialog.BackgroundValue;
                _store.AddListObject(list);

                _viewId = list.Id;
                _store.Settings.LastView = _viewId;
                _store.Save();
                RefreshContent();
            }
        }

        private void Sidebar_ListContext(object sender, EventArgs e)
        {
            SidebarNode node = sender as SidebarNode;
            if (node == null || node.List == null) return;

            TodoList list = node.List;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiFont.Get(9.5f);

            menu.Items.Add("编辑列表…", null, delegate
            {
                // 编辑过程中选图标 / 点颜色 / 点背景都会即时作用到这个列表上，「取消」时用这几项还原
                int oldColor = list.ColorArgb;
                int oldBgKind = list.BackgroundKind;
                string oldBgValue = list.BackgroundValue;
                int oldIcon = list.IconKind;

                using (ListEditorDialog dialog = new ListEditorDialog("编辑列表", list.Name, list.IconKind,
                           list.ColorArgb, list.BackgroundKind, list.BackgroundValue))
                {
                    dialog.AppearancePreview = delegate (int iconKind, int kind, string value, int argb)
                    {
                        list.IconKind = iconKind;
                        _store.SetListBackground(list, kind, value);
                        _store.SetListColor(list, IndexOfPalette(argb));
                        _store.RaiseChanged();
                    };

                    if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.Applied)
                    {
                        list.IconKind = oldIcon;
                        _store.SetListBackground(list, oldBgKind, oldBgValue);
                        _store.SetListColor(list, IndexOfPalette(oldColor));
                        _store.RaiseChanged();
                        return;
                    }

                    list.Name = dialog.ListName;
                    list.ColorArgb = dialog.ColorArgb;
                    list.IconKind = dialog.IconKind;
                    list.BackgroundKind = dialog.BackgroundKind;
                    list.BackgroundValue = dialog.BackgroundValue;
                    _store.Save();
                    _store.RaiseChanged();
                }
            });

            menu.Items.Add("清空该列表的已完成任务", null, delegate
            {
                int removed = _store.ClearDoneInList(list.Id);
                if (removed == 0)
                    MessageBox.Show(this, "该列表没有已完成的任务。", "便签 TODO",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除列表", null, delegate
            {
                int count = _store.Query(list.Id, false).Count + _store.Query(list.Id, true).Count;
                string message = count > 0
                    ? string.Format("删除列表「{0}」及其中的 {1} 条任务？", list.Name, count)
                    : string.Format("删除列表「{0}」？", list.Name);
                if (MessageBox.Show(this, message, "便签 TODO",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                _store.RemoveList(list);
                if (_store.FindList(_viewId) == null)
                {
                    // 列表被删光了也要能用：退回「今天」，并把上次视图记成它
                    _viewId = ViewIds.MyDay;
                    _store.Settings.LastView = _viewId;
                    _store.Save();
                }
                _sidebar.ActiveView = _viewId;
                RefreshContent();
            });

            menu.Show(Cursor.Position);
        }

        // ---------- 窗口行为 ----------

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
            // 最大化/缩放后强制整窗重绘，避免旧位置的标题图标残留
            Invalidate(true);
        }

        /// <summary>
        /// 点添加条最右侧的星星时，输入框会先失焦（进而提交并清空已输入的文字）。
        /// 这里在鼠标按下阶段（早于失焦消息）先打标记，让这次失焦不提交。
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONDBLCLK = 0x0203;

            if (m.Msg == WM_LBUTTONDOWN || m.Msg == WM_LBUTTONDBLCLK)
            {
                int lp = m.LParam.ToInt32();
                Point pt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                if (AddBarStarRect().Contains(pt)) _suppressCommit = true;
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// 收起态没有标题栏：按住 ☰ 那一行（除两个按钮外）拖动窗口。
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left && _pinned
                && PinDragRect().Contains(e.Location)
                && !PinMenuRect().Contains(e.Location)
                && !PinCloseRect().Contains(e.Location))
            {
                Native.DragWindow(Handle);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            bool starClick = AddBarStarRect().Contains(e.Location);
            _suppressCommit = false;   // 标记只用一次，鼠标松开即失效

            if (e.Button != MouseButtons.Left) return;

            // 收起态：侧栏滑出时，点侧栏以外（📌/齿轮 除外）就收回
            if (_pinned && _flyoutOpen)
            {
                Rectangle[] ic = HeaderIconRects();
                if (!_sidebar.Bounds.Contains(e.Location)
                    && !ic[0].Contains(e.Location) && !ic[1].Contains(e.Location))
                {
                    ToggleFlyout();
                    return;
                }
            }

            // 输入条最右侧的星星：切换「重要」，并把焦点还给输入框，已输入的内容保持不动
            if (starClick)
            {
                _newTaskImportant = !_newTaskImportant;
                if (_newTaskActive)
                {
                    _newTask.Visible = true;
                    _newTask.Focus();
                }
                Invalidate();
                return;
            }

            // 点了添加条以外的地方 = 输入框失去焦点 → 恢复默认显示（隐藏输入框、显示「添加任务」）
            if (_newTaskActive && !AddBarRect().Contains(e.Location))
            {
                CommitNewTask();
                return;
            }

            // 右上角：PIN 切换收起态，齿轮打开背景设置
            Rectangle[] icons = HeaderIconRects();
            if (icons[0].Contains(e.Location)) { TogglePin(); return; }
            if (icons[1].Contains(e.Location)) { OpenThemePicker(); return; }

            if (_pinned)
            {
                if (PinMenuRect().Contains(e.Location)) { ToggleFlyout(); return; }
                if (PinCloseRect().Contains(e.Location)) { Close(); return; }   // 和标题栏的 × 一样（按设置收起或退出）
            }

            if (AddBarRect().Contains(e.Location))
            {
                BeginEditNewTask();
            }
        }

        /// <summary>右键分组标题：已完成组可批量清除。</summary>
        private void Group_Menu(object sender, EventArgs e)
        {
            GroupEventArgs args = e as GroupEventArgs;
            bool doneGroup = args != null && args.DoneGroup;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiFont.Get(9.5f);

            if (doneGroup)
            {
                // 「任务」视图下是所有列表的已完成任务，其余视图只清当前视图
                string scope = _viewId == ViewIds.Done ? ViewIds.Tasks : _viewId;
                int count = _store.Query(scope, true).Count;
                menu.Items.Add("清除已完成的 " + count + " 条任务", null, delegate
                {
                    if (count == 0) return;
                    if (MessageBox.Show(this, "清除全部 " + count + " 条已完成任务？", "便签 TODO",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _store.ClearView(scope, true);
                });
            }
            else
            {
                menu.Items.Add("收起这一组", null, delegate { _tasks.ToggleGroup(false); });
            }

            menu.Show(Cursor.Position);
        }

        /// <summary>
        /// 内容区空白处右键：三个内置视图（今天 / 重要 / 任务）提供「清空全部 / 清空已完成」。
        /// 自定义列表不弹这个菜单（列表的清理在侧栏列表菜单里）。
        /// </summary>
        private void Tasks_BackgroundMenu(object sender, EventArgs e)
        {
            if (IsCustomView()) return;

            // 「任务」视图 = 所有任务，其余视图按各自语义
            string scope = _viewId == ViewIds.Done ? ViewIds.Tasks : _viewId;
            int done = _store.Query(scope, true).Count;
            int active = _store.Query(scope, false).Count;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiFont.Get(9.5f);

            menu.Items.Add("清空全部（" + (active + done) + " 条）", null, delegate
            {
                int total = active + done;
                if (total == 0) return;
                if (MessageBox.Show(this, "确定清空当前视图里的 " + total + " 条任务？\r\n\r\n" +
                        "这些任务会从它们所属的列表里一起删除，且无法撤销。", "便签 TODO",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                _store.ClearView(scope, false);
            });

            ToolStripItem clearDone = menu.Items.Add("清空已完成（" + done + " 条）", null, delegate
            {
                if (done == 0) return;
                if (MessageBox.Show(this, "确定清空当前视图里已完成的 " + done + " 条任务？", "便签 TODO",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _store.ClearView(scope, true);
            });
            clearDone.Enabled = done > 0;

            menu.Show(Cursor.Position);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl+N：光标进添加任务输入框
            if (e.Control && e.KeyCode == Keys.N)
            {
                if (!_newTask.Focused) BeginEditNewTask();
                e.SuppressKeyPress = true;
                return;
            }

            // Ctrl+I：切换「重要」标记（输入条开着就切换那颗星，否则切换选中的任务）
            if (e.Control && e.KeyCode == Keys.I)
            {
                ToggleImportantShortcut();
                e.SuppressKeyPress = true;
                return;
            }

            // Ctrl+M：切换窗体模式（收起小窗 / 恢复正常）
            if (e.Control && e.KeyCode == Keys.M)
            {
                TogglePin();
                e.SuppressKeyPress = true;
                return;
            }

            // Ctrl+P：换背景（打开背景设置）
            if (e.Control && e.KeyCode == Keys.P)
            {
                OpenThemePicker();
                e.SuppressKeyPress = true;
                return;
            }

            // Alt+1 / Alt+2 / Alt+3：今天 / 重要 / 任务；Alt+~：收起态下滑出/收起侧栏菜单
            if (e.Alt)
            {
                if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) { SwitchView(ViewIds.MyDay); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) { SwitchView(ViewIds.Important); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) { SwitchView(ViewIds.Done); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Oemtilde && _pinned) { ToggleFlyout(); e.SuppressKeyPress = true; }
            }
        }

        // ---------- 拖拽进「添加任务」 ----------

        /// <summary>把某个控件变成拖放目标。</summary>
        private void EnableDrop(Control target)
        {
            target.AllowDrop = true;
            target.DragEnter += Drop_DragEnter;
            target.DragDrop += Drop_DragDrop;
        }

        private void Drop_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DropPayload(e.Data) != null ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Drop_DragDrop(object sender, DragEventArgs e)
        {
            object payload = DropPayload(e.Data);
            if (payload == null) return;

            Bitmap image = payload as Bitmap;
            if (image != null)
            {
                ApplyDroppedImage(image);
                return;
            }

            string[] files = payload as string[];
            if (files != null && files.Length == 1 && IsImageFile(files[0]))
            {
                ApplyDroppedImageFile(files[0]);
                return;
            }

            // 其它情况一律当文本：文件名按名字拼进去，纯文本直接进输入框
            if (files != null)
            {
                string joined = string.Empty;
                for (int i = 0; i < files.Length; i++)
                    joined += (i == 0 ? string.Empty : "; ") + System.IO.Path.GetFileName(files[i]);
                PutIntoNewTask(joined);
                return;
            }

            PutIntoNewTask(payload as string);
        }

        /// <summary>取拖拽内容：位图 → Bitmap；文件 → string[]；文本 → string；都不是则 null。</summary>
        private static object DropPayload(IDataObject data)
        {
            if (data == null) return null;
            try
            {
                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0) return files;
                }
                if (data.GetDataPresent(DataFormats.Bitmap))
                {
                    object bmp = data.GetData(DataFormats.Bitmap);
                    if (bmp is Bitmap) return bmp;
                }
                if (data.GetDataPresent(DataFormats.UnicodeText))
                {
                    string text = data.GetData(DataFormats.UnicodeText) as string;
                    if (!string.IsNullOrEmpty(text)) return text;
                }
                if (data.GetDataPresent(DataFormats.Text))
                {
                    string text = data.GetData(DataFormats.Text) as string;
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch { }
            return null;
        }

        private static bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        /// <summary>把拖进来的文字写进添加任务框（输入框没开就先打开）。</summary>
        private void PutIntoNewTask(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (text.Length == 0) return;

            if (!_newTaskActive) BeginEditNewTask();
            string current = _newTask.Text.Trim();
            _newTask.Text = current.Length > 0 ? current + " " + text : text;
            _newTask.SelectionStart = _newTask.Text.Length;
            _newTask.Focus();
            Invalidate();
        }

        /// <summary>拖进来一张图片（内存位图）：存成文件后当作当前版块的背景。</summary>
        private void ApplyDroppedImage(Bitmap bmp)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_store.FilePath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir,
                    "bg-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                ApplyDroppedImageFile(file);
            }
            catch { }
        }

        /// <summary>拖进来一个图片文件：直接当作当前版块的背景（背景不跟随主色，所以颜色沿用当前值）。</summary>
        private void ApplyDroppedImageFile(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return;
                ApplyBackground(_store.FindList(_viewId), 2, path, _viewColor.ToArgb(), false);
            }
            catch { }
        }

        /// <summary>点侧栏顶部数据库图标：用资源管理器打开数据目录（并选中 todos.json）。</summary>
        private void OpenStorageFolder()
        {
            try
            {
                string path = _store.FilePath;
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe",
                        "\"" + System.IO.Path.GetDirectoryName(path) + "\"");
            }
            catch { }
        }

        /// <summary>快捷键切视图：侧栏高亮与内容一起换。</summary>
        private void SwitchView(string viewId)
        {
            if (_viewId == viewId || FindNode(viewId) == null) return;
            _sidebar.ActiveView = viewId;
            OnViewChanged(viewId);
        }

        // ---------- 收起（PIN）态 ----------

        /// <summary>点右上角 📌 切换：收起成无标题栏的窄条、锁定尺寸并永远置顶；再点一次回到原样。</summary>
        private void TogglePin()
        {
            if (_pinned) ExitPinMode(); else EnterPinMode();
        }

        private void EnterPinMode()
        {
            _pinRestoreState = WindowState;
            if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;

            _pinRestoreBounds = Bounds;
            _pinRestoreMin = MinimumSize;
            _pinRestoreMax = MaximumSize;
            _pinRestoreTopMost = TopMost;
            _pinRestoreBorder = FormBorderStyle;

            _pinned = true;
            CloseFlyout();

            // 无标题栏 + 锁定尺寸（最小=最大，连系统贴边缩放也改不了大小）
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;

            int w = 480;
            int h = Math.Min(620, Math.Max(400, _pinRestoreBounds.Height));
            // 贴到「窗口所在那块屏幕」的右上角（多显示器时按所在屏算），不留缩进
            Rectangle wa = Screen.FromRectangle(_pinRestoreBounds).WorkingArea;
            int x = wa.Right - w;
            int y = wa.Top;

            MinimumSize = new Size(w, h);
            MaximumSize = new Size(w, h);
            SetBounds(x, y, w, h);
            TopMost = true;

            ApplyEmptyState();
            LayoutChildren();
            Invalidate(true);
        }

        private void ExitPinMode()
        {
            _pinned = false;
            CloseFlyout();

            FormBorderStyle = _pinRestoreBorder;
            MinimumSize = _pinRestoreMin;
            MaximumSize = _pinRestoreMax;
            Bounds = _pinRestoreBounds;
            TopMost = _pinRestoreTopMost;
            if (_pinRestoreState == FormWindowState.Maximized) WindowState = FormWindowState.Maximized;

            _sidebar.Visible = true;
            _sidebar.Dock = DockStyle.Left;
            ApplyEmptyState();
            LayoutChildren();
            Invalidate(true);
        }

        /// <summary>☰：从左侧滑出侧栏（原始窗口里那条导航 + 新建列表），再点收回。</summary>
        private void ToggleFlyout()
        {
            if (_flyoutTimer == null)
            {
                _flyoutTimer = new Timer();
                _flyoutTimer.Interval = 15;
                _flyoutTimer.Tick += delegate { FlyoutTick(); };
            }

            _flyoutOpen = !_flyoutOpen;

            if (_flyoutOpen)
            {
                _sidebar.Dock = DockStyle.None;
                _sidebar.SetBounds(-Design.SidebarWidth, 0, Design.SidebarWidth, ClientSize.Height);
                _sidebar.Visible = true;
                _sidebar.BringToFront();
            }

            _tasks.OverlayAlpha = _flyoutOpen ? OverlayAlphaValue : 0;
            _tasks.Invalidate();
            Invalidate();
            _flyoutTimer.Start();
        }

        private void FlyoutTick()
        {
            int target = _flyoutOpen ? 0 : -Design.SidebarWidth;
            int step = Math.Max(14, Design.SidebarWidth / 7);
            int next = _sidebar.Left + (_flyoutOpen ? step : -step);
            if ((_flyoutOpen && next >= target) || (!_flyoutOpen && next <= target)) next = target;

            _sidebar.SetBounds(next, 0, Design.SidebarWidth, ClientSize.Height);

            if (next != target) return;

            _flyoutTimer.Stop();
            if (!_flyoutOpen)
            {
                _sidebar.Visible = false;
                _sidebar.Dock = DockStyle.Left;
            }
        }

        private void CloseFlyout()
        {
            if (_flyoutTimer != null) _flyoutTimer.Stop();
            _flyoutOpen = false;
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Visible = !_pinned;
            _tasks.OverlayAlpha = 0;
            _tasks.Invalidate();
        }

        private void ToggleImportantShortcut()
        {
            if (_newTaskActive || _newTask.Focused)
            {
                _newTaskImportant = !_newTaskImportant;
                Invalidate();
                return;
            }

            TodoItem item = _tasks.SelectedItem;
            if (item != null) _store.SetImportant(item, item.Priority < 2);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_forceClose && _store.Settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
