using System;
using System.Threading;
using System.Windows.Forms;

namespace StickyTodo
{
    internal static class Program
    {
        private const string MutexName = "StickyTodo.SingleInstance.Mutex";
        private const string ShowEventName = "StickyTodo.Show.Event";

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                SignalRunningInstance();
                return;
            }

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;

                EventWaitHandle showEvent = new EventWaitHandle(
                    false, EventResetMode.AutoReset, ShowEventName);

                TrayApp app = new TrayApp(showEvent);
                Application.Run(app);
            }
            finally
            {
                try { mutex.ReleaseMutex(); }
                catch { }
                mutex.Close();
            }
        }

        private static void SignalRunningInstance()
        {
            try
            {
                EventWaitHandle handle;
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out handle))
                {
                    using (handle) handle.Set();
                }
            }
            catch { }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string message = e.Exception == null ? "未知错误" : e.Exception.Message;
            MessageBox.Show(message, "便签 TODO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
