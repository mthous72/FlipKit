using CommunityToolkit.Mvvm.ComponentModel;
using FlipKit.Core.Models;

namespace FlipKit.Desktop.Models
{
    public partial class SelectableCard : ObservableObject
    {
        public Card Card { get; }

        [ObservableProperty] private bool _isSelected;

        public SelectableCard(Card card)
        {
            Card = card;
        }
    }
}
