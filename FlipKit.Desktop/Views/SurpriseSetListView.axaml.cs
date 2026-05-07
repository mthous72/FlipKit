using Avalonia.Controls;
using Avalonia.Input;
using FlipKit.Core.Models;
using FlipKit.Desktop.ViewModels;

namespace FlipKit.Desktop.Views
{
    public partial class SurpriseSetListView : UserControl
    {
        public SurpriseSetListView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) =>
            {
                if (DataContext is SurpriseSetListViewModel vm)
                    _ = vm.LoadCommand.ExecuteAsync(null);
            };
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is SurpriseSetListViewModel vm && vm.SelectedSet != null)
                _ = vm.OpenSetCommand.ExecuteAsync(vm.SelectedSet);
        }
    }
}
