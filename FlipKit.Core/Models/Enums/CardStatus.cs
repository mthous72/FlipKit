namespace FlipKit.Core.Models.Enums
{
    public enum CardStatus
    {
        Draft,
        Priced,
        Ready,
        Listed,
        Sold,
        ReservedForSet,  // card is locked into a SurpriseSet; excluded from individual listing flows
        SoldInSet,       // card was sold as part of a completed SurpriseSet; included in revenue reports
    }
}
