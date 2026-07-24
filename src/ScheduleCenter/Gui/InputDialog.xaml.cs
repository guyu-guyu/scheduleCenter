using System.Windows;

namespace ScheduleCenter.Gui
{
    public partial class InputDialog : Window
    {
        public string InputText { get { return InputBox.Text; } }
        public bool OverwriteExisting { get { return OverwriteCheckBox.IsChecked == true; } }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue ?? "";
            InputBox.SelectAll();
            InputBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
