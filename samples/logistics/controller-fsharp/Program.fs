open System.Net.Http
open System
open stock
open System.Threading
open locations
open api
open FSharp.Control.Reactive

// http client to access the Manifesto APIs
let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

// construct the stock and location APIs
let stockApi =
    api.ManifestsFor<StockSpecManifest> client "logistics.stockr.io/v1alpha1/stock"

let locationsApi =
    api.ManifestsFor<LocationSpecManifest> client "logistics.stockr.io/v1alpha1/location"

let cts = new CancellationTokenSource()
let sem = new SemaphoreSlim(0)

// run the watch command that reads changes from the resource API
let stocksWatch = stockApi.Watch cts.Token |> Async.RunSynchronously
let stocks = stockApi.List
let locationsWatch = locationsApi.Watch cts.Token |> Async.RunSynchronously
let locations = locationsApi.List

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m -> d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> d.Remove(m.metadata.name)

// Load the initial stocks and put them into a map with the name as the key
let initialStockObs =
    Subject.behavior (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
// Merge the initial stocks map with the change stream
let allStocks =
    Observable.merge
        (stocksWatch
         |> Observable.scanInit (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialStockObs

// Load the initial locations and put them into a map with the name as the key
let initialLocationsObs =
    Subject.behavior (locations |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
// Merge the initial locations map with the change stream
let allLocations =
    Observable.merge
        (locationsWatch
         |> Observable.scanInit (locations |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialLocationsObs

// compare the stocks and locations to find all stocks without a proper location.
let x =
    (Observable.combineLatest allLocations allStocks)
        .Subscribe(
            (fun (locs, stocks) ->
                let locationIds = locs.Values |> Seq.map (fun x -> x.spec.Id)

                let phantomStock =
                    stocks.Values
                    |> Seq.filter (fun x -> locationIds |> Seq.contains x.spec.Location |> not)
                    |> Seq.map (fun x -> x.metadata.name)

                printfn "Found phantom stock: %A" phantomStock),
            (fun e -> 
                eprintfn "failed: %A" e
                cts.Cancel |> ignore
                sem.Release |> ignore),
            (fun () ->
                printf "combiner completed"
                cts.Cancel |> ignore
                sem.Release |> ignore)
        )

Console.CancelKeyPress.Add(fun _ ->
    printfn "Canceling watch"
    cts.Cancel())

async {
    sem.WaitAsync() |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Shutting down..."

}
|> Async.RunSynchronously
