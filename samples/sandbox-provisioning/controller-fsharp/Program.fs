open System.Net.Http
open System
open System.Threading
open locations
open api
open FSharp.Control.Reactive
open utilities

// http client to access the Manifesto APIs
let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

// construct the sandbox APIs
let sandboxApi =
    api.ManifestsFor<SandboxSpecManifest> client "infra.developer.io/v1alpha1/sandbox"
let sandboxStatusApi =
    api.ManifestsFor<SandboxSpecManifest> client "infra.developer.io/v1alpha1/sandbox/status"

let cts = new CancellationTokenSource()
let sem = new SemaphoreSlim(0)

// run the watch command that reads changes from the resource API
let stocks = sandboxApi.List
let stocksRevision = stocks |> mostRecentRevision
let sandboxWatch = sandboxApi.WatchFromRevision stocksRevision cts.Token |> Async.RunSynchronously
printfn "Starting to listen for sandbox changes starting at revision %i" stocksRevision

// Load the initial sandboxes and put them into a map with the name as the key
let initialSandboxObs =
    Subject.behavior (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
// Merge the initial sandboxes map with the change stream
let allSandboxes =
    Observable.merge
        (sandboxWatch
         |> Observable.scanInit (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) mapEventToDict)
        initialSandboxObs

allSandboxes.Subscribe(
    (fun x -> 
        printfn "Sandboxes: %A" x
    )) |> ignore

// Load the initial locations and put them into a map with the name as the key

Console.CancelKeyPress.Add(fun _ ->
    printfn "Stopping controller..."
    cts.Cancel())

async {
    sem.WaitAsync() |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Shutting down..."

}
|> Async.RunSynchronously
