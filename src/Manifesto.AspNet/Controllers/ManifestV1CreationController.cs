using System.Text.Json;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Mvc;

namespace Manifesto.AspNet.Controllers;
[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestV1CreationController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1CreationController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpPut(Name = "PutResourceManifest")]
    public async Task<IActionResult> PutManifestAsync(
        string group,
        string version,
        string kind,
        Manifest manifest,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        return await PutSubdocumentAsync(group, version, kind, "spec",  manifest, client, cancellationToken);
    }

    [HttpPut("{subdocument}", Name = "PutSubdocument")]
    public async Task<IActionResult> PutSubdocumentAsync(
        string group,
        string version,
        string kind,
        string subdocument,
        Manifest manifest,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        if (manifest.Subdocuments.Count == 0 || !manifest.Subdocuments.Keys.Contains(subdocument))
        {
            return BadRequest($"resource manifest does not contain a {subdocument} subdocument");
        }

        var key = Path.Combine(keyspace, manifest.Metadata.Name);


        var typeSanitizedManifest = manifest with { Group = group, Kind = kind, Version = version };

        // check for an existing value at the current key and move the state over from that manifest
        var existingDoc = await client.GetAsync(key, cancellationToken: cancellationToken);
        if (existingDoc != null && existingDoc.Count > 0)
        {
            var existingManifest = JsonSerializer.Deserialize<Manifest>(existingDoc.Kvs.First().Value.ToStringUtf8());
            var subdocumentsToKeep = existingManifest.Subdocuments;
            subdocumentsToKeep[subdocument] = manifest.Subdocuments[subdocument];
            typeSanitizedManifest = typeSanitizedManifest with { Subdocuments = subdocumentsToKeep };
        }
        else
        {
            typeSanitizedManifest = typeSanitizedManifest with {
                Subdocuments = new Dictionary<string, JsonElement> { { subdocument, manifest.Subdocuments[subdocument] } }
            };
        }

        var serializedManifest = JsonSerializer.Serialize(typeSanitizedManifest);

        try
        {
            await client.PutAsync(key, serializedManifest, cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return this.Problem("failed to create or update resource from the given manifest");
        }

        return Ok();
    }
}