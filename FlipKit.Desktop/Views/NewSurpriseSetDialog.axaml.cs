using Avalonia.Controls;
using Avalonia.Interactivity;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class NewSurpriseSetDialog : Window
    {
        public NewSurpriseSetDialog()
        {
            InitializeComponent();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();

        private void OnCreateClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not NewSurpriseSetViewModel vm) return;

            if (!vm.IsValid)
            {
                // Belt-and-braces — the button's IsEnabled is bound to IsValid
                // so this branch shouldn't fire in practice. Surface the
                // reason if it ever does (e.g. via keyboard activation) so
                // the user gets feedback instead of a silent no-op.
                vm.ValidationError = "Name is required.";
                return;
            }

            vm.Confirmed = true;
            Close();
        }
    }
}
