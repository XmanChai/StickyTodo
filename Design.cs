using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>
    /// 设计规范：配色、字号、圆角与间距，以及卡片/图标等绘制工具。
    /// 参照微软 To Do：浅灰紫侧边栏 + 白卡任务行 + 列表主题色作为头部底色。
    /// </summary>
    internal static class Design
    {
        // ---------- 基础配色 ----------
        /// <summary>侧边栏底色：与 Windows 原生标题栏一致（本机标题栏为纯白）。</summary>
        public static readonly Color Sidebar = Color.White;
        public static readonly Color ContentBack = Color.FromArgb(243, 242, 248);
        public static readonly Color CardFill = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(226, 224, 234);
        public static readonly Color CardHover = Color.FromArgb(250, 250, 253);
        public static readonly Color Ink = Color.FromArgb(32, 31, 30);
        public static readonly Color InkSoft = Color.FromArgb(96, 94, 92);
        public static readonly Color InkFaint = Color.FromArgb(150, 148, 148);
        public static readonly Color CheckRing = Color.FromArgb(120, 118, 124);
        public static readonly Color Hover = Color.FromArgb(240, 240, 245);
        /// <summary>侧栏选中项底色（白底侧栏上用一个浅灰胶囊）。</summary>
        public static readonly Color PillActive = Color.FromArgb(235, 235, 242);
        public static readonly Color SectionBack = Color.FromArgb(243, 242, 248);
        public static readonly Color HeaderInk = Color.White;
        public static readonly Color DueToday = Color.FromArgb(197, 70, 60);
        public static readonly Color DueOverdue = Color.FromArgb(197, 70, 60);
        public static readonly Color DoneLine = Color.FromArgb(170, 170, 170);
        public static readonly Color Important = Color.FromArgb(224, 122, 63);
        public static readonly Color MyDaySun = Color.FromArgb(240, 176, 60);
        public static readonly Color BrandPurple = Color.FromArgb(135, 100, 184);
        public static readonly Color Accent = Color.FromArgb(135, 100, 184);

        // ---------- 几何 ----------
        public const int SidebarWidth = 260;
        public const int HeaderHeight = 72;
        public const int RowHeight = 48;
        public const int RowGap = 8;
        public const int CardRadius = 6;
        public const int ContentPad = 24;
        public const int NavItemHeight = 36;

        /// <summary>列表主题色可选值。</summary>
        public static readonly Color[] Palette = new Color[]
        {
            Color.FromArgb(135, 100, 184),  // 紫
            Color.FromArgb(76, 138, 202),   // 蓝
            Color.FromArgb(72, 168, 160),   // 青
            Color.FromArgb(93, 168, 96),    // 绿
            Color.FromArgb(216, 168, 62),   // 黄
            Color.FromArgb(232, 150, 74),   // 橙
            Color.FromArgb(212, 96, 110),   // 红
            Color.FromArgb(150, 120, 200)   // 淡紫
        };

        public static Color ListColor(TodoList list)
        {
            return list == null ? Palette[0] : list.Color;
        }

        /// <summary>背景主题：渐变由两个色构成，用于列表背景（对应「主题」设置里的背景选项）。</summary>
        public class BackgroundTheme
        {
            public string Name;
            public Color From;
            public Color To;

            public BackgroundTheme(string name, Color from, Color to)
            {
                Name = name;
                From = from;
                To = to;
            }
        }

        public static readonly BackgroundTheme[] Themes = new BackgroundTheme[]
        {
            new BackgroundTheme("紫罗兰", Color.FromArgb(129, 92, 182), Color.FromArgb(178, 148, 216)),
            new BackgroundTheme("深海", Color.FromArgb(48, 104, 172), Color.FromArgb(120, 178, 220)),
            new BackgroundTheme("青竹", Color.FromArgb(46, 148, 140), Color.FromArgb(126, 200, 178)),
            new BackgroundTheme("苔绿", Color.FromArgb(78, 150, 88), Color.FromArgb(160, 204, 140)),
            new BackgroundTheme("暖沙", Color.FromArgb(198, 154, 78), Color.FromArgb(232, 202, 140)),
            new BackgroundTheme("日落", Color.FromArgb(206, 118, 74), Color.FromArgb(236, 178, 124)),
            new BackgroundTheme("莓红", Color.FromArgb(190, 82, 104), Color.FromArgb(228, 150, 166)),
            new BackgroundTheme("石墨", Color.FromArgb(78, 80, 96), Color.FromArgb(142, 146, 164))
        };

        /// <summary>把颜色按比例调亮（ratio &gt; 0）或调暗（ratio &lt; 0）。</summary>
        public static Color Shade(Color color, float ratio)
        {
            if (ratio > 0f)
            {
                return Color.FromArgb(
                    (int)(color.R + (255 - color.R) * ratio),
                    (int)(color.G + (255 - color.G) * ratio),
                    (int)(color.B + (255 - color.B) * ratio));
            }
            float k = 1f + ratio;
            if (k < 0f) k = 0f;
            return Color.FromArgb((int)(color.R * k), (int)(color.G * k), (int)(color.B * k));
        }

        /// <summary>按亮度挑一个用于背景之上文字的对比色。</summary>
        public static Color ContrastOn(Color background)
        {
            int luma = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return luma >= 150 ? Ink : Color.White;
        }

        /// <summary>背景之上叠一层淡淡的雾面，保证白卡与文字都看得清。</summary>
        public static Color ScrimOn(Color background)
        {
            Color ink = ContrastOn(background);
            return ink == Color.White ? Color.FromArgb(46, 0, 0, 0) : Color.FromArgb(26, 255, 255, 255);
        }

        /// <summary>用于「智能视图」的固定主题色。</summary>
        public static readonly Color MyDayColor = Color.FromArgb(135, 100, 184);
        public static readonly Color ImportantColor = Color.FromArgb(120, 118, 190);
        public static readonly Color PlannedColor = Color.FromArgb(76, 138, 202);
        public static readonly Color DoneColor = Color.FromArgb(93, 168, 96);
        public static readonly Color TasksColor = Color.FromArgb(135, 100, 184);

        // ---------- 绘制工具 ----------

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                using (GraphicsPath path = Round(r, radius))
                using (SolidBrush brush = new SolidBrush(fill))
                    g.FillPath(brush, path);
            }
            finally { g.SmoothingMode = old; }
        }

        public static void Card(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                using (GraphicsPath path = Round(r, radius))
                {
                    using (SolidBrush brush = new SolidBrush(fill))
                        g.FillPath(brush, path);
                    using (Pen pen = new Pen(border, 1f))
                        g.DrawPath(pen, path);
                }
            }
            finally { g.SmoothingMode = old; }
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            if (d > r.Height) d = r.Height;
            if (d > r.Width) d = r.Width;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds,
            ContentAlignment align)
        {
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            if (align == ContentAlignment.MiddleCenter)
                flags |= TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            else if (align == ContentAlignment.MiddleRight)
                flags |= TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
            else if (align == ContentAlignment.TopLeft)
                flags |= TextFormatFlags.Left | TextFormatFlags.Top;
            else
                flags |= TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, text, font, bounds, color, flags);
        }

        public static void DrawClipped(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            Region old = null;
            try
            {
                old = g.Clip;
                g.SetClip(bounds);
                TextRenderer.DrawText(g, text, font, bounds, color,
                    TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            finally
            {
                if (old != null) g.Clip = old;
            }
        }
    }

    /// <summary>界面图标：全部用 GDI+ 现画，不依赖字体符号，保证各机器一致。</summary>
    internal static class Icons
    {
        public static void PlusCircle(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.6f))
            {
                g.DrawEllipse(pen, r);
                float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f, arm = r.Width * 0.22f;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
                g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
            }
            Rm(g);
        }

        public static void Plus(Graphics g, Point center, int size, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int half = size / 2;
                g.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
                g.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
            }
            Rm(g);
        }

        public static void CircleDot(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.5f))
                g.DrawEllipse(pen, r);
            using (SolidBrush brush = new SolidBrush(color))
            {
                float d = r.Width * 0.24f;
                g.FillEllipse(brush, r.Left + (r.Width - d) / 2f, r.Top + (r.Height - d) / 2f, d, d);
            }
            Rm(g);
        }

        public static void Dots(Graphics g, Point center, Color color)
        {
            Dots(g, center, color, 3.4f);
        }

        /// <summary>三个圆点（row = ...），可指定点径。</summary>
        public static void Dots(Graphics g, Point center, Color color, float dotSize)
        {
            Sm(g);
            float gap = dotSize * 1.9f;
            using (SolidBrush brush = new SolidBrush(color))
            {
                for (int i = -1; i <= 1; i++)
                    g.FillEllipse(brush,
                        center.X + i * gap - dotSize / 2f, center.Y - dotSize / 2f, dotSize, dotSize);
            }
            Rm(g);
        }

        /// <summary>三横线（☰）：收起（PIN）态标题区左上角。</summary>
        public static void Menu(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, Math.Max(1.4f, r.Height * 0.095f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float x0 = r.Left + r.Width * 0.06f;
                float x1 = r.Right - r.Width * 0.06f;
                for (int i = 0; i < 3; i++)
                {
                    float y = r.Top + r.Height * (0.24f + i * 0.26f);
                    g.DrawLine(pen, x0, y, x1, y);
                }
            }
            Rm(g);
        }

        /// <summary>齿轮（⚙）：右上角的设置/背景入口。</summary>
        public static void Gear(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
            float body = r.Width * 0.29f;
            float outer = r.Width * 0.40f;
            float inner = r.Width * 0.12f;
            float pw = Math.Max(1.4f, r.Width * 0.09f);
            using (Pen pen = new Pen(color, pw))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - body, cy - body, body * 2, body * 2);
                g.DrawEllipse(pen, cx - inner, cy - inner, inner * 2, inner * 2);
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI / 4 * i;
                    g.DrawLine(pen,
                        (float)(cx + Math.Cos(a) * (body - pw * 0.3f)), (float)(cy + Math.Sin(a) * (body - pw * 0.3f)),
                        (float)(cx + Math.Cos(a) * outer), (float)(cy + Math.Sin(a) * outer));
                }
            }
            Rm(g);
        }

        /// <summary>数据库（圆柱）图标：侧栏顶部。</summary>
        public static void Database(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            float x = r.Left + r.Width * 0.16f;
            float w = r.Width * 0.68f;
            float top = r.Top + r.Height * 0.14f;
            float h = r.Height * 0.72f;
            float eh = r.Height * 0.22f;
            using (Pen pen = new Pen(color, Math.Max(1.3f, r.Width * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, x, top, w, eh);                          // 顶盖
                g.DrawArc(pen, x, top + h - eh, w, eh, 0, 180);             // 底弧
                g.DrawLine(pen, x, top + eh / 2, x, top + h - eh / 2);      // 左壁
                g.DrawLine(pen, x + w, top + eh / 2, x + w, top + h - eh / 2);   // 右壁
                g.DrawArc(pen, x, top + h * 0.45f - eh / 2, w, eh, 0, 180); // 中间分层
            }
            Rm(g);
        }

        /// <summary>✕：收起态的关闭按钮。</summary>
        public static void Close(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, Math.Max(1.5f, r.Width * 0.1f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float pad = r.Width * 0.26f;
                g.DrawLine(pen, r.Left + pad, r.Top + pad, r.Right - pad, r.Bottom - pad);
                g.DrawLine(pen, r.Right - pad, r.Top + pad, r.Left + pad, r.Bottom - pad);
            }
            Rm(g);
        }

        // ---------- 统一图标编号（列表图标 / 侧栏 / 标题区共用） ----------
        // 0..9 与旧版含义一一对应（列表/星/日历/人/房子/勾选/太阳/旗/铃/书签），后面是更丰富的补充。
        // 字形取自 Windows 10 自带的 Segoe MDL2 Assets（微软 To Do / Win10 应用用的同一套）：
        // 单色轮廓字形、可跟随颜色，比 emoji 轮廓干净得多；不需要任何外部图标文件。
        // 少数语义 MDL2 没有对应字形（水果 / 美食 / 礼物），已换成相近的概念并同步改了名字。

        /// <summary>置顶（PIN）图标：图钉字形。</summary>
        public const int PinKind = 17;

        /// <summary>图标字体：优先 MDL2，缺失时退到 Segoe UI Symbol。</summary>
        private static readonly string IconFontName = PickIconFont();

        private static string PickIconFont()
        {
            foreach (string name in new string[] { "Segoe MDL2 Assets", "Segoe UI Symbol", "Segoe UI" })
            {
                try
                {
                    using (FontFamily family = new FontFamily(name))
                        if (family != null) return name;
                }
                catch { }
            }
            return "Segoe UI";
        }

        public static readonly string[] Emojis = new string[]
        {
            "\uEADF", // 0  列表（带勾选的清单）
            "\uE734", // 1  星标（空心星）
            "\uE787", // 2  日历
            "\uEA8C", // 3  成员（人）
            "\uE80F", // 4  房子
            "\uE73E", // 5  勾选
            "\uE706", // 6  太阳
            "\uE7C1", // 7  旗帜
            "\uEA8F", // 8  提醒（铃）
            "\uE8EC", // 9  书签（标签）
            "\uE8A9", // 10 分类（方格；MDL2 没有水果）
            "\uE7BF", // 11 购物（推车）
            "\uE821", // 12 工作（公文包）
            "\uE8D6", // 13 音乐（音符）
            "\uEB4C", // 14 旅行（飞机）
            "\uEA80", // 15 灵感（灯泡）
            "\uF272", // 16 目标（靶心）
            "\uE840", // 17 置顶（图钉）
            "\uE8C7", // 18 财务（卡片）
            "\uE8F1", // 19 学习（书）
            "\uE805", // 20 运动（走动的人）
            "\uEB9F", // 21 图片（MDL2 没有美食）
            "\uEB51", // 22 重点（心形）
            "\uE823", // 23 闹钟（时钟）
            "\uE8A5", // 24 文档（MDL2 没有礼物）
            "\uE95E", // 25 健康（心 + 医疗）
            "\uE7F8", // 26 电脑（笔记本）
            "\uE735", // 27 收藏（实心星）
            "\uE90F", // 28 工具（扳手）
            "\uE790"  // 29 设计（调色板）
        };

        public static readonly string[] KindNames = new string[]
        {
            "列表", "星标", "日历", "成员", "房子", "勾选", "太阳", "旗帜", "提醒", "书签",
            "分类", "购物", "工作", "音乐", "旅行", "灵感", "目标", "置顶", "财务", "学习",
            "运动", "图片", "重点", "闹钟", "文档", "健康", "电脑", "收藏", "工具", "设计"
        };

        /// <summary>可选图标编号（列表图标选择用）。</summary>
        public static readonly int[] IconsList = new int[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
            20, 21, 22, 23, 24, 25, 26, 27, 28, 29
        };

        public static string EmojiOf(int kind)
        {
            if (kind < 0 || kind >= Emojis.Length) return Emojis[0];
            return Emojis[kind];
        }

        public static string NameOf(int kind)
        {
            if (kind < 0 || kind >= KindNames.Length) return KindNames[0];
            return KindNames[kind];
        }

        /// <summary>按编号绘制图标（MDL2 单色字形，跟随 color）。</summary>
        public static void ByKind(Graphics g, Rectangle box, int kind, Color color)
        {
            ByKind(g, box, kind, color, false);
        }

        /// <summary>按编号绘制图标；bold = true 时用描边加粗（标题区用）。</summary>
        public static void ByKind(Graphics g, Rectangle box, int kind, Color color, bool bold)
        {
            DrawEmoji(g, box, EmojiOf(kind), color, bold);
        }

        /// <summary>
        /// 把字形画进指定矩形，颜色跟随 color；bold 用四次微偏移加粗。
        /// 用 GDI+ 的 AntiAlias 画（灰度抗锯齿，不带 ClearType 彩边），字号 = 矩形高度。
        /// 关键：先量出字形的**墨迹范围**，再把墨迹中心对准矩形中心 —— GDI+ 默认按「行盒」居中，
        /// 而 MDL2 字形的墨迹偏上行盒，直接用行盒居中会让图标看着比旁边的文字高一截。
        /// </summary>
        public static void DrawEmoji(Graphics g, Rectangle box, string emoji, Color color, bool bold)
        {
            if (string.IsNullOrEmpty(emoji) || box.Width <= 0 || box.Height <= 0) return;

            float size = box.Height;
            using (Font font = new Font(IconFontName, size, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                System.Drawing.Text.TextRenderingHint oldHint = g.TextRenderingHint;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                try
                {
                    RectangleF ink;
                    using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        path.AddString(emoji, font.FontFamily, (int)font.Style, font.Size,
                            new PointF(0, 0), format);
                        ink = path.GetBounds();
                    }
                    if (ink.Width <= 0 || ink.Height <= 0) return;

                    float x = box.Left + (box.Width - ink.Width) / 2f - ink.Left;
                    float y = box.Top + (box.Height - ink.Height) / 2f - ink.Top;

                    if (bold)
                    {
                        float o = Math.Max(0.6f, box.Height * 0.035f);
                        g.DrawString(emoji, font, brush, x - o, y, format);
                        g.DrawString(emoji, font, brush, x + o, y, format);
                        g.DrawString(emoji, font, brush, x, y - o, format);
                        g.DrawString(emoji, font, brush, x, y + o, format);
                    }
                    else
                    {
                        g.DrawString(emoji, font, brush, x, y, format);
                    }
                }
                finally
                {
                    g.TextRenderingHint = oldHint;
                }
            }
        }

        /// <summary>旗帜。</summary>
        public static void Flag(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float x = r.Left + r.Width * 0.28f;
                g.DrawLine(pen, x, r.Top + 1, x, r.Bottom - 1);
                g.DrawLine(pen, x, r.Top + 1, r.Right - 2, r.Top + r.Height * 0.32f);
                g.DrawLine(pen, r.Right - 2, r.Top + r.Height * 0.32f, x, r.Top + r.Height * 0.60f);
            }
            Rm(g);
        }

        /// <summary>铃铛。</summary>
        public static void Bell(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                float w = r.Width, h = r.Height;
                g.DrawArc(pen, r.Left + w * 0.22f, r.Top + h * 0.08f, w * 0.56f, h * 0.62f, 180, 180);
                g.DrawLine(pen, r.Left + w * 0.22f, r.Top + h * 0.39f, r.Left + w * 0.22f, r.Top + h * 0.72f);
                g.DrawLine(pen, r.Right - w * 0.22f, r.Top + h * 0.39f, r.Right - w * 0.22f, r.Top + h * 0.72f);
                g.DrawLine(pen, r.Left + w * 0.12f, r.Top + h * 0.72f, r.Right - w * 0.12f, r.Top + h * 0.72f);
                g.DrawArc(pen, r.Left + w * 0.40f, r.Top + h * 0.74f, w * 0.20f, h * 0.20f, 0, 180);
            }
            Rm(g);
        }

        /// <summary>书签。</summary>
        public static void Bookmark(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                float w = r.Width, h = r.Height;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(r.Left + w * 0.26f, r.Top + 1),
                    new PointF(r.Right - w * 0.26f, r.Top + 1),
                    new PointF(r.Right - w * 0.26f, r.Bottom - 1),
                    new PointF(r.Left + w * 0.5f, r.Bottom - h * 0.30f),
                    new PointF(r.Left + w * 0.26f, r.Bottom - 1),
                    new PointF(r.Left + w * 0.26f, r.Top + 1)
                });
            }
            Rm(g);
        }

        public static void Sun(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f;
            float core = r.Width * 0.22f;
            using (Pen pen = new Pen(color, Math.Max(1.3f, r.Width * 0.055f)))
            {
                // 镂空：圆心只描边
                g.DrawEllipse(pen, cx - core, cy - core, core * 2, core * 2);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI / 4 * i;
                    float r1 = core + 2.6f, r2 = r.Width * 0.47f;
                    g.DrawLine(pen,
                        (float)(cx + Math.Cos(a) * r1), (float)(cy + Math.Sin(a) * r1),
                        (float)(cx + Math.Cos(a) * r2), (float)(cy + Math.Sin(a) * r2));
                }
            }
            Rm(g);
        }

        /// <summary>圆圈内打勾（镂空）：任务视图。</summary>
        public static void CheckCircleOutline(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                g.DrawEllipse(pen, r);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float x = r.Left, y = r.Top, w = r.Width, h = r.Height;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(x + w * 0.28f, y + h * 0.52f),
                    new PointF(x + w * 0.45f, y + h * 0.70f),
                    new PointF(x + w * 0.74f, y + h * 0.32f)
                });
            }
            Rm(g);
        }

        public static void Star(Graphics g, Rectangle r, Color color, bool filled)
        {
            Sm(g);
            PointF[] pts = StarPoints(r);
            if (filled)
            {
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillPolygon(brush, pts);
            }
            else
            {
                using (Pen pen = new Pen(color, 1.4f))
                    g.DrawPolygon(pen, pts);
            }
            Rm(g);
        }

        private static PointF[] StarPoints(Rectangle r)
        {
            float cx = r.Left + r.Width / 2f, cy = r.Top + r.Height / 2f + 0.6f;
            float outer = r.Width * 0.47f, inner = outer * 0.44f;
            PointF[] pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + Math.PI / 5 * i;
                float rad = (i % 2 == 0) ? outer : inner;
                pts[i] = new PointF((float)(cx + Math.Cos(a) * rad), (float)(cy + Math.Sin(a) * rad));
            }
            return pts;
        }

        public static void Calendar(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.5f))
            {
                g.DrawRectangle(pen, r.Left, r.Top + 2, r.Width - 1, r.Height - 3);
                g.DrawLine(pen, r.Left, r.Top + 7, r.Right - 1, r.Top + 7);
                g.DrawLine(pen, r.Left + 3, r.Top, r.Left + 3, r.Top + 4);
                g.DrawLine(pen, r.Right - 4, r.Top, r.Right - 4, r.Top + 4);
            }
            Rm(g);
        }

        public static void Person(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.5f))
            {
                float d = r.Width * 0.36f;
                g.DrawEllipse(pen, r.Left + (r.Width - d) / 2f, r.Top + 1, d, d);
                g.DrawArc(pen, r.Left + r.Width * 0.14f, r.Top + r.Height * 0.52f,
                    r.Width * 0.72f, r.Height * 0.62f, 180, 180);
            }
            Rm(g);
        }

        public static void Home(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.5f))
            {
                float w = r.Width, h = r.Height;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(r.Left, r.Top + h * 0.45f),
                    new PointF(r.Left + w / 2f, r.Top),
                    new PointF(r.Right, r.Top + h * 0.45f)
                });
                g.DrawRectangle(pen, r.Left + w * 0.2f, r.Top + h * 0.45f, w * 0.6f, h * 0.5f);
            }
            Rm(g);
        }

        /// <summary>列表图标：三行小横条。</summary>
        public static void ListLines(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (SolidBrush brush = new SolidBrush(color))
            {
                int bar = 3;
                g.FillRectangle(brush, r.Left, r.Top + 2, r.Width, bar);
                g.FillRectangle(brush, r.Left, r.Top + r.Height / 2 - 1, r.Width, bar);
                g.FillRectangle(brush, r.Left, r.Bottom - 5, r.Width, bar);
            }
            Rm(g);
        }

        /// <summary>灯泡：建议我的一天。</summary>
        public static void Bulb(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                // 灯罩
                int d = (int)(r.Width * 0.64f);
                Rectangle glass = new Rectangle(r.Left + (r.Width - d) / 2, r.Top, d, d);
                g.DrawEllipse(pen, glass);

                // 灯头（略窄的小方 + 一道灯丝横线）
                int bw = Math.Max(5, (int)(d * 0.52f));
                int bh = Math.Max(4, (int)(r.Height * 0.28f));
                int bx = r.Left + (r.Width - bw) / 2;
                int by = glass.Bottom - 2;
                if (by + bh > r.Bottom) bh = Math.Max(3, r.Bottom - by);
                g.DrawRectangle(pen, bx, by, bw, bh);
                g.DrawLine(pen, bx, by + bh / 2, bx + bw, by + bh / 2);
            }
            Rm(g);
        }

        /// <summary>分享：三个圆点用线连起来。</summary>
        public static void Share(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            float d = r.Width * 0.30f;
            PointF a = new PointF(r.Left + d / 2f, r.Top + r.Height / 2f);
            PointF b = new PointF(r.Right - d / 2f, r.Top + d / 2f);
            PointF c = new PointF(r.Right - d / 2f, r.Bottom - d / 2f);
            using (Pen pen = new Pen(color, 1.4f))
            {
                g.DrawLine(pen, a, b);
                g.DrawLine(pen, a, c);
            }
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, a.X - d / 2f, a.Y - d / 2f, d, d);
                g.FillEllipse(brush, b.X - d / 2f, b.Y - d / 2f, d, d);
                g.FillEllipse(brush, c.X - d / 2f, c.Y - d / 2f, d, d);
            }
            Rm(g);
        }

        /// <summary>标题栏图标底块：圆角方块（实体背景），前两个图标才有。</summary>
        public static void IconChip(Graphics g, Rectangle r, Color fill)
        {
            Design.FillRound(g, r, 7, fill);
        }

        /// <summary>「分享/导出」图标：方框 + 右上角小方块 + 左下斜箭头（对齐参考图最左那个）。</summary>
        public static void Export(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.4f))
            {
                // 外框（略偏右下，给左上留出箭头位置）
                Rectangle box = new Rectangle(r.Left + (int)(r.Width * 0.16f), r.Top + (int)(r.Height * 0.24f),
                    (int)(r.Width * 0.62f), (int)(r.Height * 0.58f));
                g.DrawRectangle(pen, box);

                // 右上角小方块（实心）
                int sq = Math.Max(4, (int)(r.Width * 0.26f));
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillRectangle(brush, new Rectangle(box.Right - sq / 2, box.Top - sq / 2, sq, sq));

                // 左下斜箭头（指向左上）
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float x1 = box.Left + box.Width * 0.30f, y1 = box.Bottom - box.Height * 0.28f;
                float x2 = box.Left + box.Width * 0.72f, y2 = box.Bottom - box.Height * 0.72f;
                g.DrawLine(pen, x1, y1, x2, y2);
                g.DrawLine(pen, x2, y2, x2 - box.Width * 0.26f, y2);
                g.DrawLine(pen, x2, y2, x2, y2 + box.Height * 0.30f);
            }
            Rm(g);
        }

        /// <summary>调色盘：主题/背景设置。</summary>
        public static void Palette(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            Rectangle ring = new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
            using (Pen pen = new Pen(color, 1.3f))
                g.DrawEllipse(pen, ring);
            using (SolidBrush brush = new SolidBrush(color))
            {
                float d = ring.Width * 0.15f;
                g.FillEllipse(brush, ring.Left + ring.Width * 0.28f, ring.Top + ring.Height * 0.30f, d, d);
                g.FillEllipse(brush, ring.Left + ring.Width * 0.58f, ring.Top + ring.Height * 0.24f, d, d);
                g.FillEllipse(brush, ring.Left + ring.Width * 0.30f, ring.Top + ring.Height * 0.58f, d, d);
                g.FillEllipse(brush, ring.Left + ring.Width * 0.60f, ring.Top + ring.Height * 0.60f, d, d);
            }
            Rm(g);
        }

        /// <summary>空状态插画：日历页 + 太阳 + 闪光（对应 To Do 的 "Focus on your day"）。</summary>
        public static void EmptyDay(Graphics g, Rectangle r)
        {
            Sm(g);

            // 日历页（带一点偏移阴影）
            int pw = (int)(r.Width * 0.56f);
            int ph = (int)(r.Height * 0.76f);
            int px = r.Left + (int)(r.Width * 0.08f);
            int py = r.Top + (int)(r.Height * 0.18f);
            Rectangle page = new Rectangle(px, py, pw, ph);

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillRectangle(shadow, new Rectangle(page.X + 3, page.Y + 3, page.Width, page.Height));
            using (SolidBrush white = new SolidBrush(Color.White))
                g.FillRectangle(white, page);
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(96, 140, 214)))
                g.FillRectangle(blue, new Rectangle(page.X, page.Y, page.Width, Math.Max(6, page.Height / 5)));

            using (Pen line = new Pen(Color.FromArgb(202, 206, 216), 2f))
            {
                g.DrawLine(line, page.X + 6, page.Y + page.Height * 0.46f, page.Right - 6, page.Y + page.Height * 0.46f);
                g.DrawLine(line, page.X + 6, page.Y + page.Height * 0.66f, page.Right - 14, page.Y + page.Height * 0.66f);
            }

            // 太阳 + 光芒
            int sunSize = (int)(r.Width * 0.28f);
            Rectangle sun = new Rectangle(r.Right - sunSize - (int)(r.Width * 0.02f), r.Top, sunSize, sunSize);
            using (SolidBrush yellow = new SolidBrush(Color.FromArgb(245, 190, 70)))
            {
                g.FillEllipse(yellow, sun);
                using (Pen ray = new Pen(Color.FromArgb(245, 190, 70), 2f))
                {
                    ray.StartCap = LineCap.Round;
                    ray.EndCap = LineCap.Round;
                    float cx = sun.Left + sun.Width / 2f;
                    float cy = sun.Top + sun.Height / 2f;
                    float r1 = sun.Width * 0.62f;
                    float r2 = sun.Width * 0.88f;
                    for (int i = 0; i < 8; i++)
                    {
                        double a = Math.PI / 4 * i;
                        g.DrawLine(ray,
                            (float)(cx + Math.Cos(a) * r1), (float)(cy + Math.Sin(a) * r1),
                            (float)(cx + Math.Cos(a) * r2), (float)(cy + Math.Sin(a) * r2));
                    }
                }
            }

            // 两处小闪光
            Sparkle(g, new Point(r.Left + (int)(r.Width * 0.05f), r.Bottom - (int)(r.Height * 0.12f)), 9);
            Sparkle(g, new Point(r.Right - (int)(r.Width * 0.08f), r.Top + (int)(r.Height * 0.34f)), 7);

            Rm(g);
        }

        private static void Sparkle(Graphics g, Point center, int size)
        {
            using (Pen pen = new Pen(Color.White, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int half = size / 2;
                g.DrawLine(pen, center.X - half, center.Y, center.X + half, center.Y);
                g.DrawLine(pen, center.X, center.Y - half, center.X, center.Y + half);
            }
        }

        public static void Chevron(Graphics g, Point center, int direction, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (direction == 0)
                {
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(center.X - 4, center.Y - 2),
                        new PointF(center.X, center.Y + 2),
                        new PointF(center.X + 4, center.Y - 2)
                    });
                }
                else
                {
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(center.X - 2, center.Y - 4),
                        new PointF(center.X + 2, center.Y),
                        new PointF(center.X - 2, center.Y + 4)
                    });
                }
            }
            Rm(g);
        }

        /// <summary>未勾选的空心圆。</summary>
        public static void Ring(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (Pen pen = new Pen(color, 1.6f))
                g.DrawEllipse(pen, r);
            Rm(g);
        }

        /// <summary>勾选态：实心圆 + 白勾。</summary>
        public static void CheckFilled(Graphics g, Rectangle r, Color color)
        {
            Sm(g);
            using (SolidBrush brush = new SolidBrush(color))
                g.FillEllipse(brush, r);
            float x = r.Left, y = r.Top, w = r.Width, h = r.Height;
            using (Pen pen = new Pen(Color.White, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new PointF[]
                {
                    new PointF(x + w * 0.28f, y + h * 0.52f),
                    new PointF(x + w * 0.45f, y + h * 0.70f),
                    new PointF(x + w * 0.74f, y + h * 0.32f)
                });
            }
            Rm(g);
        }

        private static void Sm(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // 高质量合成：抗锯齿边缘在彩色背景上更柔，不那么"毛毛的"
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        }

        private static void Rm(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.Default;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.Default;
        }
    }
}
