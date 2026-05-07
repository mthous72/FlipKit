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
            if (DataContext is NewSurpriseSetViewModel vm && vm.IsValid)
            {
                vm.Confirmed = true;
                Close();
            }
        }
    }
}
