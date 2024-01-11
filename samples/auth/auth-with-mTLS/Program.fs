namespace Stockr.Api

#nowarn "20"

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open dotnet_etcd
open dotnet_etcd.interfaces
open Giraffe


open Manifesto.AspNet


module Program =
    open Microsoft.AspNetCore.Authentication.Certificate
    open System.Security.Cryptography.X509Certificates
    open Microsoft.AspNetCore.Server.Kestrel.Core
    open Microsoft.AspNetCore.Server.Kestrel.Https
    open Microsoft.AspNetCore.Http
    open Microsoft.AspNetCore.Hosting
    open System.Net.Security
    open System.Net
    open Manifesto.AspNet.api.v1
    open System.Security.Claims
    open Microsoft.IdentityModel.Tokens
    open System.IdentityModel.Tokens.Jwt
    open Microsoft.AspNetCore.Authentication.JwtBearer
    open Microsoft.IdentityModel.Logging

    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)
        IdentityModelEventSource.ShowPII <- true
        IdentityModelEventSource.LogCompleteSecurityArtifact <- true;

        let caCert =
            X509Certificate2.CreateFromPemFile(
                "samples/auth/auth-with-mTLS/ca.crt",
                "samples/auth/auth-with-mTLS/ca.key"
            )
        
        let jwtCert =
            X509Certificate2.CreateFromPemFile(
                "samples/auth/auth-with-mTLS/jwt.crt",
                "samples/auth/auth-with-mTLS/jwt.key"
            )

        builder.WebHost.ConfigureKestrel(fun (options: KestrelServerOptions) ->
            options.ConfigureHttpsDefaults(fun options ->
                options.ClientCertificateMode <- ClientCertificateMode.AllowCertificate

                options.ClientCertificateValidation <-
                    (fun (cert: X509Certificate2) (chain: X509Chain) (sslPolicyErrors: SslPolicyErrors) ->
                        chain.ChainPolicy.TrustMode <- X509ChainTrustMode.CustomRootTrust
                        chain.ChainPolicy.CustomTrustStore.Add(caCert)
                        let valid = chain.Build(cert)
                        valid))
            options.Listen(IPAddress.Any, 5000)

            options.Listen(
                IPAddress.Any,
                5001,
                (fun listenOptions -> listenOptions.UseHttps("samples/auth/auth-with-mTLS/server.pfx") |> ignore)
            ))

        builder
            .Services
            .AddAuthentication()
            // .AddAuthentication((fun sharedOptions ->
            //     sharedOptions.DefaultScheme <- "smart"
            //     sharedOptions.DefaultChallengeScheme <- "smart"
            //     sharedOptions.DefaultAuthenticateScheme <- "smart"
            // ))
            // .AddPolicyScheme("smart", "JWT or Certificate", (fun options ->
            //     let f (context : HttpContext) = 
            //         let authHeader = context.Request.Headers["Authorization"].ToString()
            //         let scheme =
            //             if authHeader.StartsWith("Bearer ") then
            //                 JwtBearerDefaults.AuthenticationScheme
            //             else 
            //                 CertificateAuthenticationDefaults.AuthenticationScheme
            //         scheme
            //     options.ForwardDefaultSelector <- f
            // ))
            .AddJwtBearer(fun o ->
                let k = new X509SecurityKey(jwtCert)
                let v = new TokenValidationParameters()
                v.ValidateIssuerSigningKey <- false
                v.IssuerSigningKey <- k
                v.ValidateIssuer <- false
                v.ValidateAudience <- false
                v.ValidateLifetime <- false
                o.TokenValidationParameters <- v
                o.IncludeErrorDetails <- true
                ()
            )
            // .AddCertificate(fun u ->
            //     u.AllowedCertificateTypes <- CertificateTypes.All
            //     u.ChainTrustValidationMode <- X509ChainTrustMode.CustomRootTrust
            //     u.AdditionalChainCertificates.Add(caCert)
            //     u.CustomTrustStore.Clear()
            //     u.CustomTrustStore.Add(caCert)

            //     u.RevocationMode <- X509RevocationMode.NoCheck
            //     let events = new CertificateAuthenticationEvents()
            //     events.OnCertificateValidated <- fun context ->
            //         let seg = context.ClientCertificate.SubjectName.EnumerateRelativeDistinguishedNames()
            //         let keys = seg |> Seq.map (fun x -> x.GetSingleElementType().FriendlyName, x.GetSingleElementValue()) |> Map.ofSeq

            //         let claims = [
            //             new Claim(
            //                 ClaimTypes.GroupSid,
            //                 keys.["O"],
            //                 ClaimValueTypes.String, context.Options.ClaimsIssuer)
            //         ]
            //         let identities = [new ClaimsIdentity([claims |> List.toSeq ; context.Principal.Claims ] |> Seq.concat, context.Scheme.Name)]
            //         context.Principal <- new ClaimsPrincipal(identities)


            //         context.Success()
            //         System.Threading.Tasks.Task.CompletedTask
            //     u.Events <- events)


        builder.Services |> hosting.configureServices
        builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"))
        let app = builder.Build()

        let knownKeyspaces group version kind =
            match group, version, kind with
            | "stocks.stockr.io", "v1alpha1", t when [ "stock"; "stocks"; "s" ] |> Seq.contains t ->
                "/registry/stocks.stockr.io/v1alpha1/stocks"
            | "logistics.stockr.io", "v1alpha1", t when
                [ "production-orders"; "po"; "production-order" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/productionorders"
            | "logistics.stockr.io", "v1alpha1", t when [ "transport"; "transports"; "tr" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/transports"
            | "logistics.stockr.io", "v1alpha1", t when [ "locations"; "lo"; "location" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/locations"
            | "events.stockr.io", "v1", t when [ "event"; "events" ] |> Seq.contains t ->
                "/registry/events.stockr.io/v1/events"
            | _ -> null

        let resourceTTL group version kind =
            match group, version, kind with
            | "events.stockr.io", "v1", t when [ "event"; "events" ] |> Seq.contains t -> Some(60L * 60L * 3L)
            | _ -> None

        // Very basic authorization, only allow system:masters group - this can be expanded to arbitrary authorization mechanisms 
        let allowed group version kind verb (identity : ClaimsPrincipal) =
            let isMaster = identity.Claims |> Seq.exists (fun x -> x.Type = ClaimTypes.GroupSid && x.Value = "system:masters")
            match verb with
            | "update" -> isMaster
            | _ -> true

        let unauthenticated =
            RequestErrors.UNAUTHORIZED
                ""
                ""
                "Unauthenticated"

        app.UseAuthentication()

        app.Use (fun context (next : RequestDelegate) ->
            next.Invoke(context)
        )

        app
        |> hosting.configureApp 
            (requiresAuthentication unauthenticated >=> api.v1.controllers.endpoints knownKeyspaces resourceTTL allowed)
        app.Run()
        exitCode
