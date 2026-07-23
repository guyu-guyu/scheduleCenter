using System.Windows;
using System.Windows.Controls;
using ScheduleCenter.Gui;

namespace ScheduleCenter
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            Loaded += delegate { _vm.RefreshCommand.Execute(null); };
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _vm.SelectedFolder = e.NewValue as FolderNode;
        }

        private void TaskGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm.SelectedTask != null && _vm.EditCommand.CanExecute(null))
                _vm.EditCommand.Execute(null);
        }

        private void HistoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ComboBox).SelectedItem as ComboBoxItem;
            if (item != null)
                _vm.HistoryFilter = item.Content.ToString();
        }
    }
}
