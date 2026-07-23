using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ScheduleCenter.Core;

namespace ScheduleCenter.Gui
{
    public sealed class MainViewModel : ViewModelBase
    {
        internal readonly ScheduledTaskService Service = new ScheduledTaskService();
        private List<TaskInfo> _allTasks = new List<TaskInfo>();

        public ObservableCollection<FolderNode> Folders { get; private set; }
        public ObservableCollection<TaskRowViewModel> Tasks { get; private set; }
        public ObservableCollection<HistoryRowViewModel> HistoryEvents { get; private set; }

        private FolderNode _selectedFolder;
        public FolderNode SelectedFolder
        {
            get { return _selectedFolder; }
            set { if (Set(ref _selectedFolder, value)) ApplyFilter(); }
        }

        private TaskRowViewModel _selectedTask;
        public TaskRowViewModel SelectedTask
        {
            get { return _selectedTask; }
            set { if (Set(ref _selectedTask, value)) LoadHistory(); }
        }

        private string _searchText = "";
        public string SearchText
        {
            get { return _searchText; }
            set { if (Set(ref _searchText, value)) ApplyFilter(); }
        }

        private string _statusText = "就绪";
        public string StatusText
        {
            get { return _statusText; }
            set { Set(ref _statusText, value); }
        }

        private string _historyTitle = "运行历史";
        public string HistoryTitle
        {
            get { return _historyTitle; }
            set { Set(ref _historyTitle, value); }
        }

        private string _historyFilter = "全部";
        public string HistoryFilter
        {
            get { return _historyFilter; }
            set { if (Set(ref _historyFilter, value)) LoadHistory(); }
        }

        public ICommand NewCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand EditCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand ToggleEnabledCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand CopyCliCommand { get; private set; }
        public ICommand RefreshHistoryCommand { get; private set; }

        public MainViewModel()
        {
            Folders = new ObservableCollection<FolderNode>();
            Tasks = new ObservableCollection<TaskRowViewModel>();
            HistoryEvents = new ObservableCollection<HistoryRowViewModel>();

            NewCommand = new RelayCommand(NewTask);
            RefreshCommand = new RelayCommand(Refresh);
            EditCommand = new RelayCommand(EditTask, () => SelectedTask != null);
            DeleteCommand = new RelayCommand(DeleteTask, () => SelectedTask != null);
            ToggleEnabledCommand = new RelayCommand(ToggleEnabled, () => SelectedTask != null);
            RunCommand = new RelayCommand(RunTask, () => SelectedTask != null);
            CopyCliCommand = new RelayCommand(CopyCli, () => SelectedTask != null);
            RefreshHistoryCommand = new RelayCommand(LoadHistory, () => SelectedTask != null);
        }

        internal void Refresh()
        {
            try
            {
                _allTasks = new List<TaskInfo>(Service.List(null));
                BuildFolderTree();
                ApplyFilter();
                StatusText = "共 " + _allTasks.Count + " 个任务";
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void ApplyFilter()
        {
            Tasks.Clear();
            string folderPath = SelectedFolder == null ? "" : SelectedFolder.FullPath;
            foreach (TaskInfo t in _allTasks)
            {
                if (!string.IsNullOrEmpty(folderPath))
                {
                    string expected = ScheduledTaskService.RootFolderPath + "\\" + folderPath;
                    if (!string.Equals(t.Folder, expected, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                if (!string.IsNullOrEmpty(SearchText) &&
                    t.RelativeName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Tasks.Add(new TaskRowViewModel(t));
            }
        }

        private void BuildFolderTree()
        {
            var root = new FolderNode { Name = "ScheduleCenter", FullPath = "" };
            var nodes = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            nodes[""] = root;

            foreach (TaskInfo t in _allTasks)
            {
                string folder = t.Folder;
                if (!folder.StartsWith(ScheduledTaskService.RootFolderPath)) continue;
                string rel = folder.Length > ScheduledTaskService.RootFolderPath.Length
                    ? folder.Substring(ScheduledTaskService.RootFolderPath.Length + 1)
                    : "";
                if (string.IsNullOrEmpty(rel) || nodes.ContainsKey(rel)) continue;

                string[] parts = rel.Split('\\');
                string path = "";
                FolderNode parent = root;
                foreach (string part in parts)
                {
                    path = path.Length == 0 ? part : path + "\\" + part;
                    FolderNode node;
                    if (!nodes.TryGetValue(path, out node))
                    {
                        node = new FolderNode { Name = part, FullPath = path };
                        nodes[path] = node;
                        parent.Children.Add(node);
                    }
                    parent = node;
                }
            }

            Folders.Clear();
            Folders.Add(root);
        }
        internal void NewTask()
        {
            var editor = new EditorWindow(Service, null);
            editor.Owner = System.Windows.Application.Current.MainWindow;
            if (editor.ShowDialog() == true)
                Refresh();
        }
        internal void EditTask()
        {
            if (SelectedTask == null) return;
            try
            {
                TaskInfo current = Service.Get(SelectedTask.Info.RelativeName);
                var editor = new EditorWindow(Service, current);
                editor.Owner = System.Windows.Application.Current.MainWindow;
                if (editor.ShowDialog() == true)
                    Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void DeleteTask()
        {
            if (SelectedTask == null) return;
            string name = SelectedTask.Info.RelativeName;
            System.Windows.MessageBoxResult confirm = System.Windows.MessageBox.Show(
                "确定要删除任务 '" + name + "' 吗？", "确认删除",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (confirm != System.Windows.MessageBoxResult.Yes) return;
            try
            {
                Service.Delete(name, true);
                Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void ToggleEnabled()
        {
            if (SelectedTask == null) return;
            try
            {
                Service.SetEnabled(SelectedTask.Info.RelativeName, !SelectedTask.Info.Enabled);
                Refresh();
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void RunTask()
        {
            if (SelectedTask == null) return;
            try
            {
                Service.Run(SelectedTask.Info.RelativeName);
                StatusText = "已触发运行: " + SelectedTask.Info.RelativeName;
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }

        internal void CopyCli()
        {
            if (SelectedTask == null) return;
            try
            {
                TaskInfo current = Service.Get(SelectedTask.Info.RelativeName);
                System.Windows.Clipboard.SetText(Service.BuildAddCommand(current));
                StatusText = "CLI 命令已复制到剪贴板";
            }
            catch (TaskServiceException ex)
            {
                ShowError(ex);
            }
        }
        internal void LoadHistory() { /* Task 14 */ }

        internal static void ShowError(TaskServiceException ex)
        {
            System.Windows.MessageBox.Show(ex.CodeName + ": " + ex.Message, "操作失败",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
