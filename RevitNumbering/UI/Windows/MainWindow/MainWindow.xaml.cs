using System.Reflection;
using System.Windows;

namespace RevitNumbering.UI.Windows.MainWindow
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            Title = assemblyName.Name + " " + assemblyName.Version;
        }
    }
}
