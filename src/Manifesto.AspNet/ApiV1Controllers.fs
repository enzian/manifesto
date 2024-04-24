namespace Manifesto.AspNet.api.v1

open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text.Json
open dotnet_etcd.interfaces
open System.Collections.Generic
open System
open Etcdserverpb
open Google.Protobuf
open dotnet_etcd

type ResourceType = { group: string;
                      version: string;
                      kind: string
                      plural: string
                      shorthand: string
                      namespaced: bool }


module controllers =
    open models

    let inline toMap kvps =
        kvps
        |> Seq.map (|KeyValue|)
        |> Map.ofSeq

    let ManifestListHandler keyspaceFactory (group: string) (version: string) (typ: string) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let continuation = 
                match ctx.GetQueryStringValue "continuation" |> Result.defaultValue "0" |> Int64.TryParse with
                | (true, i) -> i
                | (false, _) -> 0L
            let limit = 
                match ctx.GetQueryStringValue "limit" |> Result.defaultValue "1000" |> Int64.TryParse with
                | (true, i) -> i
                | (false, _) -> 1000
            
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ

            let mutable manifests = Seq.empty
            let mutable mayBeMore = true;
            let mutable nextContinuation = continuation;

            while manifests |> Seq.length < (int)limit && mayBeMore do
                let rangeQuer = new RangeRequest()
                rangeQuer.Key <- EtcdClient.GetStringByteForRangeRequests(keyspace)
                rangeQuer.RangeEnd <- ByteString.CopyFromUtf8(EtcdClient.GetRangeEnd(keyspace))
                rangeQuer.Limit <- 1000L
                rangeQuer.MinModRevision <- nextContinuation
                let results = client.Get(rangeQuer)

                let manifestsPage = 
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
                    | Error _ -> manifestsPage |> Seq.toList
                    | Ok q ->
                        let conditions = stringToConditions q
                        manifestsPage |> Seq.filter (fun m -> matchConditions conditions (m.metadata.labels |> Option.defaultValue Map.empty)) |> Seq.toList
                
                manifests <- [manifests; filteredManifests] |> Seq.concat |> Seq.truncate (int limit)
                mayBeMore <- results.More
                nextContinuation <- 
                    match (results.Kvs |> Seq.map (fun x -> x.ModRevision) |> Seq.toList) with 
                    | [] -> results.Header.Revision
                    | kvs -> kvs |> Seq.max 
            
            
            let resultPage = { 
                total = manifests |> Seq.length
                items = manifests
                continuation = nextContinuation + 1L}
            (resultPage |> json) next ctx

    let ManifestCreationHandler keyspaceFactory ttl subdocument (group: string) (version: string) (typ: string) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let manifest = JsonSerializer.Deserialize<Manifest>(ctx.Request.BodyReader.AsStream())
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            let ttl = ttl group version typ
            let key = sprintf "%s/%s" keyspace manifest.metadata.name

            let putManifest manifest = 
                let value = JsonSerializer.Serialize manifest
                match ttl with 
                | Some (ttl) ->
                    let lease = client.LeaseGrant (new LeaseGrantRequest(TTL = ttl))
                    let putResult = client.Put(
                        new PutRequest (
                                Key = ByteString.CopyFromUtf8(key),
                                Value = ByteString.CopyFromUtf8(value),
                                Lease = lease.ID))
                    {manifest with metadata.revision = Some (putResult.Header.Revision.ToString())}
                | None ->
                    let putResult = client.Put(key, value)
                    {manifest with metadata.revision = Some (putResult.Header.Revision.ToString())}

            let existingRes = client.Get(key)
            if existingRes.Count > 0 then
                let existingManifest = 
                    existingRes.Kvs
                    |> Seq.head
                    |> fun kv -> kv.Value.ToStringUtf8()
                    |> JsonSerializer.Deserialize<Manifest>
                let updatedSubdocuments = existingManifest.subdocuments |> toMap |> Map.add subdocument manifest.subdocuments.[subdocument]
                let updatedManifest = 
                    {existingManifest with metadata = manifest.metadata ; subdocuments = updatedSubdocuments}
                let updatedManifest = putManifest updatedManifest 
                (updatedManifest |> json) next ctx
            else
                let createdManifest = putManifest manifest
                (createdManifest |> json) next ctx
    
    let ManifestDeleteHandler keyspaceFactory (group: string) (version: string) (typ: string) (name: string) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            let key = sprintf "%s/%s" keyspace name
            let deleteResult = client.Delete(key)
            
            (if deleteResult.Deleted > 0 then text "deleted" 
                else RequestErrors.notFound ("Not Found" |> text)
            ) next ctx
    
    
    let ManifestBatchDeleteHandler keyspaceFactory (group: string) (version: string) (typ: string) : HttpHandler  =
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
            
            let deleteResult = 
                filteredManifests
                |> Seq.map (fun m -> client.Delete(sprintf "%s/%s" keyspace m.metadata.name))
                |> Seq.fold (fun acc x -> acc + x.Deleted) 0L
            
            (sprintf "deleted %i resources" deleteResult |> text) next ctx
    
    let watchReponseHandler (ctx: HttpContext) (resp: Etcdserverpb.WatchResponse) = 
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
                            let putAction = if event.Kv.CreateRevision < event.Kv.ModRevision then "UPDATED" else "CREATED"
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
    
    let ManifestWatchHandler keyspaceFactory (group: string) (version: string) (typ: string) : HttpHandler  =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let client = ctx.RequestServices.GetService<IEtcdClient>()
            let keyspace = keyspaceFactory group version typ
            
            let createReq = new WatchCreateRequest()
            createReq.Key <- EtcdClient.GetStringByteForRangeRequests(keyspace)
            createReq.RangeEnd <- ByteString.CopyFromUtf8(EtcdClient.GetRangeEnd(keyspace))
            createReq.StartRevision <- 
                match ctx.GetQueryStringValue "revision" with
                | Ok q -> Int64.Parse q 
                | _ -> 0
            createReq.PrevKv <- true
            
            let req = new WatchRequest()
            req.CreateRequest <- createReq

            try
                client.WatchAsync(req, (watchReponseHandler ctx), cancellationToken = ctx.RequestAborted)
                    |> Async.AwaitTask |> Async.RunSynchronously |> ignore
                ("" |> text) next ctx
            with
            | :? OperationCanceledException -> 
                ("completed" |> text |> Successful.OK) next ctx

    let endpoints keyspaces ttl isAuthorized =
        let mustHavePermission group version kind verb : HttpHandler = 
            fun next (ctx : HttpContext) ->
                let identity = ctx.User
                let allowed = isAuthorized group version kind verb identity
                if allowed then
                    next ctx
                else
                    RequestErrors.FORBIDDEN "Forbidden" next ctx
                        
        subRoute "/apis"
            (choose [
                GET >=> 
                    choose [
                        routef "/%s/%s/%s/" (fun (group, version, kind) -> 
                            (mustHavePermission group version kind "list") >=> ManifestListHandler keyspaces group version kind)
                        routef "/watch/%s/%s/%s/" (fun (group, version, kind) -> 
                            mustHavePermission group version kind "watch" >=> ManifestWatchHandler keyspaces group version kind)
                ]
                PUT >=> routef "/%s/%s/%s/" (fun (group, version, kind) -> 
                    mustHavePermission group version kind "write" >=> ManifestCreationHandler keyspaces ttl "spec" group version kind)
                PUT >=> routef "/%s/%s/%s/%s" (fun (group, version, kind, subDoc) -> 
                    mustHavePermission group version kind "write" >=> ManifestCreationHandler keyspaces ttl subDoc group version kind)
                DELETE >=> 
                    choose [
                        routef "/%s/%s/%s/%s" (fun (group, version, kind, name) ->  
                            mustHavePermission group version kind "delete" >=> ManifestDeleteHandler keyspaces group version kind name)
                        routef "/%s/%s/%s/" (fun (group, version, kind) ->  
                            mustHavePermission group version kind "delete" >=> ManifestBatchDeleteHandler keyspaces group version kind)
                    ]
                ])
 