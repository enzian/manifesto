namespace api_fsharp
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Manifesto.AspNet.FSharp
open dotnet_etcd.interfaces
open dotnet_etcd

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let keyspace group version typ =
            match (group, version, typ) with 
            | ("stock", "logistics.stockr.io", "v1alpha1") -> $"/registry/stocks"
            | ("stocks", "logistics.stockr.io", "v1alpha1") -> $"/registry/stocks"
            | ("location", "logistics.stockr.io", "v1alpha1") -> $"/registry/locations"
            | ("locations", "logistics.stockr.io", "v1alpha1") -> $"/registry/locations"
            | _ -> ""

        let builder = WebApplication.CreateBuilder(args)
        builder.Services |> api.v1.configureServices |> ignore
        builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379")) |> ignore

        let app = builder.Build()
        app |> api.v1.configureApp keyspace |> ignore
        
        app.Run()

        exitCode
