using System.Security.Claims;
using KeyValueStore.Api.Authentication;
using KeyValueStore.Api.Contracts;
using KeyValueStore.Application.UseCases;
using KeyValueStore.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyValueStore.Api.Controllers;

[ApiController]
[Route("api/keyspaces/{keyspace}/entries")]
[Authorize(
    AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
public sealed class EntriesController : ControllerBase
{
    private readonly SetEntryUseCase _setEntry;
    private readonly GetEntryUseCase _getEntry;
    private readonly DeleteEntryUseCase _deleteEntry;
    private readonly AuthorizeEntryOperationUseCase _authorizeEntry;

    public EntriesController(
        SetEntryUseCase setEntry,
        GetEntryUseCase getEntry,
        DeleteEntryUseCase deleteEntry,
        AuthorizeEntryOperationUseCase authorizeEntry)
    {
        _setEntry = setEntry;
        _getEntry = getEntry;
        _deleteEntry = deleteEntry;
        _authorizeEntry = authorizeEntry;
    }

    [HttpPut("{key}")]
    public IActionResult Set(
        string keyspace,
        string key,
        [FromBody] SetEntryRequest request)
    {
        IActionResult? denial = CheckAccess(
            keyspace,
            EntryOperation.Write);

        if (denial is not null)
            return denial;

        if (request is null || request.Value is null)
            return BadRequest("A value is required.");

        _setEntry.Execute(
            keyspace,
            key,
            request.Value,
            DateTimeOffset.UtcNow,
            request.TtlSeconds,
            request.Metadata);

        return NoContent();
    }

    [HttpGet("{key}")]
    public IActionResult Get(
        string keyspace,
        string key)
    {
        IActionResult? denial = CheckAccess(
            keyspace,
            EntryOperation.Read);

        if (denial is not null)
            return denial;

        var entry = _getEntry.Execute(keyspace, key);

        if (entry is null)
            return NotFound();

        return Ok(new
        {
            entry.Key,
            entry.Value,
            entry.CreatedAt,
            entry.UpdatedAt,
            entry.ExpiresAt,
            entry.Metadata
        });
    }

    [HttpDelete("{key}")]
    public IActionResult Delete(
        string keyspace,
        string key)
    {
        IActionResult? denial = CheckAccess(
            keyspace,
            EntryOperation.Delete);

        if (denial is not null)
            return denial;

        bool deleted = _deleteEntry.Execute(keyspace, key);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private IActionResult? CheckAccess(
        string keyspace,
        EntryOperation operation)
    {
        string? userIdValue = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
            userIdValue,
            out Guid requesterId))
        {
            return Unauthorized();
        }

        bool allowed = _authorizeEntry.CanExecute(
            requesterId,
            keyspace,
            operation);

        if (!allowed)
            return Forbid();

        return null;
    }
}