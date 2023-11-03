namespace Manifesto.AspNet.Controllers;

using System.Text.Json;
using System.Text.Json.Serialization;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("apis/{group}/{version}/{kind}")]
public class ManifestV1DeletionController : ControllerBase
{
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1DeletionController(
        IKeyspaceSource keyspaceSource
    )
    {
        this.keyspaceSource = keyspaceSource;
    }

    [HttpDelete("{name}", Name = "DropResourceManifest")]
    public async Task<IActionResult> PutManifestAsync(
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
            await client.DeleteAsync(key, cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return Problem("failed to create or update resource from the given manifest");
        }

        return Ok();
    }
}