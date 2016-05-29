using System.Windows;

namespace dotnet_new3.VSIX
{
    /// <summary>
    /// Interaction logic for InfoCollectorDialog.xaml
    /// </summary>
    public partial class InfoCollectorDialog
    {
        public InfoCollectorDialog()
        {
            InitializeComponent();
        }

        public InfoCollectorDialog(string name)
            : this()
        {
            AuthorTextBox.Text = "Me";
            FriendlyNameTextBox.Text = name;
            DefaultNameTextBox.Text = name;
            ShortNameTextBox.Text = name;
        }

        public string FriendlyName => FriendlyNameTextBox.Text;

        public string DefaultName => DefaultNameTextBox.Text;

        public string ShortName => ShortNameTextBox.Text;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
