namespace Manifesto.AspNet.Controllers;

using System.Text.Json;
using System.Text.Json.Serialization;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("apis/{group}/{version}/{kind}/{name}")]
public class ManifestV1ReadController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1ReadController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpGet(Name = "GetResourceManifest")]
    public async Task<IActionResult> GetManifestAsync(
        string group,
        string version,
        string kind,
        string name,
        IEtcdClient client,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest("Unknown manifest kind, group or version");
        }

        var key = Path.Combine(keyspace, name);

        try
        {
            var response = await client.GetAsync(key, cancellationToken: cancellationToken);
            if(response.Count == 0) {
                return NotFound();
            }

            var firstKv = response.Kvs.First();

            var first = JsonSerializer.Deserialize<Manifest>(firstKv.Value.Span);
            
            var manifestWithVersion = first with { Metadata = first.Metadata with { Revision = (firstKv.ModRevision > 0 ? firstKv.ModRevision : firstKv.CreateRevision).ToString()}};
            
            return Ok(manifestWithVersion);
        }
        catch (Exception _)
        {
            return this.Problem("failed to get manifest");
        }
    }
}