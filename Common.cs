using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>Win32 互操作辅助。</summary>
    internal static class Native
    {
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int HT_CAPTION = 0x0002;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>用系统机制拖动无边框窗口。</summary>
        public static void DragWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            ReleaseCapture();
            SendMessage(handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        public static void FreeIconHandle(IntPtr hIcon)
        {
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }

        public static void ActivateWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try { SetForegroundWindow(handle); }
            catch { }
        }

    }

    /// <summary>运行时绘制托盘图标：紫色圆角方块 + 白色对勾，未完成数作为红色角标。</summary>
    internal static class IconFactory
    {
        public static Icon Create(int pending)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    Color fill = Design.BrandPurple;
                    Rectangle body = new Rectangle(3, 4, 24, 24);
                    using (GraphicsPath path = RoundedSquare(body, 6))
                    using (SolidBrush brush = new SolidBrush(fill))
                        g.FillPath(brush, path);

                    using (Pen pen = new Pen(Color.White, 2.6f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(body.Left + body.Width * 0.26f, body.Top + body.Height * 0.52f),
                            new PointF(body.Left + body.Width * 0.45f, body.Top + body.Height * 0.71f),
                            new PointF(body.Left + body.Width * 0.76f, body.Top + body.Height * 0.30f)
                        });
                    }

                    if (pending > 0)
                    {
                        string text = pending > 99 ? "99" : pending.ToString();
                        Rectangle badge = new Rectangle(14, 14, 18, 18);
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(214, 48, 49)))
                            g.FillEllipse(brush, badge);
                        using (Font f = new Font("Segoe UI", pending > 9 ? 7.5f : 10f, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (SolidBrush fg = new SolidBrush(Color.White))
                        {
                            SizeF sz = g.MeasureString(text, f);
                            g.DrawString(text, f, fg,
                                badge.X + (badge.Width - sz.Width) / 2f,
                                badge.Y + (badge.Height - sz.Height) / 2f);
                        }
                    }
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(handle))
                        return (Icon)temp.Clone();
                }
                finally
                {
                    Native.FreeIconHandle(handle);
                }
            }
        }

        private static GraphicsPath RoundedSquare(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>界面统一字体。</summary>
    internal static class UiFont
    {
        private static readonly string Family = ResolveFamily();

        private static string ResolveFamily()
        {
            string[] candidates = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "Tahoma" };
            for (int i = 0; i < candidates.Length; i++)
            {
                string name = candidates[i];
                try
                {
                    using (Font probe = new Font(name, 9f))
                    {
                        if (string.Equals(probe.Name, name, StringComparison.OrdinalIgnoreCase))
                            return name;
                    }
                }
                catch { }
            }
            return "Segoe UI";
        }

        public static Font Get(float size)
        {
            return new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Get(float size, FontStyle style)
        {
            return new Font(Family, size, style, GraphicsUnit.Point);
        }

        public static string FamilyName
        {
            get { return Family; }
        }
    }

    /// <summary>开机自启（HKCU Run 项），默认关闭。</summary>
    internal static class AutoStart
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "StickyTodo";

        public static bool IsEnabled()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(ValueName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch { return false; }
        }

        public static bool Set(bool enabled)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key =
                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        string exe = Application.ExecutablePath;
                        key.SetValue(ValueName, "\"" + exe + "\"", Microsoft.Win32.RegistryValueKind.String);
                    }
                    else if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch { return false; }
        }
    }
}
