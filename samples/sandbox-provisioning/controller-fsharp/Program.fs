open System.Net.Http
open System
open System.Threading
open locations
open api
open FSharp.Control.Reactive
open utilities
open FSharp.Control.Reactive.Observable

// http client to access the Manifesto APIs
let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

// construct the sandbox APIs
let sandboxApi =
    api.ManifestsFor<SandboxSpecManifest> client "infra.developer.io/v1alpha1/sandbox"

let sandboxStatusApi =
    api.ManifestsFor<SandboxStatusManifest> client "infra.developer.io/v1alpha1/sandbox/status"

let cts = new CancellationTokenSource()
let sem = new SemaphoreSlim(0)

// run the watch command that reads changes from the resource API
let (stocks, stocksRevision) = pageThroughAll (sandboxApi.List cts.Token) 0L 100L

let sandboxWatch =
    sandboxApi.WatchFromRevision stocksRevision cts.Token |> Async.RunSynchronously

printfn "Starting to listen for sandbox changes starting at revision %i" stocksRevision

// Load the initial sandboxes and put them into a map with the name as the key
let initialSandboxObs =
    Subject.behavior (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
// Merge the initial sandboxes map with the change stream
let allSandboxes =
    merge
        (sandboxWatch
         |> Observable.scanInit (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) mapEventToDict)
        initialSandboxObs
    |> publish

allSandboxes.Subscribe(
    (fun x ->
        printfn "currently active sandboxes: %A" (x.Keys)

        let sandboxesWithoutStatus =
            x.Values
            |> Seq.filter (fun x ->
                match x.status with
                | Some _ -> false
                | None -> true)

        for sandbox in sandboxesWithoutStatus do
            sandboxStatusApi.Put
                { metadata = sandbox.metadata
                  status =
                    { provisioning = "true"
                      last_attempt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() } }
            |> ignore
        )
) |> ignore

let timeoutTicker = Observable.timerPeriod DateTimeOffset.Now (TimeSpan.FromSeconds(5))

combineLatest timeoutTicker allSandboxes
|> map (fun (_, x) -> x)
|> map (
    fun (x) ->
        let hasState = function Some _ -> true | None -> false
        let hasExpired sandbox = DateTimeOffset.FromUnixTimeSeconds(sandbox.spec.validUntil |> int64) < DateTimeOffset.UtcNow
        x |> Map.filter (fun k v -> v |> hasExpired && v.status |> hasState))
|> filter (fun x -> not (Map.isEmpty x))
|> subscribe (fun x -> 
    printfn "expired sandboxes: %A" (x.Keys)
    for sandbox in x.Keys do
        printfn "deleting sandbox %s" sandbox
        match sandboxApi.Delete sandbox with
        | Ok () -> printfn "sandbox %s dropped" sandbox
        | Error e -> eprintfn "failed to drop sandbox %s: %A" sandbox e
    )
|> ignore

allSandboxes |> connect |> ignore

Console.CancelKeyPress.Add(fun _ ->
    printfn "Stopping controller..."
    cts.Cancel())

async {
    sem.WaitAsync() |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Shutting down..."

}
|> Async.RunSynchronously
