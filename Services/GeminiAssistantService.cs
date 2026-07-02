using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

/// <summary>
/// Talks to Google Gemini (free tier) to power the in-app AI assistant.
/// Configure via appsettings ("AiAssistant:GeminiApiKey") or the GEMINI_API_KEY
/// environment variable, which is the friendlier option on most hosts.
/// </summary>
public sealed class GeminiAssistantService : IAiAssistantService
{
    private const string DefaultModel = "gemini-2.5-flash";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAssistantService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        _apiKey = FirstNonBlank(
            configuration["AiAssistant:GeminiApiKey"],
            Environment.GetEnvironmentVariable("GEMINI_API_KEY")) ?? string.Empty;

        _model = FirstNonBlank(
            configuration["AiAssistant:GeminiModel"],
            Environment.GetEnvironmentVariable("GEMINI_MODEL")) ?? DefaultModel;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    private static string? FirstNonBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    public async Task<string> AskAsync(
        string question,
        IReadOnlyList<AssistantChatMessage> history,
        string? pageContextJson,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The Gemini API key is not configured.");
        }

        var systemPrompt = BuildSystemPrompt(pageContextJson);

        var contents = new JsonArray();
        foreach (var message in history)
        {
            contents.Add(new JsonObject
            {
                ["role"] = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = message.Content })
            });
        }

        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = new JsonArray(new JsonObject { ["text"] = question })
        });

        var requestBody = new JsonObject
        {
            ["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = systemPrompt })
            },
            ["contents"] = contents,
            ["generationConfig"] = new JsonObject
            {
                ["maxOutputTokens"] = 1024,
                ["temperature"] = 0.4
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        using var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Gemini API request failed.");
        }

        var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
        var parts = responseJson?["candidates"]?.AsArray()?.FirstOrDefault()?["content"]?["parts"]?.AsArray();

        if (parts is null)
        {
            return string.Empty;
        }

        var texts = parts
            .Select(part => part?["text"]?.GetValue<string>() ?? string.Empty)
            .Where(text => text.Length > 0);

        return string.Join("\n", texts).Trim();
    }

    private static string BuildSystemPrompt(string? pageContextJson)
    {
        var prompt = $"""
            You are the built-in AI assistant for the PURC Electricity Tariff Analytics & Reckoner Portal — an internal tool used by Ghana's Public Utilities Regulatory Commission (PURC) to reckon electricity bills, reverse-calculate consumption, inspect historic tariff rates, and compare bills across years.

            YOUR TWO JOBS:
            1. Answer any question a staff member has about how the program works (its pages, fields and terminology).
            2. Help interpret or analyse the calculation currently shown on screen, using the "CURRENT SCREEN STATE" data below when present.

            HOW THE PROGRAM WORKS (use this to answer "how do I…" questions):
            - The main page lets a user pick a Year, a Tariff Effective Period (tariffs change more than once in some years) and a Customer Category, then either:
              - "Consumption" mode: enter Monthly Consumption (kWh) to compute the Total Bill, or
              - "Total Bill" mode: enter a Total Monthly Bill (GHS) to reverse-calculate the estimated consumption (kWh).
            - Customer categories: Residential (with a lifeline/subsidised low-usage band), Non-Residential, Special Load Tariff (SLT) at several voltage levels (SLT-LV, SLT-MV, SLT-MV2, SLT-MV-HV, SLT-HV, SLT-HV-Mines, SLT-HV-Steel Companies), and EV Charging.
            - Maximum Demand (kVA) is only required for SLT and EV categories that bill a demand charge.
            - "Selected Tariff Rates" panel shows the exact tariff components (energy charge per block, service/fixed charge, demand charge), plus applicable Tax and Levies, for the chosen year/period/category.
            - "Bill Breakdown" and "Results" show the computed line items and totals, and can be printed for record keeping.
            - "Bill Trend Tool" compares the total bill for a fixed consumption level across multiple selected years/periods (Residential, Non-Residential or SLT categories only), with a chart, a data table and CSV export.
            - Signed-in users can change their password. Admins have an additional Admin dashboard to manage users and to maintain tariff, tax and levy records in the database.
            - Data is stored in PostgreSQL (Supabase).

            RULES:
            - Base factual/analytical answers only on the information given to you (this description and any CURRENT SCREEN STATE). If something isn't covered, say so plainly rather than guessing at figures.
            - When you compute or restate numbers, show them clearly; use small markdown tables where helpful.
            - Be concise, professional and accurate. Today's date is {DateTime.UtcNow:yyyy-MM-dd}.
            - If asked something outside this program or Ghana's electricity tariff domain, briefly say it's outside the scope of the tool.
            """;

        if (!string.IsNullOrWhiteSpace(pageContextJson))
        {
            prompt += $"""


                CURRENT SCREEN STATE (the calculation currently visible to the user, as JSON):
                {pageContextJson}
                """;
        }

        return prompt;
    }
}
