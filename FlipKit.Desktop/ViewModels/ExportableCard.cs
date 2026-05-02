using FlipKit.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlipKit.Desktop.ViewModels
{
    /// <summary>
    /// Wraps a <see cref="Card"/> with an <see cref="IsSelected"/> flag for the
    /// Export page checkbox grid. The wrapper is what the View binds to; the
    /// underlying Card is read directly for display columns and passed to the
    /// dispatcher when the user clicks Export.
    /// </summary>
    public partial class ExportableCard : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public Card Card { get; }

        public ExportableCard(Card card, bool isSelected = false)
        {
            Card = card;
            _isSelected = isSelected;
        }

        // Convenience accessors for DataGrid columns — keeps XAML simple.
        public string PlayerName => Card.PlayerName ?? string.Empty;
        public int? Year => Card.Year;
        public string? Sport => Card.Sport?.ToString();
        public string Set => Card.SetName ?? Card.Brand ?? string.Empty;
        public string Status => Card.Status.ToString();
        public decimal? ListingPrice => Card.ListingPrice;
        public System.DateTime CreatedAt => Card.CreatedAt;
    }
}
