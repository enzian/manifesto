open System.Net.Http
open System
open stock
open System.Threading
open locations
open utilities
open logistics
open production
open events
open api

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
let eventsApi =
    api.ManifestsFor<EventManifest> client "events.stockr.io/v1/events/"

let sendEvent instance controller ``type`` action reason note = 
    eventsApi.Put {
        metadata = {
            name = Guid.NewGuid().ToString()
            labels = [] |> Map.ofList |> Some
            annotations = None
            revision = None
            ``namespace`` = None
        }
        eventTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        action = action
        note = note
        reason = reason
        reportingController = controller
        reportingInstance = instance
        ``type`` = ``type``
    } |> ignore

let machineName = Environment.MachineName
let productionOrderControllerLog = sendEvent machineName "production-order-transports" "NORMAL"
let productionOrderControllerWarn = sendEvent machineName "production-order-transports" "WARNING"

let transportCtlLogger = {
    new IEventLogger with 
        member _.Log = sendEvent machineName "production-order-transports" "NORMAL"
        member _.Warn = sendEvent machineName "production-order-transports" "NORMAL"
}

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
controllers.CreateTransportsForNewProductionOrders transportsApi productionOrderChanges aggregatedStocks transportCtlLogger |> ignore
controllers.CancelTransportsForDeleteProductionOrders transportsApi productionOrderChanges transportCtlLogger |> ignore
controllers.UpdateProductionOrderTransports transportsApi productionOrderChanges transportCtlLogger |> ignore

Console.CancelKeyPress.Add(fun _ ->
    printfn "Canceling watch"
    cts.Cancel())

async {
    sem.WaitAsync() |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Shutting down..."

}
|> Async.RunSynchronously
