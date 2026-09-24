using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StickyTodo
{
    /// <summary>设置窗口。</summary>
    public class SettingsForm : Form
    {
        private readonly Store _store;

        private CheckBox _chkAutoStart;
        private CheckBox _chkShowOnStart;
        private CheckBox _chkOnTop;
        private CheckBox _chkMinimize;
        private CheckBox _chkConfirmDelete;
        private TrackBar _opacity;
        private Label _opacityLabel;

        public SettingsForm(Store store)
        {
            _store = store;
            BuildUi();
            LoadValues();
        }

        private void BuildUi()
        {
            SuspendLayout();

            Text = "设置";
            Font = UiFont.Get(9.5f);
            ClientSize = new Size(420, 372);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.White;
            try { Icon = IconFactory.Create(0); }
            catch { }

            GroupBox general = new GroupBox();
            general.Text = "常规";
            general.SetBounds(12, 10, 396, 132);

            _chkAutoStart = MakeCheck("开机自动启动（默认关闭）", 22);
            _chkMinimize = MakeCheck("关闭窗口时收起进托盘（不勾选则关闭即退出）", 48);
            _chkConfirmDelete = MakeCheck("删除前二次确认", 74);
            _chkShowOnStart = MakeCheck("启动时显示窗口", 100);

            general.Controls.Add(_chkAutoStart);
            general.Controls.Add(_chkMinimize);
            general.Controls.Add(_chkConfirmDelete);
            general.Controls.Add(_chkShowOnStart);

            GroupBox window = new GroupBox();
            window.Text = "窗口";
            window.SetBounds(12, 150, 396, 118);

            _chkOnTop = MakeCheck("窗口置顶显示", 22);

            Label opacityCaption = new Label();
            opacityCaption.Text = "不透明度";
            opacityCaption.SetBounds(16, 56, 60, 20);

            _opacityLabel = new Label();
            _opacityLabel.SetBounds(322, 56, 60, 20);
            _opacityLabel.Text = "100%";

            // 先建好标签再创建 TrackBar：设置 Minimum 会触发 ValueChanged。
            _opacity = new TrackBar();
            _opacity.Minimum = 50;
            _opacity.Maximum = 100;
            _opacity.TickFrequency = 10;
            _opacity.SetBounds(76, 48, 240, 45);
            _opacity.ValueChanged += delegate { _opacityLabel.Text = _opacity.Value + "%"; };

            Button openData = new Button();
            openData.Text = "打开数据目录";
            openData.SetBounds(16, 84, 116, 26);
            openData.FlatStyle = FlatStyle.System;
            openData.Click += OpenData_Click;

            window.Controls.Add(_chkOnTop);
            window.Controls.Add(opacityCaption);
            window.Controls.Add(_opacity);
            window.Controls.Add(_opacityLabel);
            window.Controls.Add(openData);

            Button save = new Button();
            save.Text = "保存";
            save.SetBounds(216, 328, 88, 30);
            save.FlatStyle = FlatStyle.System;
            save.Click += Save_Click;

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(310, 328, 88, 30);
            cancel.FlatStyle = FlatStyle.System;
            cancel.DialogResult = DialogResult.Cancel;

            Label hint = new Label();
            hint.SetBounds(14, 280, 392, 40);
            hint.ForeColor = Color.FromArgb(110, 110, 110);
            hint.Text = "数据文件：" + _store.FilePath
                + "\r\n列表、任务、到期日与重要标记都会保存在这里。";

            Controls.Add(general);
            Controls.Add(window);
            Controls.Add(hint);
            Controls.Add(save);
            Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;

            ResumeLayout(false);
        }

        private CheckBox MakeCheck(string text, int top)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.SetBounds(16, top, 364, 22);
            return box;
        }

        private void LoadValues()
        {
            AppSettings settings = _store.Settings;

            _chkAutoStart.Checked = AutoStart.IsEnabled();
            _chkMinimize.Checked = settings.MinimizeToTray;
            _chkConfirmDelete.Checked = settings.ConfirmDelete;
            _chkShowOnStart.Checked = settings.ShowOnStart;
            _chkOnTop.Checked = settings.OnTop;

            int value = settings.Opacity;
            if (value < _opacity.Minimum) value = _opacity.Minimum;
            if (value > _opacity.Maximum) value = _opacity.Maximum;
            _opacity.Value = value;
            _opacityLabel.Text = value + "%";
        }

        private void Save_Click(object sender, EventArgs e)
        {
            AppSettings settings = _store.Settings;

            settings.MinimizeToTray = _chkMinimize.Checked;
            settings.ConfirmDelete = _chkConfirmDelete.Checked;
            settings.ShowOnStart = _chkShowOnStart.Checked;
            settings.OnTop = _chkOnTop.Checked;
            settings.Opacity = _opacity.Value;

            bool autoStartWanted = _chkAutoStart.Checked;
            if (AutoStart.IsEnabled() != autoStartWanted && !AutoStart.Set(autoStartWanted))
            {
                MessageBox.Show(this, "写入开机启动项失败，可能被安全软件拦截。", "便签 TODO",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            settings.AutoStart = autoStartWanted;

            _store.Save();
            _store.RaiseChanged();

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OpenData_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(Store.DataDirectory))
                    Directory.CreateDirectory(Store.DataDirectory);
                Process.Start("explorer.exe", "\"" + Store.DataDirectory + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开目录：" + ex.Message, "便签 TODO",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
