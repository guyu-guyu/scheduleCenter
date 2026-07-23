using System.Windows;
using Microsoft.Win32;
using ScheduleCenter.Core;
using ScheduleCenter.Gui;

namespace ScheduleCenter
{
    public partial class EditorWindow : Window
    {
        private readonly EditorViewModel _vm;

        public EditorWindow(ScheduledTaskService service, TaskInfo existing)
        {
            InitializeComponent();
            _vm = new EditorViewModel(service, existing);
            _vm.RequestClose += delegate(bool saved)
            {
                DialogResult = saved;
                Close();
            };
            DataContext = _vm;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) == true)
                _vm.Path = dlg.FileName;
        }
    }
}
