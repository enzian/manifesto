module api

open FsHttp
open System.IO
open System.Text.Json
open System.Threading
open System
open System.Net.Http
open filter

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


type Event<'T when 'T :> Manifest> =
    | Update of 'T
    | Create of 'T
    | Delete of 'T

type WireEvent<'T> =
    { ``type``: string
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
    abstract List: seq<'T>
    abstract FilterByLabel: (KeyIs seq -> 'T seq)
    abstract Watch: (CancellationToken -> Async<IObservable<Event<'T>>>)
    abstract WatchFromRevision: (uint -> CancellationToken -> Async<IObservable<Event<'T>>>)
    abstract Put: ('T -> Result<unit, exn>)
    abstract Delete: (string -> Result<unit, exn>)

let jsonOptions = new JsonSerializerOptions()
jsonOptions.PropertyNameCaseInsensitive <- true

let watchResource<'T when 'T :> Manifest> (client: HttpClient) uri (revision:uint option) (cts: CancellationToken) =
    async {
        let path = 
            match revision with 
            | Some r -> Path.Combine ("watch/", uri, (sprintf "?revision=%i" r)) 
            | None -> Path.Combine("watch/", uri)
        
        let! responseSteam = client.GetStreamAsync(path, cts) |> Async.AwaitTask

        let streamReader = new StreamReader(responseSteam)

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
                member x.Subscribe(observer) =
                    readEvent observer streamReader cts |> Async.StartDisposable }

        return
            lineReaderObservable
            |> Observable.map (fun line -> JsonSerializer.Deserialize<WireEvent<'T>>(line, jsonOptions))
            |> Observable.map (fun wireEvent ->
                match wireEvent.``type`` with
                | "MODIFIED" -> Update wireEvent.object
                | "ADDED" -> Create wireEvent.object
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


let listWithKey<'T when 'T :> Manifest> httpClient path =
    try

        http {
            config_transformHttpClient (fun _ -> httpClient)
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
        }
        |> Request.send
        |> Response.deserializeJson<seq<'T>>
    with _ ->
        Seq.empty

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

let listWithFilter<'T when 'T :> Manifest> httpClient path (keyIs: KeyIs seq) =
    try
        let filter = keyIs |> Seq.map formatLabelFilter |> String.concat ","
        http {
            config_transformHttpClient (fun _ -> httpClient)
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
            query [("filter", filter)]
        }
        |> Request.send
        |> Response.deserializeJson<seq<'T>>
    with _ ->
        Seq.empty

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
        member _.List = listWithKey httpClient path
        member _.FilterByLabel = listWithFilter httpClient path
        member _.Watch = watchResource httpClient path None
        member _.WatchFromRevision = (fun r cts ->  watchResource httpClient path (Some r) cts)
        member _.Put = putManifest httpClient path
        member _.Delete = dropManifest httpClient path }
