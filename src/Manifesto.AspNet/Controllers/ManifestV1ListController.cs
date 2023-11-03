namespace Manifesto.AspNet.Controllers;

using System.Collections.Immutable;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Manifesto.AspNet.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestV1ListController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1ListController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpGet(Name = "ListResourceManifests")]
    public async Task<IActionResult> ListManifestsAsync(
        [FromRoute] string group,
        [FromRoute] string version,
        [FromRoute] string kind,
        [FromQuery] string? filter,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        return string.IsNullOrWhiteSpace(filter)
            ? await ListManifestsUnfilteredAsync(group, version, kind, client, cancellationToken)
            : await ListManifestsFilteredAsync(group, version, kind, filter, client, cancellationToken);
    }

    private async Task<IActionResult> ListManifestsUnfilteredAsync(
        [FromRoute] string group,
        [FromRoute] string version,
        [FromRoute] string kind,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        try
        {
            var response = await client.GetRangeAsync(keyspace, cancellationToken: cancellationToken);
            var manifests = response.Kvs
                .Select(x => JsonSerializer
                    .Deserialize<Manifest>(x.Value.Span)
                    .AddRevisionFromEtcdKv(x))
                .ToArray();
            return Ok(manifests);
        }
        catch (Exception)
        {
            return Problem("failed to list resources.");
        }
    }

    private async Task<IActionResult> ListManifestsFilteredAsync(
        [FromRoute] string group,
        [FromRoute] string version,
        [FromRoute] string kind,
        [FromQuery] string filter,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        var filters = Selectors.TryParse(filter);
        if (!filters.Any())
        {
            return BadRequest("failed to parse filter string");
        }

        try
        {
            var response = await client.GetRangeAsync(keyspace, cancellationToken: cancellationToken);
            var manifests = response.Kvs
                .Select(x => JsonSerializer
                    .Deserialize<Manifest>(x.Value.Span)
                    .AddRevisionFromEtcdKv(x))
                .Where(x => Selectors.Validate(
                    filters,
                    x.Metadata.Labels ?? ImmutableDictionary<string, string>.Empty))
                .ToArray();
            return Ok(manifests);
        }
        catch (Exception)
        {
            return Problem("failed to list resources.");
        }
    }
}