
using KeyValueStore.Api.Contracts;
using KeyValueStore.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace KeyValueStore.Api.Controllers;

[ApiController]
[Route("api/keyspaces/{keyspace}/entries")]
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
        if (request.Value is null)
            return BadRequest(new { error = "Value cannot be null." });

        try
        {
            _setEntry.Execute(
                keyspace,
                key,
                request.Value,
                DateTimeOffset.UtcNow);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{key}")]
    public IActionResult Get(string keyspace, string key)
    {
        try
        {
            var entry = _getEntry.Execute(keyspace, key);

            if (entry is null)
                return NotFound();

            return Ok(new EntryResponse(
                entry.Key,
                entry.Value,
                entry.CreatedAt,
                entry.UpdatedAt,
                entry.ExpiresAt));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{key}")]
    public IActionResult Delete(string keyspace, string key)
    {
        try
        {
            bool deleted = _deleteEntry.Execute(keyspace, key);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
