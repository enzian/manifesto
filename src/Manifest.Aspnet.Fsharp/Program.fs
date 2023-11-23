namespace Manifest.Aspnet.Fsharp
#nowarn "20"
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

module Program =
    open Giraffe
    let exitCode = 0

    let webApp =
        choose [
            route "/ping"   >=> text "pong"
            route "/"       >=> htmlFile "/pages/index.html" ]

    let configureApp (app : IApplicationBuilder) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp

    let configureServices (services : IServiceCollection) =
        // Add Giraffe dependencies
        services.AddGiraffe() |> ignore

    [<EntryPoint>]
    let main _ =
        Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(
                fun webHostBuilder ->
                    webHostBuilder
                        .Configure(configureApp)
                        .ConfigureServices(configureServices)
                        |> ignore)
            .Build()
            .Run()
        exitCode
