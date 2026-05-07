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

        public bool Confirmed { get; set; }

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
    }
}
