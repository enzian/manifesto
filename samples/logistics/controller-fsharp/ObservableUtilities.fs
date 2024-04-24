module utilities

open api
open System.Threading
open FSharp.Control.Reactive
open utilities
open FSharp.Control.Reactive.Observable

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m -> d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> d.Remove(m.metadata.name)

let watchResourceOfType<'T when 'T :> Manifest> (api: ManifestApi<'T>) (token: CancellationToken) =
    let (initialResources, startRevision) = pageThroughAll (api.List token) 0L 100L
    let initialResourcesObs =
        Subject.behavior (initialResources |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
    
    let watchObs = api.WatchFromRevision startRevision token |> Async.RunSynchronously |> publish
    
    let aggregate = 
        Observable.merge
            (watchObs
             |> Observable.scanInit (initialResources |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) mapEventToDict)
            initialResourcesObs
    watchObs |> connect |> ignore

    (aggregate, watchObs)

