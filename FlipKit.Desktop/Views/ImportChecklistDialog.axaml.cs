using Avalonia.Controls;
using Avalonia.Interactivity;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class ImportChecklistDialog : Window
    {
        public ImportChecklistDialog()
        {
            InitializeComponent();
        }

        public ImportChecklistDialog(ImportChecklistViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void OnImportClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ImportChecklistViewModel vm)
            {
                await vm.CommitCommand.ExecuteAsync(null);
                if (vm.Committed) Close(true);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
