using System.Security.Claims;
using KeyValueStore.Api.Authentication;
using KeyValueStore.Api.Contracts;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyValueStore.Api.Controllers;

[ApiController]
[Route("api/admin/users/{userId:guid}/api-keys")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = nameof(UserRole.Admin))]
public sealed class AdminApiKeysController : ControllerBase
{
    private readonly IssueApiKeyUseCase _issueApiKey;

    public AdminApiKeysController(
        IssueApiKeyUseCase issueApiKey)
    {
        _issueApiKey = issueApiKey;
    }

    [HttpPost]
    public IActionResult Issue(Guid userId)
    {
        if (userId == Guid.Empty)
            return BadRequest("User ID cannot be empty.");

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
            IssuedApiKey issued = _issueApiKey.Execute(
                requesterId,
                userId,
                DateTimeOffset.UtcNow);

            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Pragma"] = "no-cache";

            var response = new IssueApiKeyResponse(
                issued.KeyId,
                issued.UserId,
                issued.PlaintextKey,
                issued.CreatedAt);

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}