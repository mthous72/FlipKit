using System;
using System.Collections.Generic;
using CardLister.Models;
using CardLister.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CardLister.ViewModels
{
    public partial class CardDetailViewModel : ObservableObject
    {
        public static List<string> GradeOptions { get; } = BuildGradeOptions();
        public static List<string> AutoGradeOptions { get; } = BuildGradeOptions();

        private static List<string> BuildGradeOptions()
        {
            var options = new List<string> { "", "Authentic" };
            for (decimal d = 0; d <= 10; d += 0.5m)
                options.Add(d.ToString("0.#"));
            return options;
        }

        public List<string> GradingCompanyOptions { get; set; } = new() { "PSA", "BGS", "CGC", "CCG", "SGC" };

        // Card Identity
        [ObservableProperty] private string _playerName = string.Empty;
        [ObservableProperty] private string? _cardNumber;
        [ObservableProperty] private int? _year;
        [ObservableProperty] private Sport? _sport;

        // Manufacturer / Set
        [ObservableProperty] private string? _manufacturer;
        [ObservableProperty] private string? _brand;
        [ObservableProperty] private string? _setName;
        [ObservableProperty] private string? _team;

        // Variation / Parallel
        [ObservableProperty] private string _variationType = "Base";
        [ObservableProperty] private string? _parallelName;
        [ObservableProperty] private string? _serialNumbered;
        [ObservableProperty] private bool _isShortPrint;
        [ObservableProperty] private bool _isSSP;

        // Special Attributes
        [ObservableProperty] private bool _isRookie;
        [ObservableProperty] private bool _isAuto;
        [ObservableProperty] private bool _isRelic;

        // Condition / Grading
        [ObservableProperty] private string _condition = "Near Mint";
        [ObservableProperty] private bool _isGraded;
        [ObservableProperty] private string? _gradeCompany;
        [ObservableProperty] private string? _gradeValue;
        [ObservableProperty] private string? _certNumber;
        [ObservableProperty] private string? _autoGrade;

        // Cost Basis
        [ObservableProperty] private decimal? _costBasis;
        [ObservableProperty] private CostSource? _costSource;
        [ObservableProperty] private DateTime? _costDate;
        [ObservableProperty] private string? _costNotes;

        // Listing Settings
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private string _listingType = "Buy It Now";
        [ObservableProperty] private bool _offerable = true;
        [ObservableProperty] private string _shippingProfile = "4 oz";

        // Whatnot
        [ObservableProperty] private string _whatnotCategory = "Sports Cards";
        [ObservableProperty] private string? _whatnotSubcategory;

        // Notes
        [ObservableProperty] private string? _notes;

        public Card ToCard()
        {
            return new Card
            {
                PlayerName = PlayerName,
                CardNumber = CardNumber,
                Year = Year,
                Sport = Sport,
                Manufacturer = Manufacturer,
                Brand = Brand,
                SetName = SetName,
                Team = Team,
                VariationType = VariationType,
                ParallelName = ParallelName,
                SerialNumbered = SerialNumbered,
                IsShortPrint = IsShortPrint,
                IsSSP = IsSSP,
                IsRookie = IsRookie,
                IsAuto = IsAuto,
                IsRelic = IsRelic,
                Condition = Condition,
                IsGraded = IsGraded,
                GradeCompany = GradeCompany,
                GradeValue = GradeValue,
                CertNumber = CertNumber,
                AutoGrade = AutoGrade,
                CostBasis = CostBasis,
                CostSource = CostSource,
                CostDate = CostDate,
                CostNotes = CostNotes,
                Quantity = Quantity,
                ListingType = ListingType,
                Offerable = Offerable,
                ShippingProfile = ShippingProfile,
                WhatnotCategory = WhatnotCategory,
                WhatnotSubcategory = WhatnotSubcategory,
                Notes = Notes
            };
        }

        public static CardDetailViewModel FromCard(Card card)
        {
            return new CardDetailViewModel
            {
                PlayerName = card.PlayerName,
                CardNumber = card.CardNumber,
                Year = card.Year,
                Sport = card.Sport,
                Manufacturer = card.Manufacturer,
                Brand = card.Brand,
                SetName = card.SetName,
                Team = card.Team,
                VariationType = card.VariationType,
                ParallelName = card.ParallelName,
                SerialNumbered = card.SerialNumbered,
                IsShortPrint = card.IsShortPrint,
                IsSSP = card.IsSSP,
                IsRookie = card.IsRookie,
                IsAuto = card.IsAuto,
                IsRelic = card.IsRelic,
                Condition = card.Condition,
                IsGraded = card.IsGraded,
                GradeCompany = card.GradeCompany,
                GradeValue = card.GradeValue,
                CertNumber = card.CertNumber,
                AutoGrade = card.AutoGrade,
                CostBasis = card.CostBasis,
                CostSource = card.CostSource,
                CostDate = card.CostDate,
                CostNotes = card.CostNotes,
                Quantity = card.Quantity,
                ListingType = card.ListingType,
                Offerable = card.Offerable,
                ShippingProfile = card.ShippingProfile,
                WhatnotCategory = card.WhatnotCategory,
                WhatnotSubcategory = card.WhatnotSubcategory,
                Notes = card.Notes
            };
        }
    }
}
