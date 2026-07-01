namespace PurcHistoricTariffReckoner.CSharp.Models;

public sealed class AssistantChatMessage
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}

public sealed class AssistantAskRequest
{
    public string Question { get; init; } = string.Empty;

    public List<AssistantChatMessage> History { get; init; } = new();

    public string? PageContextJson { get; init; }
}

public sealed class AssistantAskResponse
{
    public string Answer { get; init; } = string.Empty;
}
