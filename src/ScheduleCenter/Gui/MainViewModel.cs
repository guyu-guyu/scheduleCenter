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

        internal void Refresh() { /* Task 11 */ }
        internal void ApplyFilter() { /* Task 11 */ }
        internal void NewTask() { /* Task 12 */ }
        internal void EditTask() { /* Task 13 */ }
        internal void DeleteTask() { /* Task 13 */ }
        internal void ToggleEnabled() { /* Task 13 */ }
        internal void RunTask() { /* Task 13 */ }
        internal void CopyCli() { /* Task 13 */ }
        internal void LoadHistory() { /* Task 14 */ }

        internal static void ShowError(TaskServiceException ex)
        {
            System.Windows.MessageBox.Show(ex.CodeName + ": " + ex.Message, "操作失败",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
