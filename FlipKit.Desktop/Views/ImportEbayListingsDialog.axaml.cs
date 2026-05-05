using Avalonia.Controls;
using Avalonia.Interactivity;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class ImportEbayListingsDialog : Window
    {
        public ImportEbayListingsDialog()
        {
            InitializeComponent();
        }

        public ImportEbayListingsDialog(ImportEbayListingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void OnImportClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ImportEbayListingsViewModel vm)
            {
                await vm.CommitCommand.ExecuteAsync(null);
                if (vm.Committed) Close(true);
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            // Return whatever Committed flag the user got — caller refreshes inventory
            // either way (a partial commit can still have Inserted/Updated > 0).
            if (DataContext is ImportEbayListingsViewModel vm)
                Close(vm.Committed);
            else
                Close(false);
        }
    }
}
