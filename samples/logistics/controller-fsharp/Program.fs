open System.Net.Http
open System
open stock
open System.Threading
open locations
open utilities
open logistics
open production
open FSharp.Control.Reactive.Observable

// http client to access the Manifesto APIs
let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

// construct the stock and location APIs
let stockApi =
    api.ManifestsFor<StockSpecManifest> client "logistics.stockr.io/v1alpha1/stock/"

let locationsApi =
    api.ManifestsFor<LocationSpecManifest> client "logistics.stockr.io/v1alpha1/location/"
let transportsApi =
    api.ManifestsFor<TransportSpecManifest> client "logistics.stockr.io/v1alpha1/transports/"
let productionOrdersApi =
    api.ManifestsFor<ProductionOrderSpecManifest> client "logistics.stockr.io/v1alpha1/production-orders/"

let cts = new CancellationTokenSource()
let sem = new SemaphoreSlim(0)

// initiate watches to always have an up-to-date view of the resources
let (aggregatedStocks, stockChanges)  = watchResourceOfType stockApi cts.Token
let (aggregatedLocations, locationChanges) = watchResourceOfType locationsApi cts.Token
let (aggreatedTransports, transportChanges) = watchResourceOfType transportsApi cts.Token
let (aggregatedProductionOrders, productionOrderChanges) = watchResourceOfType productionOrdersApi cts.Token

// compare the stocks and locations to find all stocks without a proper location.
controllers.createLocationsForPhantomStock stockApi locationsApi aggregatedLocations aggregatedStocks |> ignore

// controllers.createTransportsForProduction transportsApi aggregatedProductionOrders aggreatedTransports aggregatedStocks |> ignore
controllers.CreateTransportsForNewProductionOrders transportsApi productionOrderChanges aggregatedStocks |> ignore
controllers.CancelTransportsForDeleteProductionOrders transportsApi productionOrderChanges |> ignore
controllers.UpdateProductionOrderTransports transportsApi productionOrderChanges |> ignore

Console.CancelKeyPress.Add(fun _ ->
    printfn "Canceling watch"
    cts.Cancel())

async {
    sem.WaitAsync() |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Shutting down..."

}
|> Async.RunSynchronously
