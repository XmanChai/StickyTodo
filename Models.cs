using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace StickyTodo
{
    /// <summary>一个自定义列表（微软 To Do 里的「列表」）。</summary>
    [DataContract]
    public class TodoList
    {
        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; }

        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; }

        /// <summary>主题色 ARGB（列表头底色与侧边栏色点）。</summary>
        [DataMember(Name = "color", Order = 2)]
        public int ColorArgb { get; set; }

        /// <summary>
        /// 背景类型：-1 未设置（按主题色渐变）、0 纯色、1 渐变、2 图片。
        /// 用 -1 而不是 0 作为「未设置」，因为 JSON 序列化会省略等于默认值的字段。
        /// </summary>
        [DataMember(Name = "bgKind", Order = 3)]
        public int BackgroundKind { get; set; }

        /// <summary>背景参数：渐变序号，或图片文件路径。</summary>
        [DataMember(Name = "bgValue", Order = 4)]
        public string BackgroundValue { get; set; }

        /// <summary>列表图标种类（见 Icons.ByKind）。默认 0 = 列表线条。</summary>
        [DataMember(Name = "icon", Order = 5)]
        public int IconKind { get; set; }

        public TodoList()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = string.Empty;
            ColorArgb = Design.Palette[0].ToArgb();
            BackgroundKind = -1;
            BackgroundValue = string.Empty;
            IconKind = 0;
        }

        /// <summary>背景色序号（kind = -1 或 1 时有效）。</summary>
        public int ThemeIndex
        {
            get
            {
                int index;
                if (int.TryParse(BackgroundValue, out index)) return index;
                return -1;
            }
        }

        public Color Color
        {
            get
            {
                try { return Color.FromArgb(ColorArgb); }
                catch { return Design.Palette[0]; }
            }
        }
    }

    /// <summary>单条任务。</summary>
    [DataContract]
    public class TodoItem
    {
        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; }

        [DataMember(Name = "title", Order = 1)]
        public string Title { get; set; }

        [DataMember(Name = "done", Order = 2)]
        public bool Done { get; set; }

        [DataMember(Name = "created", Order = 3)]
        public long Created { get; set; }

        /// <summary>重要（星标）。</summary>
        [DataMember(Name = "priority", Order = 4)]
        public int Priority { get; set; }

        /// <summary>所属列表。</summary>
        [DataMember(Name = "listId", Order = 5)]
        public string ListId { get; set; }

        /// <summary>到期日，yyyyMMdd；0 表示未设置。</summary>
        [DataMember(Name = "dueDay", Order = 6)]
        public int DueDay { get; set; }

        /// <summary>加入「我的一天」的日期，yyyyMMdd；0 表示不在我的一天。</summary>
        [DataMember(Name = "myDay", Order = 7)]
        public int MyDay { get; set; }

        public TodoItem()
        {
            Id = Guid.NewGuid().ToString("N");
            Title = string.Empty;
            Created = NowMs();
        }

        public static long NowMs()
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(DateTime.UtcNow - epoch).TotalMilliseconds;
        }

        public DateTime CreatedLocal
        {
            get
            {
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return epoch.AddMilliseconds(Created).ToLocalTime();
            }
        }

        public bool Important
        {
            get { return Priority >= 2; }
            set { Priority = value ? 2 : 0; }
        }

        public static int ToDay(DateTime date)
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }

        public static int Today
        {
            get { return ToDay(DateTime.Today); }
        }

        public static DateTime FromDay(int day)
        {
            if (day <= 0) return DateTime.MinValue;
            int y = day / 10000;
            int m = (day / 100) % 100;
            int d = day % 100;
            try { return new DateTime(y, m, d); }
            catch { return DateTime.MinValue; }
        }

        public DateTime DueDate
        {
            get { return FromDay(DueDay); }
        }

        public bool IsDueToday
        {
            get { return DueDay != 0 && DueDay == Today; }
        }

        public bool IsOverdue
        {
            get { return DueDay != 0 && DueDay < Today && !Done; }
        }

        /// <summary>到期日的显示文案（今天 / 明天 / 周X / M月d日）。</summary>
        public string DueText
        {
            get
            {
                if (DueDay == 0) return string.Empty;
                DateTime due = DueDate;
                if (due == DateTime.MinValue) return string.Empty;

                int delta = (int)(due.Date - DateTime.Today).Days;
                if (delta == 0) return "今天";
                if (delta == 1) return "明天";
                if (delta == -1) return "昨天";
                if (delta > 1 && delta < 7) return "周" + "日一二三四五六"[(int)due.DayOfWeek];
                if (delta < 0) return due.ToString("M月d日");
                if (due.Year == DateTime.Today.Year) return due.ToString("M月d日");
                return due.ToString("yyyy年M月d日");
            }
        }
    }

    /// <summary>
    /// 内置视图（我的一天 / 重要 / 计划 / 已完成）的外观设置：主色 + 背景。
    /// 自定义列表的背景存在 TodoList 自身里，这里只存内置视图的。
    /// </summary>
    [DataContract]
    public class ViewStyle
    {
        [DataMember(Name = "view", Order = 0)]
        public string View { get; set; }

        [DataMember(Name = "color", Order = 1)]
        public int ColorArgb { get; set; }

        /// <summary>背景类型：-1 未设置（按主色渐变）、0 纯色、1 渐变、2 图片。</summary>
        [DataMember(Name = "bgKind", Order = 2)]
        public int BackgroundKind { get; set; }

        [DataMember(Name = "bgValue", Order = 3)]
        public string BackgroundValue { get; set; }

        public ViewStyle()
        {
            View = string.Empty;
            ColorArgb = 0;
            BackgroundKind = -1;
            BackgroundValue = string.Empty;
        }
    }

    /// <summary>可持久化的设置项。</summary>
    [DataContract]
    public class AppSettings
    {
        [DataMember(Name = "autoStart")] public bool AutoStart { get; set; }
        [DataMember(Name = "minimizeToTray")] public bool MinimizeToTray { get; set; }
        [DataMember(Name = "confirmDelete")] public bool ConfirmDelete { get; set; }
        [DataMember(Name = "showOnStart")] public bool ShowOnStart { get; set; }
        [DataMember(Name = "onTop")] public bool OnTop { get; set; }
        [DataMember(Name = "opacity")] public int Opacity { get; set; }

        [DataMember(Name = "windowX")] public int WindowX { get; set; }
        [DataMember(Name = "windowY")] public int WindowY { get; set; }
        [DataMember(Name = "windowW")] public int WindowW { get; set; }
        [DataMember(Name = "windowH")] public int WindowH { get; set; }

        [DataMember(Name = "lastView")] public string LastView { get; set; }

        /// <summary>内置视图的外观（可按视图分别设置背景）。</summary>
        [DataMember(Name = "viewStyles")] public List<ViewStyle> ViewStyles { get; set; }

        public AppSettings()
        {
            ApplyDefaults();
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            ApplyDefaults();
        }

        private void ApplyDefaults()
        {
            AutoStart = false;
            MinimizeToTray = true;
            ConfirmDelete = true;
            ShowOnStart = true;
            OnTop = true;
            Opacity = 100;

            WindowX = -1;
            WindowY = -1;
            WindowW = 1000;
            WindowH = 640;

            LastView = ViewIds.MyDay;
            ViewStyles = new List<ViewStyle>();
        }

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }
    }

    /// <summary>内置视图 ID。</summary>
    public static class ViewIds
    {
        public const string MyDay = "myday";
        public const string Important = "important";
        public const string Planned = "planned";
        public const string Done = "done";
        public const string Tasks = "tasks";   // 全部任务
    }

    /// <summary>磁盘上的根对象。</summary>
    [DataContract]
    public class AppData
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; }

        [DataMember(Name = "lists", Order = 1)]
        public List<TodoList> Lists { get; set; }

        [DataMember(Name = "todos", Order = 2)]
        public List<TodoItem> Todos { get; set; }

        [DataMember(Name = "settings", Order = 3)]
        public AppSettings Settings { get; set; }

        public AppData()
        {
            Version = 2;
            Lists = new List<TodoList>();
            Todos = new List<TodoItem>();
            Settings = new AppSettings();
        }
    }
}
