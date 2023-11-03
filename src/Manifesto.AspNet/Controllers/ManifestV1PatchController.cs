using System.Dynamic;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using dotnet_etcd.interfaces;
using Manifesto.AspNet.Manifests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Manifesto.AspNet.Controllers;
[ApiController]
[Route("apis/{group}/{version}/{kind}/")]
public class ManifestV1PatchController : ControllerBase
{
    private readonly IEtcdClient _etcdClient;
    private readonly ILogger<ManifestV1PatchController> logger;
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1PatchController(
        IKeyspaceSource keyspaceSource,
        IEtcdClient client,
        ILogger<ManifestV1PatchController> logger
    )
    {
        this._etcdClient = client;
        this.logger = logger;
        this.keyspaceSource = keyspaceSource;
    }

    [HttpPatch("{name}", Name = "PatchResourceName")]
    public async Task<IActionResult> PatchResourceByName(
        string group,
        string version,
        string kind,
        string name,
        [FromBody] JsonPatchDocument<ManifestPatch> patchDoc,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrWhiteSpace(keyspace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

        var key = Path.Combine(keyspace, name);

        try
        {
            var response = await _etcdClient.GetAsync(key, cancellationToken: cancellationToken);
            if (response.Count == 0)
            {
                return NotFound();
            }

            var first_response = response.Kvs.First();
            var first = JsonConvert.DeserializeObject<ManifestPatch>(first_response.Value.ToStringUtf8());

            try
            {
                patchDoc.ApplyTo(first);
                var serializedManifest = JsonSerializer.Serialize(first);

                try
                {
                    await _etcdClient.PutAsync(key, serializedManifest, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    return this.Problem($"failed to update the stored manifest: {e.Message}");
                }

                return Ok(first);
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to apply patch: {e.Message}");
            }

        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to get resource {keyspace}");
            return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to apply patch: {e.Message}");
        }
    }

    [HttpPatch(Name = "PatchResourcesBatched")]
    public async Task<IActionResult> PatchResourcesBatched(
        string group,
        string version,
        string kind,
        string filter,
        [FromBody] JsonPatchDocument<ManifestPatch> patchDoc,
        CancellationToken cancellationToken)
    {
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrWhiteSpace(keyspace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

        var filters = Utilities.Selectors.TryParse(filter);
        if (!filters.Any())
        {
            return BadRequest("failed to parse filter string");
        }

        try
        {
            var response = await _etcdClient.GetRangeAsync(keyspace, cancellationToken: cancellationToken);
            var manifests = response.Kvs
                .Select(x => JsonConvert.DeserializeObject<ManifestPatch>(x.Value.ToStringUtf8()))
                .Where(x => Utilities.Selectors.Validate(filters, x.Metadata.Labels))
                .Select(x => {
                    patchDoc.ApplyTo(x);
                    return x;
                })
                .ToArray();
            
            var tasks = manifests.Select(x => {
                var serializedManifest = JsonSerializer.Serialize(x);
                var key = Path.Combine(keyspace, x.Metadata.Name);
                return _etcdClient.PutAsync(key, serializedManifest, cancellationToken: cancellationToken);
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to apply patch: {e.Message}");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to get resource {keyspace}");
            return StatusCode(StatusCodes.Status500InternalServerError, $"Failed to apply patch: {e.Message}");
        }

        return Ok();
    }

    public class MetadataPatch
    {
        public string Name { get; set; }
        public IDictionary<string, string> Labels { get; set; }
        public IDictionary<string, string> Annotations { get; set; }
        public string? Revision { get; set; }
    }

    public class ManifestPatch
    {
        public string? Kind { get; set; }
        public string? Group { get; set; }
        public string? Version { get; set; }
        public Metadata Metadata { get; set; }
        public ExpandoObject Spec { get; set; }
        public ExpandoObject Status { get; set; }
    }
}