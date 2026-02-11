using System;
using Avalonia.Controls;
using Avalonia.Input;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Ensure cleanup on window close
            Closing += OnClosing;
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            // Dispose the MainWindowViewModel to clean up all child ViewModels
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not MainWindowViewModel mainVm)
                return;

            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (ctrl && e.Key == Key.N)
            {
                // Browse image on Scan page
                if (mainVm.CurrentPage is ScanViewModel scanVm)
                {
                    scanVm.BrowseImageCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (ctrl && e.Key == Key.S)
            {
                // Save on Scan page
                if (mainVm.CurrentPage is ScanViewModel scanVm && scanVm.SaveCardCommand.CanExecute(null))
                {
                    scanVm.SaveCardCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}