using System.Security.Claims;
using KeyValueStore.Api.Authentication;
using KeyValueStore.Api.Contracts;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyValueStore.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName,
    Roles = nameof(UserRole.Admin))]
public sealed class AdminUsersController : ControllerBase
{
    private readonly CreateUserUseCase _createUser;

    public AdminUsersController(
        CreateUserUseCase createUser)
    {
        _createUser = createUser;
    }

    [HttpPost]
    public IActionResult Create(
        [FromBody] CreateUserRequest? request)
    {
        UserRole? role = request?.Role?
            .Trim()
            .ToLowerInvariant() switch
        {
            "reader" => UserRole.Reader,
            "writer" => UserRole.Writer,
            "admin" => UserRole.Admin,
            _ => null
        };

        if (role is null)
        {
            return BadRequest(
                "Role must be Reader, Writer, or Admin.");
        }

        string? userIdValue = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
            userIdValue,
            out Guid requesterId) ||
            requesterId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            CreatedUser created = _createUser.Execute(
                requesterId,
                role.Value);

            var response = new CreateUserResponse(
                created.UserId,
                created.Keyspace,
                created.Role.ToString());

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