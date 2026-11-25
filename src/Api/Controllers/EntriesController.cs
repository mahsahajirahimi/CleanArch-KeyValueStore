using KeyValueStore.Api.Authentication;
using KeyValueStore.Api.Contracts;
using KeyValueStore.Application.UseCases;
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

    public EntriesController(
        SetEntryUseCase setEntry,
        GetEntryUseCase getEntry,
        DeleteEntryUseCase deleteEntry)
    {
        _setEntry = setEntry;
        _getEntry = getEntry;
        _deleteEntry = deleteEntry;
    }

    [HttpPut("{key}")]
    public IActionResult Set(
        string keyspace,
        string key,
        [FromBody] SetEntryRequest request)
    {
        if (request is null ||
            request.Value is null)
        {
            return BadRequest(
                "A value is required.");
        }

        _setEntry.Execute(
            keyspace,
            key,
            request.Value,
            DateTimeOffset.UtcNow);

        return NoContent();
    }

    [HttpGet("{key}")]
    public IActionResult Get(
        string keyspace,
        string key)
    {
        var entry = _getEntry.Execute(
            keyspace,
            key);

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
        bool deleted = _deleteEntry.Execute(
            keyspace,
            key);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}