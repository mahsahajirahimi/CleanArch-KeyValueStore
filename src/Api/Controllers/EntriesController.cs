using System.Security.Claims;
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
        return ExecuteAsRequester(requesterId =>
        {
            try
            {
                _setEntry.Execute(
                    requesterId,
                    keyspace,
                    key,
                    request?.Value,
                    DateTimeOffset.UtcNow,
                    request?.TtlSeconds,
                    request?.Metadata);

                return NoContent();
            }
            catch (ArgumentOutOfRangeException ex)
                when (ex.ParamName == "ttlSeconds")
            {
                return BadRequest(new
                {
                    error = "ttlSeconds must be a positive integer."
                });
            }
            catch (ArgumentException ex)
                when (ex.ParamName == "metadata")
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }
            catch (ArgumentException ex)
                when (ex.ParamName == "value")
            {
                return BadRequest("A value is required.");
            }
        });
    }

    [HttpGet("{key}")]
    public IActionResult Get(
        string keyspace,
        string key)
    {
        return ExecuteAsRequester(requesterId =>
        {
            var entry = _getEntry.Execute(
                requesterId,
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
        });
    }

    [HttpDelete("{key}")]
    public IActionResult Delete(
        string keyspace,
        string key)
    {
        return ExecuteAsRequester(requesterId =>
        {
            bool deleted = _deleteEntry.Execute(
                requesterId,
                keyspace,
                key);

            if (!deleted)
                return NotFound();

            return NoContent();
        });
    }

    private IActionResult ExecuteAsRequester(
        Func<Guid, IActionResult> action)
    {
        string? userIdValue = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
            userIdValue,
            out Guid requesterId))
        {
            return Unauthorized();
        }

        try
        {
            return action(requesterId);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}