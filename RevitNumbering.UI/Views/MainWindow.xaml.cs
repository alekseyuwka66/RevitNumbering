using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using RevitNumbering.Core.Settings;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    /// <summary>The plugin window. Modeless, owned by the main window of Revit.</summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private bool _closeWithoutQuestion;
        private WindowPlacement _placement;

        /// <param name="ownerHandle">The main window of Revit.</param>
        public MainWindow(MainViewModel viewModel, IntPtr ownerHandle)
        {
            InitializeComponent();
            _viewModel = viewModel;
            viewModel.Dialogs = new Dialogs(this);
            DataContext = viewModel;
            new WindowInteropHelper(this).Owner = ownerHandle;

            RestorePlacement(viewModel.SavedPlacement);
            Loaded += (sender, e) => viewModel.OpenCommand.Execute(null);
            Closing += OnClosing;
            Closed += (sender, e) => _viewModel.Close(_placement);
        }

        /// <summary>Revit is closing: no question about unsaved settings.</summary>
        public void CloseWithoutQuestion()
        {
            _closeWithoutQuestion = true;
            Close();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_closeWithoutQuestion && !_viewModel.ConfirmClose())
            {
                e.Cancel = true;
                return;
            }

            Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty)
            {
                _placement = new WindowPlacement
                {
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                };
            }
        }

        /// <summary>The saved position is used only while it is still on a screen.</summary>
        private void RestorePlacement(WindowPlacement placement)
        {
            if (placement == null)
            {
                return;
            }

            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var saved = new Rect(placement.Left, placement.Top,
                Math.Max(placement.Width, MinWidth), Math.Max(placement.Height, MinHeight));
            if (!screen.Contains(saved))
            {
                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = saved.Left;
            Top = saved.Top;
            Width = saved.Width;
            Height = saved.Height;
        }
    }
}
