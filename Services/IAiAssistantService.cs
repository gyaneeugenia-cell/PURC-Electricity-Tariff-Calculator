using PurcHistoricTariffReckoner.CSharp.Models;

namespace PurcHistoricTariffReckoner.CSharp.Services;

public interface IAiAssistantService
{
    bool IsConfigured { get; }

    Task<string> AskAsync(
        string question,
        IReadOnlyList<AssistantChatMessage> history,
        string? pageContextJson,
        CancellationToken cancellationToken);
}
