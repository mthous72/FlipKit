using CommunityToolkit.Mvvm.ComponentModel;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Drives NewSurpriseSetDialog. Extends ObservableObject (not ViewModelBase) so the
    /// ViewLocator smoke test doesn't treat it as a navigable page.
    /// </summary>
    public partial class NewSurpriseSetViewModel : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _showName = string.Empty;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private decimal _spotPrice;
        [ObservableProperty] private string _sharedCondition = "Near Mint";
        [ObservableProperty] private string _sharedShippingProfile = string.Empty;
        [ObservableProperty] private string _sharedWhatnotCategory = "Sports Trading Cards";
        [ObservableProperty] private string _notes = string.Empty;

        // Set by the OK / Cancel buttons in the dialog. Kept observable so the
        // dialog can react to value changes (the dialog code-behind is a thin
        // wrapper that just calls Close() — historically OnCreateClick checked
        // IsValid inline, which became dead code once IsValid was bound to
        // IsEnabled on the Create button).
        [ObservableProperty] private bool _confirmed;

        // Validation message shown next to the Name field. Cleared as soon as
        // the user starts typing, set on Submit-with-empty-name. Kept separate
        // from IsValid so the field doesn't render the error pre-emptively
        // (only after the user has tried to confirm).
        [ObservableProperty] private string? _validationError;

        public SurpriseSet BuildSet() => new()
        {
            Name = Name.Trim(),
            ShowName = string.IsNullOrWhiteSpace(ShowName) ? null : ShowName.Trim(),
            Title = string.IsNullOrWhiteSpace(Title) ? Name.Trim() : Title.Trim(),
            SpotPrice = SpotPrice,
            SharedCondition = SharedCondition,
            SharedShippingProfile = SharedShippingProfile,
            SharedWhatnotCategory = string.IsNullOrWhiteSpace(SharedWhatnotCategory)
                ? "Sports Trading Cards" : SharedWhatnotCategory.Trim(),
            SharedListingType = "Buy it Now",
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            State = SurpriseSetState.Draft,
        };

        public bool IsValid => !string.IsNullOrWhiteSpace(Name);

        // Computed from Name — when Name changes, IsValid would otherwise stay
        // stale because the source generator only raises PropertyChanged for
        // the backing field, not for derived properties. Cascade the
        // notification so {Binding IsValid} bindings (Create button's
        // IsEnabled, the CreateCommand's CanExecute) react to keystrokes.
        partial void OnNameChanged(string value)
        {
            OnPropertyChanged(nameof(IsValid));
            ValidationError = null;
        }
    }
}
