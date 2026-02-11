using System.Collections.Generic;
using System.Threading.Tasks;
using FlipKit.Core.Models;
using FlipKit.Core.Models.Enums;

namespace FlipKit.Core.Services
{
    public class MockScannerService : IScannerService
    {
        public async Task<ScanResult> ScanCardAsync(string imagePath, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free")
        {
            // Simulate API delay
            await Task.Delay(2000);

            var card = new Card
            {
                PlayerName = "Justin Jefferson",
                CardNumber = "88",
                Year = 2023,
                Sport = Sport.Football,
                Manufacturer = "Panini",
                Brand = "Prizm",
                Team = "Minnesota Vikings",
                VariationType = "Parallel",
                ParallelName = "Silver",
                IsRookie = false,
                Condition = "Near Mint",
                WhatnotSubcategory = "Football Cards",
                ImagePathFront = imagePath
            };

            var visualCues = new VisualCues
            {
                BorderColor = "silver",
                CardFinish = "prizm",
                HasFoil = true,
                HasRefractorPattern = true,
                HasSerialNumber = false
            };

            return new ScanResult
            {
                Card = card,
                VisualCues = visualCues,
                AllVisibleText = new List<string>
                {
                    "Justin Jefferson", "88", "PRIZM", "Minnesota Vikings",
                    "Wide Receiver", "2023 Panini Prizm"
                },
                Confidences = new List<FieldConfidence>
                {
                    new() { FieldName = "player_name", Confidence = VerificationConfidence.High, Reason = "AI confidence: high" },
                    new() { FieldName = "card_number", Confidence = VerificationConfidence.High, Reason = "AI confidence: high" },
                    new() { FieldName = "year", Confidence = VerificationConfidence.High, Reason = "AI confidence: high" },
                    new() { FieldName = "manufacturer", Confidence = VerificationConfidence.High, Reason = "AI confidence: high" },
                    new() { FieldName = "brand", Confidence = VerificationConfidence.High, Reason = "AI confidence: high" },
                    new() { FieldName = "variation_type", Confidence = VerificationConfidence.Medium, Reason = "AI confidence: medium" },
                    new() { FieldName = "parallel_name", Confidence = VerificationConfidence.Medium, Reason = "AI confidence: medium" }
                }
            };
        }

        public Task<string> SendCustomPromptAsync(string imagePath, string prompt, string? backImagePath = null, string model = "nvidia/nemotron-nano-12b-v2-vl:free")
        {
            return Task.FromResult(@"{""variation_confirmed"": ""Silver Prizm"", ""border_color"": ""silver"", ""surface_finish"": ""prizm/holographic""}");
        }
    }
}
