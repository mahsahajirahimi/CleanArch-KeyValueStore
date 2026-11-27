using System.Security.Claims;
using KeyValueStore.Api.Authentication;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyValueStore.Api.Controllers;

[ApiController]
[Route("api/admin/api-keys")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = nameof(UserRole.Admin))]
public sealed class AdminApiKeyRevocationsController
    : ControllerBase
{
    private readonly RevokeApiKeyUseCase _revokeApiKey;

    public AdminApiKeyRevocationsController(
        RevokeApiKeyUseCase revokeApiKey)
    {
        _revokeApiKey = revokeApiKey;
    }

    [HttpDelete("{keyId:guid}")]
    public IActionResult Revoke(Guid keyId)
    {
        if (keyId == Guid.Empty)
        {
            return BadRequest("API key ID cannot be empty.");
        }

        string? requesterIdValue = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
            requesterIdValue,
            out Guid requesterId))
        {
            return Unauthorized();
        }

        try
        {
            _revokeApiKey.Execute(
                requesterId,
                keyId,
                DateTimeOffset.UtcNow);

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}