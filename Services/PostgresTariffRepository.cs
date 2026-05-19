using Microsoft.Extensions.Configuration;
using Npgsql;
using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public sealed class PostgresTariffRepository : ITariffRepository
{
    private readonly string _connectionString;

    public PostgresTariffRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SupabasePostgres")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "The SupabasePostgres connection string is missing. Set it in appsettings or an environment variable.");
        }
    }

    public async Task<IReadOnlyList<TariffPeriodCatalogItem>> GetPeriodCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT year_id, period
            FROM tariff_components
            ORDER BY year_id, period;
            """;

        var results = new List<TariffPeriodCatalogItem>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var period = reader.GetString(1);
            results.Add(new TariffPeriodCatalogItem
            {
                YearId = ReadInt32(reader, 0),
                Period = period,
                CalendarYear = TariffCategoryRules.ExtractCalendarYear(period),
            });
        }

        return results;
    }

    public async Task<int> ResolveYearIdByPeriodAsync(
        string period,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT year_id
            FROM tariff_components
            WHERE period = @period
            ORDER BY year_id
            LIMIT 1;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("period", period);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null || value is DBNull)
        {
            throw new InvalidOperationException($"Could not resolve the year id for period '{period}'.");
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            short shortValue => shortValue,
            decimal decimalValue => decimal.ToInt32(decimalValue),
            double doubleValue => checked((int)Math.Round(doubleValue, MidpointRounding.AwayFromZero)),
            float floatValue => checked((int)Math.Round(floatValue, MidpointRounding.AwayFromZero)),
            _ => Convert.ToInt32(value),
        };
    }

    public async Task<IReadOnlyList<TariffCategoryComponent>> GetCategoryComponentTypesAsync(
        int yearId,
        string period,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT category_id, component_type
            FROM tariff_components
            WHERE year_id = @yearId
              AND period = @period
            ORDER BY category_id, component_type;
            """;

        var results = new List<TariffCategoryComponent>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("yearId", yearId);
        command.Parameters.AddWithValue("period", period);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TariffCategoryComponent
            {
                CategoryId = ReadInt32(reader, 0),
                ComponentType = reader.GetString(1),
            });
        }

        return results;
    }

    public async Task<TariffBundle> GetTariffBundleAsync(
        int yearId,
        string period,
        int categoryId,
        CancellationToken cancellationToken = default)
    {
        var components = new List<TariffComponent>();
        TaxRateRecord? tax = null;
        var levies = new List<LevyRateRecord>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string componentSql = """
            SELECT component_type, block_start, block_end, rate, unit, notes
            FROM tariff_components
            WHERE year_id = @yearId
              AND period = @period
              AND category_id = @categoryId
            ORDER BY id;
            """;

        await using (var componentCommand = new NpgsqlCommand(componentSql, connection))
        {
            componentCommand.Parameters.AddWithValue("yearId", yearId);
            componentCommand.Parameters.AddWithValue("period", period);
            componentCommand.Parameters.AddWithValue("categoryId", categoryId);

            await using var reader = await componentCommand.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                components.Add(new TariffComponent
                {
                    ComponentType = reader.GetString(0),
                    BlockStart = reader.IsDBNull(1) ? null : ReadInt32(reader, 1),
                    BlockEnd = reader.IsDBNull(2) ? null : ReadInt32(reader, 2),
                    Rate = ReadDecimal(reader, 3),
                    Unit = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Notes = reader.IsDBNull(5) ? null : reader.GetString(5),
                });
            }
        }

        const string taxSql = """
            SELECT tax_name, tax_rate
            FROM taxes
            WHERE year_id = @yearId
              AND category_id = @categoryId
            LIMIT 1;
            """;

        await using (var taxCommand = new NpgsqlCommand(taxSql, connection))
        {
            taxCommand.Parameters.AddWithValue("yearId", yearId);
            taxCommand.Parameters.AddWithValue("categoryId", categoryId);

            await using var reader = await taxCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                tax = new TaxRateRecord
                {
                    TaxName = reader.GetString(0),
                    TaxRate = ReadDecimal(reader, 1),
                };
            }
        }

        const string levySql = """
            SELECT levy_name, levy_rate
            FROM levies
            WHERE year_id = @yearId
              AND category_id = @categoryId
            ORDER BY levy_name;
            """;

        await using (var levyCommand = new NpgsqlCommand(levySql, connection))
        {
            levyCommand.Parameters.AddWithValue("yearId", yearId);
            levyCommand.Parameters.AddWithValue("categoryId", categoryId);

            await using var reader = await levyCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                levies.Add(new LevyRateRecord
                {
                    LevyName = reader.GetString(0),
                    LevyRate = ReadDecimal(reader, 1),
                });
            }
        }

        return new TariffBundle
        {
            Components = components,
            Tax = tax,
            Levies = levies,
        };
    }

    public async Task<TariffAdminUpdateResult> UpsertTariffEntryAsync(
        TariffAdminUpdateInput input,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = input.Period.Trim();
        var normalizedCategory = input.Category.Trim();
        var normalizedRecordKind = input.RecordKind.Trim();
        var categoryId = TariffCategoryRules.ResolveDatabaseCategoryId(normalizedCategory);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var yearId = await ResolveOrAllocateYearIdAsync(connection, transaction, normalizedPeriod, cancellationToken);

        bool inserted;
        string recordLabel;

        if (string.Equals(normalizedRecordKind, TariffAdminRecordKinds.TariffComponent, StringComparison.Ordinal))
        {
            var normalizedComponentType = input.ComponentType.Trim().ToLowerInvariant();
            inserted = await UpsertTariffComponentAsync(
                connection,
                transaction,
                yearId,
                normalizedPeriod,
                categoryId,
                normalizedComponentType,
                input.BlockStart,
                input.BlockEnd,
                input.Rate,
                input.Unit,
                input.Notes,
                cancellationToken);

            recordLabel = normalizedComponentType;
        }
        else if (string.Equals(normalizedRecordKind, TariffAdminRecordKinds.Tax, StringComparison.Ordinal))
        {
            var normalizedTaxName = input.ChargeName.Trim();
            inserted = await UpsertTaxAsync(
                connection,
                transaction,
                yearId,
                categoryId,
                normalizedTaxName,
                input.Rate,
                cancellationToken);

            recordLabel = normalizedTaxName;
        }
        else if (string.Equals(normalizedRecordKind, TariffAdminRecordKinds.Levy, StringComparison.Ordinal))
        {
            var normalizedLevyName = input.ChargeName.Trim();
            inserted = await UpsertLevyAsync(
                connection,
                transaction,
                yearId,
                categoryId,
                normalizedLevyName,
                input.Rate,
                cancellationToken);

            recordLabel = normalizedLevyName;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported tariff update type '{normalizedRecordKind}'.");
        }

        await transaction.CommitAsync(cancellationToken);

        return new TariffAdminUpdateResult
        {
            YearId = yearId,
            CalendarYear = input.CalendarYear,
            Period = normalizedPeriod,
            Category = normalizedCategory,
            RecordKind = normalizedRecordKind,
            RecordLabel = recordLabel,
            Inserted = inserted,
        };
    }

    private static async Task<int> ResolveOrAllocateYearIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string period,
        CancellationToken cancellationToken)
    {
        const string existingYearSql = """
            SELECT year_id
            FROM tariff_components
            WHERE period = @period
            ORDER BY year_id DESC
            LIMIT 1;
            """;

        await using (var existingYearCommand = new NpgsqlCommand(existingYearSql, connection, transaction))
        {
            existingYearCommand.Parameters.AddWithValue("period", period);
            var existingYear = await existingYearCommand.ExecuteScalarAsync(cancellationToken);

            if (existingYear is not null && existingYear is not DBNull)
            {
                return Convert.ToInt32(existingYear);
            }
        }

        const string nextYearSql = """
            SELECT COALESCE(MAX(year_id), 0) + 1
            FROM (
                SELECT year_id FROM tariff_components
                UNION ALL
                SELECT year_id FROM taxes
                UNION ALL
                SELECT year_id FROM levies
            ) AS known_years;
            """;

        await using var nextYearCommand = new NpgsqlCommand(nextYearSql, connection, transaction);
        var nextYear = await nextYearCommand.ExecuteScalarAsync(cancellationToken);
        return nextYear is null || nextYear is DBNull
            ? 1
            : Convert.ToInt32(nextYear);
    }

    private static async Task<bool> UpsertTariffComponentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int yearId,
        string period,
        int categoryId,
        string componentType,
        int? blockStart,
        int? blockEnd,
        decimal rate,
        string? unit,
        string? notes,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
            UPDATE tariff_components
            SET rate = @rate,
                unit = @unit,
                notes = @notes
            WHERE year_id = @yearId
              AND period = @period
              AND category_id = @categoryId
              AND LOWER(component_type) = LOWER(@componentType)
              AND block_start IS NOT DISTINCT FROM @blockStart
              AND block_end IS NOT DISTINCT FROM @blockEnd;
            """;

        await using (var updateCommand = new NpgsqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("yearId", yearId);
            updateCommand.Parameters.AddWithValue("period", period);
            updateCommand.Parameters.AddWithValue("categoryId", categoryId);
            updateCommand.Parameters.AddWithValue("componentType", componentType);
            updateCommand.Parameters.AddWithValue("blockStart", (object?)blockStart ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("blockEnd", (object?)blockEnd ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("rate", rate);
            updateCommand.Parameters.AddWithValue("unit", (object?)NormalizeOptionalText(unit) ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("notes", (object?)NormalizeOptionalText(notes) ?? DBNull.Value);

            var updatedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (updatedRows > 0)
            {
                return false;
            }
        }

        const string insertSql = """
            INSERT INTO tariff_components (
                year_id,
                period,
                category_id,
                component_type,
                block_start,
                block_end,
                rate,
                unit,
                notes
            )
            VALUES (
                @yearId,
                @period,
                @categoryId,
                @componentType,
                @blockStart,
                @blockEnd,
                @rate,
                @unit,
                @notes
            );
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("yearId", yearId);
        insertCommand.Parameters.AddWithValue("period", period);
        insertCommand.Parameters.AddWithValue("categoryId", categoryId);
        insertCommand.Parameters.AddWithValue("componentType", componentType);
        insertCommand.Parameters.AddWithValue("blockStart", (object?)blockStart ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("blockEnd", (object?)blockEnd ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("rate", rate);
        insertCommand.Parameters.AddWithValue("unit", (object?)NormalizeOptionalText(unit) ?? DBNull.Value);
        insertCommand.Parameters.AddWithValue("notes", (object?)NormalizeOptionalText(notes) ?? DBNull.Value);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> UpsertTaxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int yearId,
        int categoryId,
        string taxName,
        decimal taxRate,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
            UPDATE taxes
            SET tax_rate = @taxRate
            WHERE year_id = @yearId
              AND category_id = @categoryId
              AND LOWER(tax_name) = LOWER(@taxName);
            """;

        await using (var updateCommand = new NpgsqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("yearId", yearId);
            updateCommand.Parameters.AddWithValue("categoryId", categoryId);
            updateCommand.Parameters.AddWithValue("taxName", taxName);
            updateCommand.Parameters.AddWithValue("taxRate", taxRate);

            var updatedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (updatedRows > 0)
            {
                return false;
            }
        }

        const string insertSql = """
            INSERT INTO taxes (year_id, category_id, tax_name, tax_rate)
            VALUES (@yearId, @categoryId, @taxName, @taxRate);
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("yearId", yearId);
        insertCommand.Parameters.AddWithValue("categoryId", categoryId);
        insertCommand.Parameters.AddWithValue("taxName", taxName);
        insertCommand.Parameters.AddWithValue("taxRate", taxRate);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> UpsertLevyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int yearId,
        int categoryId,
        string levyName,
        decimal levyRate,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
            UPDATE levies
            SET levy_rate = @levyRate
            WHERE year_id = @yearId
              AND category_id = @categoryId
              AND LOWER(levy_name) = LOWER(@levyName);
            """;

        await using (var updateCommand = new NpgsqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.AddWithValue("yearId", yearId);
            updateCommand.Parameters.AddWithValue("categoryId", categoryId);
            updateCommand.Parameters.AddWithValue("levyName", levyName);
            updateCommand.Parameters.AddWithValue("levyRate", levyRate);

            var updatedRows = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (updatedRows > 0)
            {
                return false;
            }
        }

        const string insertSql = """
            INSERT INTO levies (year_id, category_id, levy_name, levy_rate)
            VALUES (@yearId, @categoryId, @levyName, @levyRate);
            """;

        await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
        insertCommand.Parameters.AddWithValue("yearId", yearId);
        insertCommand.Parameters.AddWithValue("categoryId", categoryId);
        insertCommand.Parameters.AddWithValue("levyName", levyName);
        insertCommand.Parameters.AddWithValue("levyRate", levyRate);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int ReadInt32(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            short shortValue => shortValue,
            decimal decimalValue => decimal.ToInt32(decimalValue),
            double doubleValue => checked((int)Math.Round(doubleValue, MidpointRounding.AwayFromZero)),
            float floatValue => checked((int)Math.Round(floatValue, MidpointRounding.AwayFromZero)),
            _ => Convert.ToInt32(value),
        };
    }

    private static decimal ReadDecimal(NpgsqlDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue),
            float floatValue => Convert.ToDecimal(floatValue),
            int intValue => intValue,
            long longValue => longValue,
            short shortValue => shortValue,
            _ => Convert.ToDecimal(value),
        };
    }
}
