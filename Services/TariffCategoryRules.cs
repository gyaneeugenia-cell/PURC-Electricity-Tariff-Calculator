using System.Globalization;
using System.Text.RegularExpressions;
using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public static class TariffCategoryRules
{
    public static int ResolveDatabaseCategoryId(string category)
    {
        if (string.Equals(category, "Residential", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(category, "Non-Residential", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (category.StartsWith("SLT", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (category.StartsWith("EV", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        throw new ArgumentException($"Unsupported category '{category}'.", nameof(category));
    }

    public static string GetRateLevel(string category)
    {
        if (category.StartsWith("EV", StringComparison.OrdinalIgnoreCase))
        {
            return category
                .Replace("-", "_", StringComparison.Ordinal)
                .ToLowerInvariant();
        }

        if (!category.StartsWith("SLT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Rate level can only be resolved for SLT or EV categories.",
                nameof(category));
        }

        var dashIndex = category.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex < 0 || dashIndex == category.Length - 1)
        {
            return "lv";
        }

        return category[(dashIndex + 1)..]
            .Replace("-", "_", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    public static int ExtractCalendarYear(string period)
    {
        var match = Regex.Match(period, @"(19|20)\d{2}");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not extract a calendar year from period '{period}'.");
        }

        return int.Parse(match.Value, CultureInfo.InvariantCulture);
    }

    public static bool RequiresSltDemand(string category, int calendarYear, string period)
    {
        if (!category.StartsWith("SLT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !(
            (calendarYear == 2019 && period.Contains("Q3 2019", StringComparison.OrdinalIgnoreCase)) ||
            calendarYear >= 2020
        );
    }

    public static IReadOnlyList<TariffCategoryOption> BuildUiCategoryOptions(int calendarYear, string period)
    {
        var labels = new List<string>();

        if (calendarYear == 2026)
        {
            if (string.Equals(period, "Q1 2026 (January)", StringComparison.OrdinalIgnoreCase))
            {
                labels.AddRange(
                [
                    "Residential",
                    "Non-Residential",
                    "SLT-LV",
                    "SLT-MV-HV",
                    "SLT-MV2",
                ]);
            }
            else if (string.Equals(period, "Q2 2026 (April)", StringComparison.OrdinalIgnoreCase))
            {
                labels.AddRange(
                [
                    "Residential",
                    "Non-Residential",
                    "SLT-LV",
                    "SLT-MV",
                    "SLT-MV2",
                    "SLT-HV",
                    "EV-CHARGING",
                ]);
            }
        }
        else if (calendarYear is 2024 or 2025)
        {
            labels.AddRange(
            [
                "Residential",
                "Non-Residential",
                "SLT-LV",
                "SLT-MV-HV",
                "SLT-MV2",
                "SLT-HV-MINES",
            ]);
        }
        else if (calendarYear is 2022 or 2023)
        {
            labels.AddRange(
            [
                "Residential",
                "Non-Residential",
                "SLT-LV",
                "SLT-MV",
                "SLT-HV",
                "SLT-HV-STEEL-COMPANIES",
                "SLT-HV-MINES",
            ]);
        }
        else if (calendarYear >= 2011 && calendarYear <= 2021)
        {
            labels.AddRange(
            [
                "Residential",
                "Non-Residential",
                "SLT-LV",
                "SLT-MV",
                "SLT-HV",
                "SLT-HV-MINES",
            ]);
        }
        else
        {
            labels.AddRange(
            [
                "Residential",
                "Non-Residential",
                "SLT-LV",
                "SLT-MV",
                "SLT-HV",
            ]);
        }

        return labels
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(label => new TariffCategoryOption
            {
                Code = label,
                Label = label,
            })
            .ToArray();
    }

    public static IReadOnlyList<TariffCategoryOption> BuildCategoryOptions(
        IEnumerable<TariffCategoryComponent> categoryComponents)
    {
        var options = new List<TariffCategoryOption>();
        var materialized = categoryComponents.ToList();

        if (materialized.Any(item => item.CategoryId == 1))
        {
            options.Add(new TariffCategoryOption { Code = "Residential", Label = "Residential" });
        }

        if (materialized.Any(item => item.CategoryId == 2))
        {
            options.Add(new TariffCategoryOption { Code = "Non-Residential", Label = "Non-Residential" });
        }

        var sltLabels = materialized
            .Where(item => item.CategoryId == 3)
            .Select(item => ExtractSltSuffix(item.ComponentType))
            .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(suffix => suffix, StringComparer.OrdinalIgnoreCase)
            .Select(suffix => $"SLT-{suffix!.ToUpperInvariant().Replace('_', '-')}");

        foreach (var sltLabel in sltLabels)
        {
            options.Add(new TariffCategoryOption { Code = sltLabel, Label = sltLabel });
        }

        if (materialized.Any(item => item.CategoryId == 4))
        {
            options.Add(new TariffCategoryOption { Code = "EV-CHARGING", Label = "EV-CHARGING" });
        }

        return options;
    }

    private static string? ExtractSltSuffix(string componentType)
    {
        const string servicePrefix = "service_charge_";
        const string demandPrefix = "demand_charge_";
        const string energyPrefix = "energy_";

        if (componentType.StartsWith(servicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return componentType[servicePrefix.Length..];
        }

        if (componentType.StartsWith(demandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return componentType[demandPrefix.Length..];
        }

        if (componentType.StartsWith(energyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return componentType[energyPrefix.Length..];
        }

        return null;
    }
}
