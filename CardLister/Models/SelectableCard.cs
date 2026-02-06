using CommunityToolkit.Mvvm.ComponentModel;

namespace CardLister.Models
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
