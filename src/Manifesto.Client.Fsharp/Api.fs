module api

open FsHttp
open System.IO
open System.Text.Json
open System.Threading
open System
open System.Net.Http
open filter
open System.Threading.Tasks

type Control.Async with

    static member StartDisposable(op: Async<unit>) =
        let ct = new CancellationTokenSource()
        Async.Start(op, ct.Token)

        { new IDisposable with
            member x.Dispose() = ct.Cancel() }


type Metadata =
    { name: string
      ``namespace``: string option
      labels: Map<string, string> option
      annotations: Map<string, string> option
      revision: string option }

type Manifest = 
    abstract member metadata: Metadata

type Page<'T when 'T :> Manifest> =
    { items: 'T seq
      continuations: int64 }

type Event<'T when 'T :> Manifest> =
    | Update of 'T
    | Create of 'T
    | Delete of 'T

type WireEvent<'T> =
    { eventType: string
      object: 'T }


let formatLabelFilter condition =
    match condition with
    | (k, Eq v ) -> sprintf "%s=%s" k v
    | (k, NotEq v ) ->sprintf "%s!=%s" k v
    | (k, Set) -> sprintf "%s" k
    | (k, NotSet) -> sprintf "!%s" k
    | (k, In values) -> sprintf "%s in (%s)" k (values |> String.concat ",")
    | (k, NotIn values) -> sprintf "%s in (%s)" k (values |> String.concat ",")

type ManifestApi<'T when 'T :> Manifest> =
    abstract Get: string -> Option<'T>
    abstract List: CancellationToken -> int64 -> int64 -> Page<'T>
    abstract FilterByLabel: int64 -> int64 -> KeyIs seq -> Page<'T>
    abstract Watch: CancellationToken -> Async<IObservable<Event<'T>>>
    abstract WatchFromRevision: int64 -> CancellationToken -> Async<IObservable<Event<'T>>>
    abstract Put: 'T -> Result<unit, exn>
    abstract Delete: string -> Result<unit, exn>

let pageThroughAll<'T when 'T :> Manifest> (pager) startOffset limit = 
    let rec pageThrough (offset: int64) (limit: int64) (acc: 'T seq) =
        let page = pager offset limit
        let newAcc = [acc ; page.items] |> Seq.concat
        if page.continuations > 0L then
            pageThrough (offset + limit) limit newAcc
        else
            (newAcc, page.continuations)
    pageThrough startOffset limit Seq.empty

let jsonOptions = new JsonSerializerOptions()
jsonOptions.PropertyNameCaseInsensitive <- true

let watchResource<'T when 'T :> Manifest> (client: HttpClient) uri (revision:int64 option) (cts: CancellationToken) =
    async {
        let path = 
            match revision with 
            | Some r -> Path.Combine ("watch/", uri, (sprintf "?revision=%i" r)) 
            | None -> Path.Combine("watch/", uri)
        
        let rec readEvent (observer: IObserver<_>) (streamReadr: StreamReader) (cts: CancellationToken) =
            async {
                try 
                    while not cts.IsCancellationRequested do
                        let! line = streamReadr.ReadLineAsync(cts).AsTask() |> Async.AwaitTask
                        observer.OnNext(line)
                    with e ->
                        observer.OnError(e)
                        observer.OnCompleted ()
                }

        let lineReaderObservable =
            { new IObservable<_> with
                member _.Subscribe(observer) =
                    async {
                        let backoff = 1000
                        let mutable retryCount = 0
                        while not cts.IsCancellationRequested do
                            try
                                let responseSteam = client.GetStreamAsync(path, cts) |> Async.AwaitTask |> Async.RunSynchronously
                                let streamReader = new StreamReader(responseSteam)
                                while not cts.IsCancellationRequested do
                                    retryCount <- 0
                                    let! line = streamReader.ReadLineAsync(cts).AsTask() |> Async.AwaitTask
                                    observer.OnNext(line)
                            with e ->
                                printfn "Failed to read from watch Socket, retrying with back-off %i" (backoff * retryCount)
                                Task.Delay(retryCount * backoff) |> Async.AwaitTask |> Async.RunSynchronously
                                retryCount <- retryCount + 1
                    }
                    |> Async.StartDisposable
            }

        return
            lineReaderObservable
            |> Observable.map (fun line -> JsonSerializer.Deserialize<WireEvent<'T>>(line, jsonOptions))
            |> Observable.map (fun wireEvent ->
                match wireEvent.eventType with
                | "UPDATED" -> Update wireEvent.object
                | "CREATED" -> Create wireEvent.object
                | "DELETED" -> Delete wireEvent.object
                | _ -> Update wireEvent.object)
    }

let fetchWithKey<'T when 'T :> Manifest> httpClient path resourceKey =
    try
        Some(
            http {
                config_transformHttpClient (fun _ -> httpClient)
                GET(Path.Combine(httpClient.BaseAddress.ToString(), path, resourceKey))
                CacheControl "no-cache"
            }
            |> Request.send
            |> Response.deserializeJson<'T>
        )
    with e ->
        printfn "%A" e
        None

let listWithKey<'T when 'T :> Manifest> httpClient path offset limit ct : Page<'T> =
    try
        http {
            config_transformHttpClient (fun _ -> httpClient)
            config_cancellationToken ct
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
            query [
                ("limit", limit.ToString())
                ("continuation", offset.ToString())]
        }
        |> Request.send
        |> Response.deserializeJson<Page<'T>>
    with e ->
        { items = Seq.empty ; continuations = 0L }

let putManifest<'T> httpClient path (manifest: 'T) =
    try
        http {
            config_transformHttpClient (fun _ -> httpClient)
            PUT(Path.Combine(httpClient.BaseAddress.ToString(), path))
            body
            jsonSerialize manifest
        }
        |> Request.send
        |> ignore

        Ok()
    with e ->
        Error e

let listWithFilter<'T when 'T :> Manifest> httpClient path limit continuation (keyIs: KeyIs seq) =
    try
        let filter = keyIs |> Seq.map formatLabelFilter |> String.concat ","
        http {
            config_transformHttpClient (fun _ -> httpClient)
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
            query [
                ("filter", filter)
                ("limit", limit.ToString())
                ("continuation", continuation.ToString())]
        }
        |> Request.send
        |> Response.deserializeJson<Page<'T>>
    with _ ->
        { items = Seq.empty ; continuations = 0L }

let dropManifest httpClient path key =
    try
        http {
            config_transformHttpClient (fun _ -> httpClient)
            DELETE (Path.Combine(httpClient.BaseAddress.ToString(), path, key))
        }
        |> Request.send
        |> ignore
        Ok ()
    with e ->
        Error e

let ManifestsFor<'T when 'T :> Manifest> (httpClient: HttpClient) (path: string) =
    { new ManifestApi<'T> with
        member _.Get key = fetchWithKey httpClient path key
        member _.List ct offset limit = listWithKey httpClient path offset limit ct
        member _.FilterByLabel continuation limit label = listWithFilter httpClient path limit continuation label
        member _.Watch ct = watchResource httpClient path None ct
        member _.WatchFromRevision r cts  = watchResource httpClient path (Some r) cts
        member _.Put m = putManifest httpClient path m
        member _.Delete name = dropManifest httpClient path name}
