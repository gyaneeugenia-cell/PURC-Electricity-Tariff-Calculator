using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public static class TariffPeriodCatalog
{
    private static readonly TariffPeriodCatalogItem[] Items =
    [
        new() { CalendarYear = 1998, YearId = 1, Period = "February 1998" },
        new() { CalendarYear = 1998, YearId = 1, Period = "September 1998" },
        new() { CalendarYear = 2001, YearId = 3, Period = "May 2001" },
        new() { CalendarYear = 2002, YearId = 4, Period = "August 2002" },
        new() { CalendarYear = 2003, YearId = 5, Period = "March 2003" },
        new() { CalendarYear = 2005, YearId = 8, Period = "May 2005" },
        new() { CalendarYear = 2006, YearId = 9, Period = "May 2006" },
        new() { CalendarYear = 2007, YearId = 10, Period = "November 2007" },
        new() { CalendarYear = 2008, YearId = 11, Period = "July 2008" },
        new() { CalendarYear = 2010, YearId = 12, Period = "June 2010" },
        new() { CalendarYear = 2011, YearId = 13, Period = "March 2011" },
        new() { CalendarYear = 2011, YearId = 14, Period = "June 2011" },
        new() { CalendarYear = 2011, YearId = 15, Period = "September 2011" },
        new() { CalendarYear = 2011, YearId = 16, Period = "December 2011" },
        new() { CalendarYear = 2012, YearId = 17, Period = "March 2012" },
        new() { CalendarYear = 2012, YearId = 18, Period = "June 2012" },
        new() { CalendarYear = 2012, YearId = 19, Period = "September 2012" },
        new() { CalendarYear = 2013, YearId = 20, Period = "October 2013" },
        new() { CalendarYear = 2014, YearId = 21, Period = "Q1 2014 (January)" },
        new() { CalendarYear = 2014, YearId = 22, Period = "Q2 2014 (April)" },
        new() { CalendarYear = 2014, YearId = 23, Period = "Q3 2014 (July)" },
        new() { CalendarYear = 2014, YearId = 24, Period = "Q4 2014 (October)" },
        new() { CalendarYear = 2015, YearId = 25, Period = "April 2015" },
        new() { CalendarYear = 2015, YearId = 26, Period = "July 2015" },
        new() { CalendarYear = 2015, YearId = 27, Period = "December 2015" },
        new() { CalendarYear = 2016, YearId = 28, Period = "Q2 2016 (April)" },
        new() { CalendarYear = 2016, YearId = 29, Period = "Q3 2016 (July)" },
        new() { CalendarYear = 2017, YearId = 30, Period = "Q1 2017 (January)" },
        new() { CalendarYear = 2017, YearId = 31, Period = "Q2 2017 (April)" },
        new() { CalendarYear = 2017, YearId = 32, Period = "Q3 2017 (July)" },
        new() { CalendarYear = 2018, YearId = 33, Period = "Q1 2018 (March)" },
        new() { CalendarYear = 2018, YearId = 34, Period = "Q3 2018 (July)" },
        new() { CalendarYear = 2018, YearId = 35, Period = "Q4 2018 (October)" },
        new() { CalendarYear = 2019, YearId = 38, Period = "Q3 2019 (July)" },
        new() { CalendarYear = 2019, YearId = 39, Period = "Q4 2019 (October)" },
        new() { CalendarYear = 2020, YearId = 40, Period = "Q1 2020 (January)" },
        new() { CalendarYear = 2020, YearId = 41, Period = "Q2 2020 (April)" },
        new() { CalendarYear = 2020, YearId = 42, Period = "Q3 2020 (July)" },
        new() { CalendarYear = 2020, YearId = 43, Period = "Q4 2020 (October)" },
        new() { CalendarYear = 2021, YearId = 44, Period = "Q1 2021 (January)" },
        new() { CalendarYear = 2022, YearId = 45, Period = "September 2022" },
        new() { CalendarYear = 2023, YearId = 46, Period = "Q1 2023 (February)" },
        new() { CalendarYear = 2023, YearId = 47, Period = "Q2 2023 (June)" },
        new() { CalendarYear = 2023, YearId = 48, Period = "Q3 2023 (September)" },
        new() { CalendarYear = 2023, YearId = 49, Period = "Q4 2023 (December)" },
        new() { CalendarYear = 2024, YearId = 50, Period = "Q2 2024 (April)" },
        new() { CalendarYear = 2024, YearId = 51, Period = "Q3 2024 (July)" },
        new() { CalendarYear = 2024, YearId = 52, Period = "Q4 2024 (October)" },
        new() { CalendarYear = 2025, YearId = 53, Period = "Q2 2025 (May)" },
        new() { CalendarYear = 2025, YearId = 54, Period = "Q3 2025 (July)" },
        new() { CalendarYear = 2025, YearId = 55, Period = "Q4 2025 (October)" },
        new() { CalendarYear = 2026, YearId = 56, Period = "Q1 2026 (January)" },
        new() { CalendarYear = 2026, YearId = 57, Period = "Q2 2026 (April)" },
    ];

    private static readonly int[] Years = Items
        .Select(item => item.CalendarYear)
        .Distinct()
        .OrderBy(year => year)
        .ToArray();

    public static IReadOnlyList<TariffPeriodCatalogItem> GetAll()
    {
        return Items;
    }

    public static IReadOnlyList<int> GetYears()
    {
        return Years;
    }

    public static IReadOnlyList<TariffPeriodCatalogItem> GetPeriodsForYear(int calendarYear)
    {
        return Items
            .Where(item => item.CalendarYear == calendarYear)
            .ToArray();
    }

    public static TariffPeriodCatalogItem? FindByPeriod(string period)
    {
        return Items.FirstOrDefault(item =>
            string.Equals(item.Period, period, StringComparison.OrdinalIgnoreCase));
    }
}
