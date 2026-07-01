using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PurcHistoricTariffReckoner.CSharp.Models;
using PurcHistoricTariffReckoner.CSharp.Services;

namespace PurcHistoricTariffReckoner.CSharp.Controllers;

[Authorize]
[ApiController]
[Route("api/assistant")]
public sealed class AssistantController : ControllerBase
{
    private readonly IAiAssistantService _assistantService;

    public AssistantController(IAiAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAskResponse>> Ask(
        [FromBody] AssistantAskRequest request,
        CancellationToken cancellationToken)
    {
        if (!_assistantService.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "AI assistant not configured",
                message = "The AI assistant is not yet enabled. An administrator needs to set the GEMINI_API_KEY (free) environment variable, or AiAssistant:GeminiApiKey in configuration."
            });
        }

        var question = (request.Question ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            return BadRequest(new { error = "A question is required." });
        }

        var history = (request.History ?? new List<AssistantChatMessage>())
            .Where(message => message is not null
                && !string.IsNullOrWhiteSpace(message.Content)
                && (string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)))
            .TakeLast(10)
            .ToList();

        try
        {
            var answer = await _assistantService.AskAsync(question, history, request.PageContextJson, cancellationToken);
            return Ok(new AssistantAskResponse
            {
                Answer = string.IsNullOrWhiteSpace(answer)
                    ? "I could not generate an answer. Please rephrase your question."
                    : answer
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The AI service could not be reached. Please try again." });
        }
    }
}
