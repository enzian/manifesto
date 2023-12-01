module controllers

open FSharp.Control.Reactive
open System
open locations
open stock
open api
open logistics
open production

let createLocationsForPhantomStock
    (stockApi: api.ManifestApi<StockSpecManifest>)
    (locationsApi: api.ManifestApi<LocationSpecManifest>)
    (allLocations: IObservable<Map<string, LocationSpecManifest>>)
    (allStocks: IObservable<Map<string, StockSpecManifest>>) = 

    (Observable.combineLatest allLocations allStocks)
        .Subscribe(
            (fun (locs, stocks) ->
                let locationIds = locs.Values |> Seq.map (fun x -> x.spec.Id)

                let phantomStocks =
                    stocks.Values
                    |> Seq.filter (fun x -> locationIds |> Seq.contains x.spec.Location |> not)

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
                    createLocation phantomStock.spec.Location |> ignore
                )
        )

let createTransportsForProduction
    (transportsApi: api.ManifestApi<TransportSpecManifest>)
    (allProductionOrders: IObservable<Map<string, ProductionOrderSpecManifest>>)
    (allTransports: IObservable<Map<string, TransportSpecManifest>>)
    (allStocks: IObservable<Map<string, StockSpecManifest>>) = 
    allProductionOrders 
    |> Observable.combineLatest allTransports
    |> Observable.combineLatest allStocks
    |> Observable.subscribe (fun (stocksMap,(transportsMap,productionsMap)) ->
        let transports = transportsMap.Values
        let stocks = stocksMap.Values
        let productions = productionsMap.Values

        let prodOrdersWithMissingTransports =
            productions
            |> Seq.map (fun p -> p.spec.bom |> Seq.map (fun bomLine -> {|order = p.metadata.name; full_order = p; material = bomLine.material; quantity = bomLine.quantity|}))
            |> Seq.concat
            |> Seq.filter (fun bomLine ->  not (transports |> Seq.exists (
                    fun transport ->
                        transport.spec.material = bomLine.material
                        && transport.spec.quantity = bomLine.quantity
                        && transport.metadata.labels 
                            |> Option.exists (fun labels -> labels.["transports.stockr.io/production_order"] = bomLine.order))))
        printfn "Prod Orders with missing transports: %A" (prodOrdersWithMissingTransports |> Seq.map _.order |> Seq.distinct)

        let nextTransport = prodOrdersWithMissingTransports |> Seq.tryHead
        match nextTransport with
        | None -> printfn "No more transports to create"
        | Some bomline -> 
            let res = 
                let transportId = sprintf "%s-%s-%s" bomline.order bomline.material bomline.quantity
                transportsApi.Put {
                    metadata = {
                        name = transportId
                        ``namespace``= None
                        labels = Some ( [
                            ("transports.stockr.io/autocreated","true")
                            ("transports.stockr.io/createdAt", DateTimeOffset.UtcNow.ToString("o"))
                            ("transports.stockr.io/production_order", bomline.order)
                            ] |> Map.ofSeq )
                        annotations = None
                        revision = None
                    }
                    spec = { 
                        material = bomline.material
                        quantity = bomline.quantity 
                        source = stocks |> Seq.tryFind (fun x -> x.spec.Material = bomline.material) |> Option.map (fun x -> x.spec.Location)
                        target = Some bomline.full_order.spec.from
                    }
                        
                }

            match res with 
            | Ok () -> printfn "created transport %A" bomline
            | Error e -> printfn "failed to create transport %A" e
    )
    |> ignore