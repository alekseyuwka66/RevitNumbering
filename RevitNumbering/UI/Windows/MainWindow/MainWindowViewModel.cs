using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RevitNumbering.UI.Windows.MainWindow
{
    /// <summary>
    /// Sample view model on CommunityToolkit.Mvvm.
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusText = "Ready";

        [RelayCommand]
        private void Refresh()
        {
            StatusText = "Refreshed";
        }
    }
}
