using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;

namespace FlipKit.Core.Tests.Infrastructure;

/// <summary>
/// Builds in-memory .xlsx streams mimicking the two real Checklist Insider layouts so
/// tests can exercise <c>ExcelChecklistImporter</c> without redistributing third-party
/// files. Two static helpers:
///
/// - <see cref="BuildColumnASubsetXlsx"/> — Mosaic / Donruss / Phoenix / Absolute
///   layout: header row "CARD SET / CARD # / ATHLETE / TEAM / SEQ" plus rows where
///   column A carries the subset on every line.
/// - <see cref="BuildInlineHeaderXlsx"/> — Bowman 2026 layout: no header row; subsets
///   are announced by all-caps single-cell rows in column A; data rows have
///   A=Card #, B=Player, C=Team, D=optional flag.
///
/// Real fixture files captured the same structure but carried Checklist Insider's
/// proprietary content; per the feature's ToU posture, we never bundle those files.
/// </summary>
public static class SyntheticChecklistXlsxBuilder
{
    public record CardRow(string Subset, string CardNumber, string Player, string? Team = null, string? Flag = null);

    public static MemoryStream BuildColumnASubsetXlsx(IEnumerable<CardRow> rows)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");

        sheet.Cell(1, 1).Value = "CARD SET";
        sheet.Cell(1, 2).Value = "CARD #";
        sheet.Cell(1, 3).Value = "ATHLETE";
        sheet.Cell(1, 4).Value = "TEAM";
        sheet.Cell(1, 5).Value = "SEQ";

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Subset;
            sheet.Cell(rowIndex, 2).Value = row.CardNumber;
            sheet.Cell(rowIndex, 3).Value = row.Player;
            if (!string.IsNullOrEmpty(row.Team)) sheet.Cell(rowIndex, 4).Value = row.Team;
            rowIndex++;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public static MemoryStream BuildInlineHeaderXlsx(IEnumerable<(string? subsetHeader, CardRow? card)> entries)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");

        var rowIndex = 1;
        foreach (var (subsetHeader, card) in entries)
        {
            if (subsetHeader != null)
            {
                sheet.Cell(rowIndex, 1).Value = subsetHeader;
            }
            else if (card != null)
            {
                sheet.Cell(rowIndex, 1).Value = card.CardNumber;
                sheet.Cell(rowIndex, 2).Value = card.Player;
                if (!string.IsNullOrEmpty(card.Team)) sheet.Cell(rowIndex, 3).Value = card.Team;
                if (!string.IsNullOrEmpty(card.Flag)) sheet.Cell(rowIndex, 4).Value = card.Flag;
            }
            rowIndex++;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// A representative Mosaic-style fixture: base subset, autograph subset, color
    /// parallel subset, themed insert subset. Used by the broad importer tests.
    /// </summary>
    public static MemoryStream MosaicLikeFixture()
    {
        return BuildColumnASubsetXlsx(new[]
        {
            new CardRow("Base", "1", "Patrick Mahomes", "Kansas City Chiefs"),
            new CardRow("Base", "2", "Travis Kelce", "Kansas City Chiefs"),
            new CardRow("Base", "3", "Jalen Hurts", "Philadelphia Eagles"),
            new CardRow("Base Mosaic Black", "1", "Patrick Mahomes", "Kansas City Chiefs"),
            new CardRow("Base Mosaic Gold", "1", "Patrick Mahomes", "Kansas City Chiefs"),
            new CardRow("Autographs Mosaic", "RA-PM", "Patrick Mahomes", "Kansas City Chiefs"),
            new CardRow("Rookie Autographs Mosaic Blue", "BCP-1", "Roman Anthony", "Boston Red Sox"),
            new CardRow("Visionary", "V-1", "Tyreek Hill", "Miami Dolphins"),
            new CardRow("Visionary Black", "V-1", "Tyreek Hill", "Miami Dolphins"),
            new CardRow("Stained Glass", "SG-1", "Lamar Jackson", "Baltimore Ravens"),
        });
    }
}
