namespace api_fsharp
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Manifesto.AspNet
open dotnet_etcd.interfaces
open dotnet_etcd

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)
        builder.Services |> hosting.configureServices |> ignore
        builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379")) |> ignore

        let keyspace group version typ =
            match (group, version, typ) with 
            | ("stock", "logistics.stockr.io", "v1alpha1") -> $"/registry/stocks"
            | ("stocks", "logistics.stockr.io", "v1alpha1") -> $"/registry/stocks"
            | ("location", "logistics.stockr.io", "v1alpha1") -> $"/registry/locations"
            | ("locations", "logistics.stockr.io", "v1alpha1") -> $"/registry/locations"
            | _ -> ""
        let app = builder.Build()
        app |> hosting.configureApp (api.v1.controllers.endpoints keyspace) |> ignore
        
        app.Run()

        exitCode
