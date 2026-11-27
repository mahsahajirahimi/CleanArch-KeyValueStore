using System.Security.Claims;
using KeyValueStore.Api.Authentication;
using KeyValueStore.Api.Contracts;
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
public sealed class AdminApiKeyRotationsController
    : ControllerBase
{
    private readonly RotateApiKeyUseCase _rotateApiKey;

    public AdminApiKeyRotationsController(
        RotateApiKeyUseCase rotateApiKey)
    {
        _rotateApiKey = rotateApiKey;
    }

    [HttpPost("{keyId:guid}/rotate")]
    public IActionResult Rotate(Guid keyId)
    {
        if (keyId == Guid.Empty)
        {
            return BadRequest(
                "API key ID cannot be empty.");
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
            IssuedApiKey result = _rotateApiKey.Execute(
                requesterId,
                keyId,
                DateTimeOffset.UtcNow);

            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Pragma"] = "no-cache";

            var response = new IssueApiKeyResponse(
                result.KeyId,
                result.UserId,
                result.PlaintextKey,
                result.CreatedAt);

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return Conflict(
                "API key cannot be rotated.");
        }
    }
}