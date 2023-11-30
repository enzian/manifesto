namespace Manifesto.AspNet.FSharp

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text.Json
open System.Runtime.CompilerServices

open Manifesto.AspNet.FSharp.api.v1.controllers

module hosting =
    let endpoints keyspaceFactory =
        subRoute "/api/v1"
            (choose [
                GET >=> 
                    choose [
                        routef "/%s/%s/%s" (ManifestListHandler keyspaceFactory)
                        routef "/watch/%s/%s/%s" (ManifestWatchHandler keyspaceFactory)
                ]
                PUT >=> routef "/%s/%s/%s" (ManifestCreationHandler keyspaceFactory)
                DELETE >=> routef "/%s/%s/%s/%s" (ManifestDeleteHandler keyspaceFactory)
                ])

    let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

    let configureApp keyspace (appBuilder: IApplicationBuilder) =
        appBuilder.UseRouting |> ignore
        appBuilder.UseGiraffe (endpoints keyspace)
        appBuilder.UseGiraffe notFoundHandler

    let configureServices (services: IServiceCollection) =
        services.AddRouting().AddGiraffe() |> ignore
        services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(new JsonSerializerOptions())) |> ignore

[<Extension>]
type HostingExtensions =
    [<Extension>]
    static member AddManifestoV1 (x: WebApplicationBuilder)  =
        x.Services |> hosting.configureServices |> ignore

    static member UseManifestoV1 (x:IApplicationBuilder, keyspaceFactory:System.Func<string, string , string, string>) =
        let ksp group version typ = keyspaceFactory.Invoke(group, version, typ)
        x |> hosting.configureApp ksp |> ignore