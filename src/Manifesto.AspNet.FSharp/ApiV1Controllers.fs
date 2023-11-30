namespace Manifesto.AspNet.FSharp.api.v1
        
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text.Json
open dotnet_etcd.interfaces
open System.Collections.Generic
open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions
open System.Collections.Generic

open models

module controllers =
    let ManifestListHandler keyspaceFactory ((group: string), (version: string), (typ: string)) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            let results = client.GetRange(keyspace)
            
            let manifests = 
                results.Kvs
                |> Seq.map (
                    fun kv ->
                        let manifest = 
                            kv.Value.ToByteArray() 
                            |> System.Text.Encoding.UTF8.GetString
                            |> JsonSerializer.Deserialize<Manifest>
                        {manifest with metadata.revision = Some (kv.ModRevision.ToString())}
                    )
            
            let filteredManifests =
                match ctx.GetQueryStringValue "filter" with
                | Error msg -> manifests
                | Ok q ->
                    let conditions = stringToConditions q
                    manifests |> Seq.filter (fun m -> matchConditions conditions (m.metadata.labels |> Option.defaultValue Map.empty))

            (filteredManifests |> json) next ctx

    let ManifestCreationHandler keyspaceFactory ((group: string), (version: string), (typ: string)) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let manifest = JsonSerializer.Deserialize<Manifest>(ctx.Request.BodyReader.AsStream())
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            let key = sprintf "%s/%s" keyspace manifest.metadata.name
            let value = JsonSerializer.Serialize manifest
            let putResult = client.Put(key, value)
            let revisionedManifest = {manifest with metadata.revision = Some (putResult.Header.Revision.ToString())}

            (revisionedManifest |> json) next ctx
    
    let ManifestDeleteHandler keyspaceFactory ((group: string), (version: string), (typ: string), (name: string)) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            let key = sprintf "%s/%s" keyspace name
            let deleteResult = client.Delete(key)
            
            (if deleteResult.Deleted > 0 then text "deleted" 
                else RequestErrors.notFound ("Not Found" |> text)
            ) next ctx
    
    
    let ManifestWatchHandler keyspaceFactory ((group: string), (version: string), (typ: string)) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ

            let handle (resp: Etcdserverpb.WatchResponse) = 
                if resp.Created = true then
                    ctx.Response.StatusCode <- 200
                    ctx.SetContentType "text/event-stream"
                    ctx.Response.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                    ctx.Response.Body.FlushAsync() |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                else
                    let apiEvents =  
                        resp.Events.AsReadOnly()
                        |> Seq.map (
                            fun event ->
                                match event.Type with
                                | Mvccpb.Event.Types.EventType.Put ->
                                    let putAction = if event.Kv.CreateRevision < event.Kv.ModRevision then "CREATED" else "UPDATED"
                                    let manifest = event.Kv.Value.ToByteArray() |> System.Text.Encoding.UTF8.GetString |> JsonSerializer.Deserialize<Manifest>
                                    let versionedManifest = {manifest with metadata.revision = Some (event.Kv.ModRevision.ToString())}
                                    {eventType = putAction; object = versionedManifest}
                                | Mvccpb.Event.Types.EventType.Delete ->
                                    let manifest = event.PrevKv.Value.ToByteArray() |> System.Text.Encoding.UTF8.GetString |> JsonSerializer.Deserialize<Manifest>
                                    let versionedManifest = {manifest with metadata.revision = Some (event.Kv.ModRevision.ToString())}
                                    {eventType = "DELETED"; object = versionedManifest}
                                | _ -> failwith "Unknown event type"
                        )
                    let filteredEvents =
                        match ctx.GetQueryStringValue "filter" with
                        | Error msg -> apiEvents
                        | Ok q ->
                            let conditions = stringToConditions q
                            apiEvents |> Seq.filter (fun e -> matchConditions conditions (e.object.metadata.labels |> Option.defaultValue Map.empty)) 
                    
                    for event in filteredEvents do
                        let serializedJson = (sprintf "%s%s" (JsonSerializer.Serialize event) Environment.NewLine)
                        ctx.Response.WriteAsync(serializedJson, ctx.RequestAborted) |> Async.AwaitTask |> Async.RunSynchronously |> ignore

            try
                client.WatchRangeAsync([|keyspace|], handle, cancellationToken = ctx.RequestAborted)
                    |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                ("" |> text) next ctx
            with
            | :? OperationCanceledException -> 
                ("completed" |> text |> Successful.OK) next ctx
            