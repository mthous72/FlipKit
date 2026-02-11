namespace FlipKit.Web.Models
{
    public class SettingsViewModel
    {
        public bool HasOpenRouterKey { get; set; }
        public bool HasImgBBKey { get; set; }
        public string? DefaultModel { get; set; }
        public decimal WhatnotFeePercent { get; set; }
        public decimal EbayFeePercent { get; set; }
        public decimal DefaultShippingCostPwe { get; set; }
        public decimal DefaultShippingCostBmwt { get; set; }
        public int PriceStalenessThresholdDays { get; set; }
    }
}
