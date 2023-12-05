namespace Manifesto.AspNet

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text.Json
open System.Runtime.CompilerServices

open Manifesto.AspNet.api.v1.controllers
open api.v1.controllers
open System

module hosting =

    let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

    let configureApp resourceEndpointHandler (appBuilder: IApplicationBuilder) =
        appBuilder.UseRouting |> ignore
        appBuilder.UseGiraffe resourceEndpointHandler |> ignore
        appBuilder.UseGiraffe notFoundHandler

    let configureServices (services: IServiceCollection) =
        services.AddRouting().AddGiraffe() |> ignore
        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(new JsonSerializerOptions())) |> ignore

[<Extension>]
type HostingExtensions =
    [<Extension>]
    static member AddManifestoV1 (x: WebApplicationBuilder)  =
        x.Services |> hosting.configureServices |> ignore

    [<Extension>]
    static member UseManifestoV1 (x:IApplicationBuilder, keyspaceFactory:System.Func<string, string , string, string>, ttl: System.Func<string, string, string, Nullable<int64>>) =
        let ksp group version typ = keyspaceFactory.Invoke(group, version, typ)
        let ttl group version typ = 
            let v = ttl.Invoke(group, version, typ)
            if v.HasValue then Some v.Value else None
        x |> hosting.configureApp ( endpoints ksp ttl) |> ignore