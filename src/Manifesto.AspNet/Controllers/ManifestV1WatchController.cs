namespace Manifesto.AspNet.Controllers;

using System.Text.Json;
using dotnet_etcd.interfaces;
using Etcdserverpb;
using Google.Protobuf;
using Manifesto.AspNet.Manifests;
using Manifesto.AspNet.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Mvccpb.Event.Types;

[ApiController]
[Route("apis/watch/{group}/{version}/{kind}")]
public class ManifestV1WatchController : ControllerBase
{
    private readonly IEtcdClient _etcdClient;
    private readonly ILogger<ManifestV1WatchController> logger;
    private readonly IKeyspaceSource keyspaceSource;

    public ManifestV1WatchController(
        IKeyspaceSource keyspaceSource,
        IEtcdClient client,
        ILogger<ManifestV1WatchController> logger
    )
    {
        this._etcdClient = client;
        this.logger = logger;
        this.keyspaceSource = keyspaceSource;
    }

    [HttpGet(Name = "WatchResourceKind")]
    public async Task<IActionResult> WatchResourceKind(
        string group,
        string version,
        string kind,
        [FromQuery] string? start_revision,
        [FromQuery] string? filter,
        CancellationToken cancellationToken)
    {
        var context = HttpContext;
        var keyspace = keyspaceSource.GetKeySpace(kind, version, group);
        if (string.IsNullOrWhiteSpace(keyspace)) { return NotFound($"no resource type know for group: {group}, version: {version}, kind: {kind}"); }

        var filters = filter is not null ? Selectors.TryParse(filter) : new[] { new Selector.None() };

        try
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/event-stream";

            await _etcdClient.WatchAsync(
                new WatchRequest
                {
                    CreateRequest = new WatchCreateRequest
                    {
                        Key = dotnet_etcd.EtcdClient.GetStringByteForRangeRequests(keyspace),
                        RangeEnd = ByteString.CopyFromUtf8(dotnet_etcd.EtcdClient.GetRangeEnd(keyspace)),
                        ProgressNotify = true,
                        PrevKv = true,
                        StartRevision = start_revision != null ? long.Parse(start_revision) : 0
                    }
                },
                async (response) => await ManifestV1WatchController.HandleReponse(response, filters, context, cancellationToken),
                new Grpc.Core.Metadata(),
                cancellationToken: cancellationToken);

            return Ok();
        }
        catch (TaskCanceledException _)
        {
            return new EmptyResult();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to get resource {keyspace}");
            return new EmptyResult();
        }

    }

    private static async Task HandleReponse(
        WatchResponse response,
        IEnumerable<Selector>? filters,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        {
            foreach (var e in response.Events)
            {
                if (e.Type != EventType.Delete)
                {
                    var manifest = JsonSerializer.Deserialize<Manifest>(e.Kv.Value.ToStringUtf8());
                    var revisedManifest = manifest.AddRevisionFromEtcdKv(e.Kv);

                    if (Selectors.Validate(filters, manifest.Metadata.Labels ?? new Dictionary<string, string>()))
                    {
                        var watchEvent = e switch
                        {
                            { Type: EventType.Put, Kv.Version: 1 } => new WatchEvent { Type = "ADDED", Object = revisedManifest },
                            { Type: EventType.Put, Kv.Version: > 1 } => new WatchEvent { Type = "MODIFIED", Object = revisedManifest },
                            { Type: EventType.Delete } => new WatchEvent { Type = "DELETED", Object = revisedManifest },
                        };

                        await context.Response.WriteAsync($"{JsonSerializer.Serialize(watchEvent)}{Environment.NewLine}", cancellationToken);
                    }
                }
                else
                {
                    var manifest = JsonSerializer.Deserialize<Manifest>(e.PrevKv.Value.ToStringUtf8());
                    var watchEvent = new WatchEvent { Type = "DELETED", Object = manifest }; 
                    await context.Response.WriteAsync($"{JsonSerializer.Serialize(watchEvent)}{Environment.NewLine}", cancellationToken);
                }

            }

            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}