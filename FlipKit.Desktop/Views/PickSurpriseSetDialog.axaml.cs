using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class PickSurpriseSetDialog : Window
    {
        public PickSurpriseSetDialog()
        {
            InitializeComponent();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PickSurpriseSetViewModel vm) return;
            if (!vm.IsValid) return;
            vm.Confirmed = true;
            Close();
        }

        // Inline create-new-set path: opens NewSurpriseSetDialog modally, and if
        // the user confirms it, hands the resulting VM to PickSurpriseSetVM.AddNewSetAsync
        // which inserts the set + refreshes the picker + auto-selects the new set.
        // Mirrors the BulkScan inline flow so users get the same affordance from
        // either entry point.
        private async void OnCreateNewSetClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not PickSurpriseSetViewModel vm) return;

            var newVm = new NewSurpriseSetViewModel();
            var dialog = new NewSurpriseSetDialog { DataContext = newVm };
            await dialog.ShowDialog(this);

            if (!newVm.Confirmed || !newVm.IsValid) return;
            await vm.AddNewSetAsync(newVm);
        }
    }
}
