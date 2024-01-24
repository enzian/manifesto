#r "nuget: FsHttp"
#load "../../src/Manifesto.Client.Fsharp/Filters.fs"
#load "../../src/Manifesto.Client.Fsharp/Api.fs"
#load "controller-fsharp/Locations.fs"

open locations
open api
open System.Net.Http
open System

let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

// construct the stock and location APIs
let locationsApi =
    api.ManifestsFor<LocationSpecManifest> client "logistics.stockr.io/v1alpha1/location/"

let locations = 
    [11..20]
    |> List.map (fun i ->
        [0..120]
        |> List.map (fun j ->
            {
                metadata = {
                    name = sprintf "01-%03i-%03i" i j
                    ``namespace`` = None
                    labels = Some (Map.ofList [ "aisle", i.ToString(); "space", j.ToString()])
                    annotations = None
                    revision = None
                }
                spec = {
                    Id = sprintf "01-%03i-%03i" i j
                }
            })
        )
    |> List.concat

for location in locations do
    match locationsApi.Put location with
    | Ok _ -> printfn "Created location %s" location.metadata.name
    | Error e -> printfn "Error: %A" e
