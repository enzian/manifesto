module controllers

open FSharp.Control.Reactive
open System
open locations
open stock
open api
open filter
open transports
open production
open events
open FSharp.Control.Reactive.Observable
open measurements

let createLocationsForPhantomStock
    (stockApi: api.ManifestApi<StockSpecManifest>)
    (locationsApi: api.ManifestApi<LocationSpecManifest>)
    (allLocations: IObservable<Map<string, LocationSpecManifest>>)
    (allStocks: IObservable<Map<string, StockSpecManifest>>) = 

    allStocks
    |> log "stocks changed"
    |> withLatestFrom (fun x y -> (y, x)) (allLocations |> log "locations changed")
    |> subscribe (
        (fun (locs, stocks) ->
            let locationIds = locs.Values |> Seq.map (fun x -> x.spec.Id)

            let phantomStocks =
                stocks.Values
                |> Seq.filter (fun x -> locationIds |> Seq.contains x.spec.location |> not)

            printfn "Phantom Stocks: %A" (phantomStocks |> Seq.map (fun x -> x.metadata.name))

            let createLocation loc =
                let res = 
                    locationsApi.Put {
                        metadata = {
                            name = loc
                            ``namespace``= None
                            labels = Some ( [
                                ("locations.stockr.io/autocreated","true")
                                ("locations.stockr.io/createdAt", DateTimeOffset.UtcNow.ToString("o"))
                                ] |> Map.ofSeq )
                            annotations = None
                            revision = None
                        }
                        spec = { Id = loc }
                    }

                match res with 
                | Ok () -> printfn "created locations %A" loc
                | Error e -> printfn "failed to create location %A" e
                res

            for phantomStock in phantomStocks do
                createLocation phantomStock.spec.location |> ignore
            )
        )

let CancelTransportsForDeleteProductionOrders 
    (transportsApi: api.ManifestApi<TransportSpecManifest>)
    (productionEvents: IObservable<Event<ProductionOrderSpecManifest>>)
    (logger: IEventLogger) = 

    productionEvents
    |> map (fun x -> match x with | Delete manifest -> Some manifest | _ -> None)
    |> choose (fun x -> x)
    |> switchMap (fun manifest -> 
        let transports = transportsApi.FilterByLabel 0L 1000L [ ("transports.stockr.io/production_order", Eq manifest.metadata.name) ]
        Subject.behavior transports
    )
    |> subscribe (fun transports ->
        for transport in transports do
            let res = transportsApi.Delete transport.metadata.name
            match res with 
            | Ok () -> logger.Log "REMOVE" "TransportSuccessfullyRemoved" (sprintf "successfully delete transport %s" transport.metadata.name)
            | Error e -> logger.Log "REMOVE" "TransportRemovalFailed" (sprintf "failed to delete transport %A" e)
    )

let UpdateProductionOrderTransports 
    (transportsApi: api.ManifestApi<TransportSpecManifest>)
    (productionEvents: IObservable<Event<ProductionOrderSpecManifest>>)
    (logger: IEventLogger) = 
    
    productionEvents
    |> map (fun x -> match x with | Update manifest -> Some manifest | _ -> None)
    |> choose (fun x -> x)
    |> switchMap (fun manifest -> 
        let transports = transportsApi.FilterByLabel 0L 1000L [ ("transports.stockr.io/production_order", Eq manifest.metadata.name) ]
        Subject.behavior (manifest, transports)
    )
    |> subscribe (fun (productionOrder, transports) -> 
        for line in productionOrder.spec.bom do
            let existingTransport = 
                transports |> Seq.filter (fun x -> x.spec.material = line.material) |> Seq.tryHead
            if existingTransport |> Option.isSome then
                let existingTransport = existingTransport |> Option.get
                if existingTransport.spec.quantity <> line.quantity then
                    (sprintf "Transport %s has wrong quantity. Expected %s, got %s" 
                        existingTransport.metadata.name 
                        (line.quantity |> toAmount |> AmountToString)
                        (existingTransport.spec.quantity |> toAmount |> AmountToString))
                    |> logger.Warn "UPDATE" "ChangeQuantityOfTransport"
                else 
                    (sprintf "Transport %s is correct" existingTransport.metadata.name)
                        |> logger.Log "NONE" "TransportQuantitySufficient"
            else
                (sprintf "Transport for %s is missing" line.material)
                    |> logger.Log "CREATE" "CreateTransportForMissingQuantity"
    )
    
let CreateTransportsForNewProductionOrders
    (transportsApi: api.ManifestApi<TransportSpecManifest>)
    (productionEvents: IObservable<Event<ProductionOrderSpecManifest>>)
    (allStocks: IObservable<Map<string, StockSpecManifest>>)
    (logger: IEventLogger) = 

    productionEvents
    |> map (fun x -> match x with | Create manifest -> Some manifest | _ -> None)
    |> choose (fun x -> x)
    |> switchMap (fun manifest -> 
        (Subject.behavior manifest) |> withLatestFrom (fun x y -> (y, x)) (allStocks) )
    |> subscribe (fun (stocksMap, productionOrder) ->
        let bomLines = 
            productionOrder.spec.bom 
            |> Seq.mapi (fun i bomLine -> 
                {|index = i; material = Material bomLine.material; quantity = (bomLine.quantity |> toAmount)|})
        let stocks = stocksMap.Values
        let transports = bomLines |> Seq.map (fun bomLine -> {|
            material = bomLine.material;
            quantity = bomLine.quantity;
            source = stocks |> Seq.tryFind (fun x -> (Material x.spec.material) = bomLine.material && (x.spec.quantity |> toAmount) >= (bomLine.quantity)) |> Option.map _.spec.location;
            target = Some productionOrder.spec.from|})
        let validTransports = transports |> Seq.filter (fun x -> x.source |> Option.isSome)
        let invalidTransports = transports |> Seq.filter (fun x -> x.source |> Option.isNone)
        if not (invalidTransports |> Seq.isEmpty) then
            (sprintf 
                "Cannot create a transport for po %s because of missing stock: %A"
                productionOrder.metadata.name
                (invalidTransports |> Seq.map (fun x -> {|material = x.material; quantity = x.quantity|})))
                |> logger.Warn "CREATE" "TransportOrderNotCreated"
        else 
            for transport in validTransports do
                let res = 
                    let transportId = sprintf "%s-%s-%s" productionOrder.metadata.name (transport.material |> MaterialToString) (transport.quantity |> AmountToString) 
                    transportsApi.Put {
                        metadata = {
                            name = transportId
                            ``namespace``= None
                            labels = [
                                ("transports.stockr.io/autocreated","true")
                                ("transports.stockr.io/production_order", productionOrder.metadata.name)
                            ] |> Map.ofSeq |> Some 
                            annotations = [
                                ("transports.stockr.io/createdAt", DateTimeOffset.UtcNow.ToString("o"))
                            ] |> Map.ofSeq |> Some
                            revision = None
                        }
                        spec = { 
                            material = transport.material |> MaterialToString
                            quantity = transport.quantity |> AmountToString
                            source = transport.source
                            target = transport.target
                        }
                    }

                match res with 
                | Ok () -> (sprintf "created transport %A" transport ) |> logger.Warn "CREATE" "TransportOrderCreated"
                | Error e -> sprintf "failed to create transport %A" e |> logger.Warn "CREATE" "TransportOrderCreationFailed"

    )
