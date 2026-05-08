using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FlipKit.Core.Models;
using FlipKit.Core.Services;
using Microsoft.Extensions.Logging;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Drives PickSurpriseSetDialog. Lists Draft sets so the user can target a
    /// move/save destination, with an inline "Create New Set…" path that reuses
    /// NewSurpriseSetDialog. Used by both the My Cards "Move to Surprise Set"
    /// flow and could be reused by Bulk Scan if we ever need a richer picker
    /// than the dropdown there. Extends ObservableObject (not ViewModelBase)
    /// so the ViewLocator smoke test doesn't treat it as a navigable page.
    /// </summary>
    public partial class PickSurpriseSetViewModel : ObservableObject
    {
        private readonly ISurpriseSetRepository _surpriseSetRepository;
        private readonly ILogger<PickSurpriseSetViewModel>? _logger;

        public ObservableCollection<SurpriseSet> AvailableSets { get; } = new();

        [ObservableProperty] private SurpriseSet? _selectedSet;
        [ObservableProperty] private bool _confirmed;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private int _cardCount;

        public PickSurpriseSetViewModel(
            ISurpriseSetRepository surpriseSetRepository,
            ILogger<PickSurpriseSetViewModel>? logger = null)
        {
            _surpriseSetRepository = surpriseSetRepository;
            _logger = logger;
        }

        public bool IsValid => SelectedSet != null;

        partial void OnSelectedSetChanged(SurpriseSet? value)
        {
            OnPropertyChanged(nameof(IsValid));
        }

        public string ConfirmButtonText =>
            CardCount > 1 ? $"Move {CardCount} cards" : "Move card";

        partial void OnCardCountChanged(int value)
        {
            OnPropertyChanged(nameof(ConfirmButtonText));
        }

        public async Task LoadAsync()
        {
            try
            {
                var drafts = await _surpriseSetRepository.GetDraftSetsAsync();
                AvailableSets.Clear();
                foreach (var s in drafts) AvailableSets.Add(s);
            }
            catch (System.Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load Draft surprise sets for the picker");
                ErrorMessage = $"Could not load sets: {ex.Message}";
            }
        }

        /// <summary>
        /// Inserts a new Draft set built from <paramref name="newSetVm"/>,
        /// then refreshes the picker and selects it. Returns true if the
        /// insert succeeded.
        /// </summary>
        public async Task<bool> AddNewSetAsync(NewSurpriseSetViewModel newSetVm)
        {
            if (!newSetVm.IsValid) return false;
            try
            {
                var set = newSetVm.BuildSet();
                await _surpriseSetRepository.InsertAsync(set);
                await LoadAsync();
                SelectedSet = AvailableSets.FirstOrDefault(s => s.Id == set.Id);
                return true;
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Inline create-set from PickSurpriseSetDialog failed");
                ErrorMessage = $"Could not create set: {ex.Message}";
                return false;
            }
        }
    }
}
