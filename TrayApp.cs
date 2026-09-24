using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>托盘应用上下文：持有主窗口、托盘图标与菜单。</summary>
    public class TrayApp : ApplicationContext
    {
        private readonly Store _store;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToDoForm _main;

        private Icon _icon;
        private readonly EventWaitHandle _showEvent;
        private Thread _listener;
        private volatile bool _shuttingDown;

        public TrayApp(EventWaitHandle showEvent)
        {
            _showEvent = showEvent;
            _store = new Store();

            _main = new ToDoForm(_store);
            _main.FormClosed += Main_FormClosed;

            _menu = new ContextMenuStrip();
            _menu.Font = UiFont.Get(9.5f);
            _menu.Opening += Menu_Opening;

            _icon = IconFactory.Create(_store.PendingCount);

            _tray = new NotifyIcon();
            _tray.Icon = _icon;
            _tray.Text = BuildTooltip();
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.DoubleClick += Tray_DoubleClick;

            _store.Changed += Store_Changed;

            StartListener();

            if (_store.Settings.ShowOnStart) ShowMain(false);
        }

        // ---------- 托盘交互 ----------

        private void Tray_DoubleClick(object sender, EventArgs e)
        {
            if (_main.Visible && _main.WindowState != FormWindowState.Minimized)
                _main.HideToTray();
            else
                ShowMain(false);
        }

        private void Menu_Opening(object sender, CancelEventArgs e)
        {
            _menu.Items.Clear();

            _menu.Items.Add("显示窗口", null, delegate { ShowMain(false); });
            _menu.Items.Add("新建任务…", null, delegate { ShowMain(true); });

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("今天（" + _store.MyDayCount + "）", null, delegate { ShowView(ViewIds.MyDay); });
            _menu.Items.Add("重要（" + _store.Query(ViewIds.Important, false).Count + "）", null,
                delegate { ShowView(ViewIds.Important); });
            _menu.Items.Add("任务（" + _store.Query(ViewIds.Tasks, false).Count + "）", null,
                delegate { ShowView(ViewIds.Done); });

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("设置…", null, delegate { ShowSettings(); });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("退出", null, delegate { ExitApp(); });
        }

        private void ShowMain(bool focusNewTask)
        {
            _main.ShowAndActivate(focusNewTask);
        }

        private void ShowView(string viewId)
        {
            _main.ShowAndActivate(false);
            _main.ActivateView(viewId);
        }

        private void ShowSettings()
        {
            using (SettingsForm dialog = new SettingsForm(_store))
            {
                dialog.ShowDialog();
            }
            if (!_main.Visible) _main.ShowAndActivate(false);
        }

        // ---------- 状态同步 ----------

        private void Store_Changed(object sender, EventArgs e)
        {
            UpdateTrayIcon();
            _tray.Text = BuildTooltip();
        }

        private string BuildTooltip()
        {
            return string.Format("便签 TODO — 未完成 {0} 条，共 {1} 条",
                _store.PendingCount, _store.TotalCount);
        }

        private void UpdateTrayIcon()
        {
            Icon fresh = IconFactory.Create(_store.PendingCount);
            _tray.Icon = fresh;
            Icon old = _icon;
            _icon = fresh;
            if (old != null) old.Dispose();
        }

        // ---------- 单实例唤醒 ----------

        private void StartListener()
        {
            _listener = new Thread(delegate ()
            {
                while (!_shuttingDown)
                {
                    bool signaled;
                    try { signaled = _showEvent.WaitOne(400); }
                    catch { break; }

                    if (!signaled) continue;
                    if (_shuttingDown) break;

                    try { _main.BeginInvoke(new MethodInvoker(delegate { ShowMain(false); })); }
                    catch { }
                }
            });
            _listener.IsBackground = true;
            _listener.Start();
        }

        // ---------- 退出 ----------

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (!_shuttingDown) ExitApp();
        }

        public void ExitApp()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;

            _store.Save();

            _tray.Visible = false;
            _tray.Dispose();

            try { if (!_main.IsDisposed) _main.ForceClose(); }
            catch { }

            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shuttingDown = true;
                if (_icon != null) _icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
