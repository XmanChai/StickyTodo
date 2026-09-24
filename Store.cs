using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace StickyTodo
{
    /// <summary>
    /// 数据与设置的读写中心，JSON 落盘于 %APPDATA%\StickyTodo\todos.json。
    /// 负责列表、任务、设置的增删改，以及旧版本（v1：无列表/无到期日）数据的自动迁移。
    /// </summary>
    public class Store
    {
        public static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyTodo");

        private const int CurrentVersion = 2;

        private readonly string _path;
        private AppData _data;
        private bool _dirty;

        /// <summary>任务或列表发生变化时触发。</summary>
        public event EventHandler Changed;

        public string LastError { get; private set; }

        public Store()
        {
            _path = Path.Combine(DataDirectory, "todos.json");
            Load();
        }

        public string FilePath
        {
            get { return _path; }
        }

        public AppSettings Settings
        {
            get { return _data.Settings; }
        }

        public List<TodoList> Lists
        {
            get { return _data.Lists; }
        }

        public List<TodoItem> Items
        {
            get { return _data.Todos; }
        }

        public int PendingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _data.Todos.Count; i++)
                    if (!_data.Todos[i].Done) count++;
                return count;
            }
        }

        public int TotalCount
        {
            get { return _data.Todos.Count; }
        }

        public TodoList FindList(string id)
        {
            for (int i = 0; i < _data.Lists.Count; i++)
                if (_data.Lists[i].Id == id) return _data.Lists[i];
            return null;
        }

        public TodoList FirstList
        {
            get { return _data.Lists.Count > 0 ? _data.Lists[0] : null; }
        }

        public string DefaultListId
        {
            get
            {
                TodoList list = FirstList;
                return list != null ? list.Id : string.Empty;
            }
        }

        // ---------- 持久化 ----------

        public void Load()
        {
            AppData loaded = null;
            bool migrated = false;

            if (File.Exists(_path))
            {
                try
                {
                    using (FileStream fs = File.OpenRead(_path))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppData));
                        loaded = (AppData)ser.ReadObject(fs);
                    }
                }
                catch (Exception ex)
                {
                    LastError = "读取数据失败，已重建：" + ex.Message;
                    TryBackupBrokenFile();
                    loaded = null;
                }
            }

            if (loaded == null)
            {
                // 可能是旧版本文件，尝试按 v1 结构读一次
                MigrateResult result = TryLoadLegacy();
                if (result != null)
                {
                    loaded = result.Data;
                    migrated = true;
                }
            }

            if (loaded == null) loaded = new AppData();

            if (loaded.Lists == null) loaded.Lists = new List<TodoList>();
            if (loaded.Todos == null) loaded.Todos = new List<TodoItem>();
            if (loaded.Settings == null) loaded.Settings = new AppSettings();
            if (loaded.Settings.ViewStyles == null) loaded.Settings.ViewStyles = new List<ViewStyle>();
            if (string.IsNullOrEmpty(loaded.Settings.LastView)) loaded.Settings.LastView = ViewIds.MyDay;

            loaded.Todos.RemoveAll(IsBlankItem);

            // 任务找不到所属列表时，挂到第一个列表；一个列表都没有就留空
            // （留空的任务只在「今天 / 重要 / 任务」里出现，不会偷偷造出一个列表来）
            for (int i = 0; i < loaded.Todos.Count; i++)
            {
                TodoItem item = loaded.Todos[i];
                if (!string.IsNullOrEmpty(item.ListId)
                    && loaded.Lists.Exists(delegate (TodoList l) { return l.Id == item.ListId; }))
                    continue;

                if (loaded.Lists.Count == 0)
                {
                    if (item.ListId != string.Empty) { item.ListId = string.Empty; migrated = true; }
                    continue;
                }

                item.ListId = loaded.Lists[0].Id;
                migrated = true;
            }

            _data = loaded;
            _dirty = migrated;
            if (migrated) Save();
        }

        private class MigrateResult
        {
            public AppData Data;
        }

        /// <summary>旧版 v1 文件（只有 todos + settings，没有 lists / dueDay / myDay）。</summary>
        private MigrateResult TryLoadLegacy()
        {
            try
            {
                if (!File.Exists(_path)) return null;

                LegacyData legacy;
                using (FileStream fs = File.OpenRead(_path))
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(LegacyData));
                    legacy = (LegacyData)ser.ReadObject(fs);
                }
                if (legacy == null || legacy.Todos == null) return null;

                AppData data = new AppData();
                data.Lists.Add(CreateList("工作", 0));
                data.Lists.Add(CreateList("个人", 2));
                string listId = data.Lists[0].Id;

                for (int i = 0; i < legacy.Todos.Count; i++)
                {
                    LegacyItem old = legacy.Todos[i];
                    if (old == null || string.IsNullOrEmpty(old.Title)) continue;
                    TodoItem item = new TodoItem();
                    if (!string.IsNullOrEmpty(old.Id)) item.Id = old.Id;
                    item.Title = old.Title;
                    item.Done = old.Done;
                    item.Created = old.Created;
                    item.Priority = old.Priority >= 2 ? 2 : 0;
                    item.ListId = listId;
                    data.Todos.Add(item);
                }

                LastError = null;
                MigrateResult result = new MigrateResult();
                result.Data = data;
                return result;
            }
            catch
            {
                return null;
            }
        }

        [DataContract]
        private class LegacyData
        {
            [DataMember(Name = "todos")] public List<LegacyItem> Todos { get; set; }
        }

        [DataContract]
        private class LegacyItem
        {
            [DataMember(Name = "id")] public string Id { get; set; }
            [DataMember(Name = "title")] public string Title { get; set; }
            [DataMember(Name = "done")] public bool Done { get; set; }
            [DataMember(Name = "created")] public long Created { get; set; }
            [DataMember(Name = "priority")] public int Priority { get; set; }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);

                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppData));
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    ser.WriteObject(ms, _data);
                    bytes = ms.ToArray();
                }

                string tmp = _path + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(_path))
                {
                    try
                    {
                        File.Replace(tmp, _path, null);
                    }
                    catch
                    {
                        File.Copy(tmp, _path, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, _path);
                }
                LastError = null;
                _dirty = false;
            }
            catch (Exception ex)
            {
                LastError = "保存数据失败：" + ex.Message;
            }
        }

        private void TryBackupBrokenFile()
        {
            try
            {
                string backup = _path + ".broken";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(_path, backup);
            }
            catch { }
        }

        private static bool IsBlankItem(TodoItem item)
        {
            return item == null || string.IsNullOrEmpty(item.Title);
        }

        public void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Commit()
        {
            Save();
            RaiseChanged();
        }

        // ---------- 列表 ----------

        public TodoList CreateList(string name, int paletteIndex)
        {
            TodoList list = new TodoList();
            list.Name = name;
            int index = Math.Abs(paletteIndex) % Design.Palette.Length;
            list.ColorArgb = Design.Palette[index].ToArgb();
            list.BackgroundKind = -1;      // 未设置：按主题色渐变
            list.BackgroundValue = string.Empty;
            return list;
        }

        public TodoList AddList(string name)
        {
            TodoList list = CreateList(name, _data.Lists.Count);
            _data.Lists.Add(list);
            Commit();
            return list;
        }

        /// <summary>把一个已经配置好的列表加入数据（新建列表编辑器用）。</summary>
        public void AddListObject(TodoList list)
        {
            if (list == null) return;
            _data.Lists.Add(list);
            Commit();
        }

        public void RenameList(TodoList list, string name)
        {
            if (list == null || string.IsNullOrEmpty(name) || list.Name == name) return;
            list.Name = name;
            Commit();
        }

        public void SetListColor(TodoList list, int paletteIndex)
        {
            if (list == null) return;
            list.ColorArgb = Design.Palette[Math.Abs(paletteIndex) % Design.Palette.Length].ToArgb();
            Commit();
        }

        /// <summary>设置列表背景：kind 0 纯色 / 1 渐变 / 2 图片。</summary>
        public void SetListBackground(TodoList list, int kind, string value)
        {
            if (list == null) return;
            list.BackgroundKind = kind;
            list.BackgroundValue = value == null ? string.Empty : value;
            Commit();
        }

        // ---------- 内置视图的外观（每个版块都能单独设背景） ----------

        public static Color DefaultViewColor(string viewId)
        {
            if (viewId == ViewIds.MyDay) return Design.MyDayColor;
            if (viewId == ViewIds.Important) return Design.ImportantColor;
            if (viewId == ViewIds.Planned) return Design.PlannedColor;
            if (viewId == ViewIds.Done) return Design.DoneColor;
            return Design.TasksColor;
        }

        public ViewStyle FindViewStyle(string viewId)
        {
            if (_data.Settings.ViewStyles == null) _data.Settings.ViewStyles = new List<ViewStyle>();
            for (int i = 0; i < _data.Settings.ViewStyles.Count; i++)
                if (_data.Settings.ViewStyles[i].View == viewId) return _data.Settings.ViewStyles[i];
            return null;
        }

        private ViewStyle EnsureViewStyle(string viewId)
        {
            ViewStyle style = FindViewStyle(viewId);
            if (style != null) return style;

            style = new ViewStyle();
            style.View = viewId;
            style.ColorArgb = DefaultViewColor(viewId).ToArgb();
            style.BackgroundKind = -1;
            _data.Settings.ViewStyles.Add(style);
            return style;
        }

        /// <summary>
        /// 解析任意视图的外观：自定义列表取列表自身，内置视图取 ViewStyle（没有则用默认主色 + 未设置背景）。
        /// </summary>
        public void ResolveViewStyle(string viewId, out Color color, out int bgKind, out string bgValue)
        {
            TodoList list = FindList(viewId);
            if (list != null)
            {
                color = list.Color;
                bgKind = list.BackgroundKind;
                bgValue = list.BackgroundValue == null ? string.Empty : list.BackgroundValue;
                return;
            }

            ViewStyle style = FindViewStyle(viewId);
            if (style != null)
            {
                Color fallback = DefaultViewColor(viewId);
                Color picked;
                try { picked = Color.FromArgb(style.ColorArgb); }
                catch { picked = fallback; }
                color = style.ColorArgb == 0 ? fallback : picked;
                bgKind = style.BackgroundKind;
                bgValue = style.BackgroundValue == null ? string.Empty : style.BackgroundValue;
                return;
            }

            color = DefaultViewColor(viewId);
            bgKind = -1;
            bgValue = string.Empty;
        }

        /// <summary>给内置视图设置背景（kind：-1 默认 / 0 纯色 / 1 渐变 / 2 图片）。</summary>
        public void SetViewBackground(string viewId, int kind, string value)
        {
            ViewStyle style = EnsureViewStyle(viewId);
            style.BackgroundKind = kind;
            style.BackgroundValue = value == null ? string.Empty : value;
            Commit();
        }

        /// <summary>给内置视图设置主色（纯色背景下同时会改侧栏颜色）。</summary>
        public void SetViewColor(string viewId, int argb)
        {
            ViewStyle style = EnsureViewStyle(viewId);
            style.ColorArgb = argb;
            Commit();
        }

        public void RemoveList(TodoList list)
        {
            if (list == null) return;

            bool removed = _data.Lists.Remove(list);
            if (!removed) return;

            _data.Todos.RemoveAll(delegate (TodoItem t) { return t.ListId == list.Id; });

            // 允许把列表全部删光（不再自动补一个「工作」回来）
            Commit();
        }

        // ---------- 任务 ----------

        public TodoItem Add(string title, string listId)
        {
            return Add(title, listId, 0);
        }

        /// <summary>
        /// 新增任务；priority 2 = 重要。
        /// 所有入口加进来的任务都会记上时间（Created 时间戳）并标记为「今天」，
        /// 所以不管在哪一栏加的，都能在「今天」里看到（跨天后自动离开「今天」）。
        /// </summary>
        public TodoItem Add(string title, string listId, int priority)
        {
            TodoItem item = new TodoItem();
            item.Title = title;
            item.Priority = priority >= 2 ? 2 : 0;
            item.ListId = string.IsNullOrEmpty(listId) ? DefaultListId : listId;
            item.MyDay = TodoItem.Today;
            _data.Todos.Add(item);
            Commit();
            return item;
        }

        /// <summary>切换任务的重要标记。</summary>
        public void ToggleImportant(TodoItem item)
        {
            if (item == null) return;
            SetImportant(item, item.Priority < 2);
        }

        public void SetListIcon(TodoList list, int iconKind)
        {
            if (list == null || list.IconKind == iconKind) return;
            list.IconKind = iconKind;
            Commit();
        }

        public void Update(TodoItem item, string title)
        {
            if (item == null) return;
            string clean = title == null ? string.Empty : title.Trim();
            if (clean.Length == 0) return;

            bool changed = false;
            if (item.Title != clean) { item.Title = clean; changed = true; }
            if (changed) Commit();
        }

        public void SetDone(TodoItem item, bool done)
        {
            if (item == null || item.Done == done) return;
            item.Done = done;
            Commit();
        }

        public void SetImportant(TodoItem item, bool important)
        {
            if (item == null) return;
            int wanted = important ? 2 : 0;
            if (item.Priority == wanted) return;
            item.Priority = wanted;
            Commit();
        }

        public void SetDueDay(TodoItem item, int day)
        {
            if (item == null || item.DueDay == day) return;
            item.DueDay = day;
            Commit();
        }

        public void SetMyDay(TodoItem item, int day)
        {
            if (item == null || item.MyDay == day) return;
            item.MyDay = day;
            Commit();
        }

        public void MoveToList(TodoItem item, TodoList list)
        {
            if (item == null || list == null || item.ListId == list.Id) return;
            item.ListId = list.Id;
            Commit();
        }

        public void Remove(TodoItem item)
        {
            if (item == null) return;
            if (_data.Todos.Remove(item)) Commit();
        }

        public void RemoveRange(List<TodoItem> items)
        {
            if (items == null || items.Count == 0) return;
            bool removed = false;
            for (int i = 0; i < items.Count; i++)
                if (_data.Todos.Remove(items[i])) removed = true;
            if (removed) Commit();
        }

        public int ClearDoneInList(string listId)
        {
            int removed = _data.Todos.RemoveAll(delegate (TodoItem t)
            {
                return t.Done && (string.IsNullOrEmpty(listId) || t.ListId == listId);
            });
            if (removed > 0) Commit();
            return removed;
        }

        /// <summary>
        /// 按「视图语义」删除该视图下的任务（内置视图如今天/重要/任务，或某个自定义列表）。
        /// completedOnly = true 时只删已完成的。返回删除条数。
        /// </summary>
        public int ClearView(string viewId, bool completedOnly)
        {
            List<TodoItem> items = new List<TodoItem>();
            items.AddRange(Query(viewId, true));
            if (!completedOnly) items.AddRange(Query(viewId, false));

            int removed = items.Count;
            RemoveRange(items);
            return removed;
        }

        // ---------- 视图查询 ----------

        /// <summary>某个视图（内置或自定义列表）下的任务。</summary>
        public List<TodoItem> Query(string viewId, bool completed)
        {
            List<TodoItem> result = new List<TodoItem>();
            int today = TodoItem.Today;

            for (int i = 0; i < _data.Todos.Count; i++)
            {
                TodoItem item = _data.Todos[i];
                if (item == null) continue;
                if (item.Done != completed) continue;

                bool match;
                if (viewId == ViewIds.MyDay) match = item.MyDay == today && today != 0;
                else if (viewId == ViewIds.Important) match = item.Priority >= 2;
                else if (viewId == ViewIds.Planned) match = item.DueDay != 0;
                else if (viewId == ViewIds.Done) match = true;
                else if (viewId == ViewIds.Tasks) match = true;
                else match = item.ListId == viewId;

                if (match) result.Add(item);
            }

            result.Sort(delegate (TodoItem a, TodoItem b)
            {
                // 未完成在前，其次有到期日的在前、到期日升序，最后按标题/创建时间
                if (a.Done != b.Done) return a.Done ? 1 : -1;
                if ((a.DueDay != 0) != (b.DueDay != 0)) return a.DueDay != 0 ? -1 : 1;
                if (a.DueDay != b.DueDay) return a.DueDay.CompareTo(b.DueDay);
                return a.Created.CompareTo(b.Created);
            });
            return result;
        }

        /// <summary>视图下未完成 / 已完成的条数。</summary>
        public void CountView(string viewId, out int active, out int done)
        {
            active = Query(viewId, false).Count;
            done = Query(viewId, true).Count;
        }

        /// <summary>侧边栏「今天」上的数字。</summary>
        public int MyDayCount
        {
            get { return Query(ViewIds.MyDay, false).Count; }
        }

        public bool IsDirty
        {
            get { return _dirty; }
        }
    }
}
