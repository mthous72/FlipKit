namespace FlipKit.Core.Models.Enums
{
    public enum CostSource
    {
        LCS,
        Online,
        CardShow,
        Break,
        Trade,
        Gift,
        PersonalCollection,
        Unknown,
        LotSplit,  // cost was auto-computed as LotCostBasis / N for a SurpriseSet; re-balanced on add/remove
    }
}
