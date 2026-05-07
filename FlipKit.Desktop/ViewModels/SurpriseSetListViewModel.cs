using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlipKit.Core.Models;
using FlipKit.Core.Services;

namespace FlipKit.Desktop.ViewModels
{
    public partial class SurpriseSetListViewModel : ViewModelBase
    {
        private readonly ISurpriseSetRepository _repository;
        private readonly INavigationService _navigation;

        [ObservableProperty] private ObservableCollection<SurpriseSet> _sets = new();
        [ObservableProperty] private SurpriseSet? _selectedSet;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string? _statusMessage;

        public SurpriseSetListViewModel(
            ISurpriseSetRepository repository,
            INavigationService navigation)
        {
            _repository = repository;
            _navigation = navigation;
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = null;
            try
            {
                var list = await _repository.GetAllAsync();
                Sets = new ObservableCollection<SurpriseSet>(list);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load sets: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenSetAsync(SurpriseSet? set)
        {
            if (set == null) return;
            await _navigation.NavigateToSurpriseSetDetailAsync(set.Id);
        }

        [RelayCommand]
        private async Task NewSetAsync()
        {
            var vm = new NewSurpriseSetViewModel();
            var dialog = new Views.NewSurpriseSetDialog { DataContext = vm };
            await dialog.ShowDialog(GetOwnerWindow());

            if (!vm.Confirmed || !vm.IsValid) return;

            IsLoading = true;
            StatusMessage = null;
            try
            {
                var newSet = vm.BuildSet();
                await _repository.InsertAsync(newSet);
                await LoadAsync();
                StatusMessage = $"Created \"{newSet.Name}\".";
                await _navigation.NavigateToSurpriseSetDetailAsync(newSet.Id);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not create set: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static Avalonia.Controls.Window GetOwnerWindow() =>
            (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            ? desktop.MainWindow!
            : throw new InvalidOperationException("No main window.");
    }
}
