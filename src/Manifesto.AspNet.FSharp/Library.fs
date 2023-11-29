namespace Manifesto.AspNet.FSharp

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open System.Text.Json
open System.Text.Json.Serialization
open dotnet_etcd.interfaces
open System.Text.RegularExpressions

module api =
    module v1 =
        open System.Collections.Generic

        type Metadata =
            { name: string
              [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
              ``namespace``: string option
              [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
              labels: Map<string, string> option
              [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
              annotations: Map<string, string> option
              [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
              revision: string option }

        [<CLIMutable>]
        type Manifest = {
            metadata: Metadata
            [<JsonExtensionData()>]
            subdocuments: IDictionary<string, JsonElement>
        }

        let (|Regex|_|) pattern input =
            let m = Regex .Match(input, pattern)
            if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
            else None

        type Condition =
            | Equals of string * string
            | NotEquals of string * string
            | Exists of string
            | DoesNotExist of string
            | In of string * string seq
            | NotIn of string * string seq
        
        let stringToConditions (s: string) =
            s.Split(',') 
            |> Array.map (fun s -> s.Trim())
            |> Array.map (
                fun s ->
                    match s with
                    | Regex "^([\\w-/\\.]+)\\s*=\\s*(.+)$" [key; value]
                        -> Some (Equals (key, value))
                    | Regex "^([\\w-/\\.]+)\\s*!=\\s*(.+)$" [key; value] 
                        -> Some (NotEquals (key, value))
                    | Regex "^([\\w-/\\.]+)$" [key] 
                        -> Some (Exists key)
                    | Regex "^!([\\w-/\\.]+)$" [key] 
                        -> Some (DoesNotExist key)
                    | Regex "^([\\w-/\\.]+)\\s*in\\s*\\((.*)\\)$" [key; values] 
                        -> Some (In (key, values.Split(',') |> Array.map _.Trim() |> Array.toSeq))
                    | Regex "^([\\w-/\\.]+)\\s*notIn\\s*\\((.*)\\)$" [key; values] 
                        -> Some (NotIn (key, values.Split(',') |> Array.map _.Trim() |> Array.toSeq))
                    | _ -> None
            )
            |> Array.filter Option.isSome
            |> Array.map Option.get
        
        let matchConditions (conditions: Condition seq) (labels: Map<string, string>) =
            conditions
            |> Seq.forall (
                fun condition ->
                    match condition with
                    | Equals (key, value) -> labels.ContainsKey key && labels.[key] = value
                    | NotEquals (key, value) -> labels.ContainsKey key && labels.[key] <> value
                    | Exists key -> labels.ContainsKey key
                    | DoesNotExist key -> not (labels.ContainsKey key)
                    | In (key, values) -> labels.ContainsKey key && values |> Seq.exists ((=) labels.[key])
                    | NotIn (key, values) -> labels.ContainsKey key && values |> Seq.forall ((<>) labels.[key])
            )

        let ManifestListHandler keyspaceFactory ((group: string), (version: string), (typ: string)) : HttpHandler  =
            fun (next : HttpFunc) (ctx : HttpContext) ->
                let client = ctx.RequestServices.GetService<IEtcdClient>()
                let keyspace = keyspaceFactory group version typ
                let results = client.GetRange(keyspace)
                
                let manifests = 
                    results.Kvs
                    |> Seq.map (
                        fun kv ->
                            kv.Value.ToByteArray() 
                            |> System.Text.Encoding.UTF8.GetString
                            |> JsonSerializer.Deserialize<Manifest>
                        )
                
                let filteredManifests =
                    match ctx.GetQueryStringValue "filter" with
                    | Error msg -> manifests
                    | Ok q ->
                        let conditions = stringToConditions q
                        manifests |> Seq.filter (fun m -> matchConditions conditions (m.metadata.labels |> Option.defaultValue Map.empty))

                (filteredManifests |> json) next ctx

        let ManifestCreationHandler keyspaceFactory ((group: string), (version: string), (typ: string)) : HttpHandler  =
            fun (next : HttpFunc) (ctx : HttpContext) ->
                let manifest = JsonSerializer.Deserialize<Manifest>(ctx.Request.BodyReader.AsStream())
                let client = ctx.RequestServices.GetService<IEtcdClient>()
                let keyspace = keyspaceFactory group version typ
                let key = sprintf "%s/%s" keyspace manifest.metadata.name
                let value = JsonSerializer.Serialize manifest
                let putResult = client.Put(key, value)
                let revisionedManifest = {manifest with metadata.revision = Some (putResult.Header.Revision.ToString())}

                (revisionedManifest |> json) next ctx

        let endpoints keyspaceFactory =
            subRoute "/api/v1"
                (choose [
                    GET >=> 
                        choose [
                            routef "/%s/%s/%s" (ManifestListHandler keyspaceFactory)
                            routef "/watch/%s/%s/%s" (fun (group, version, kind) -> text (sprintf "watch %s %s %s" group version kind))
                    ]
                    PUT >=> routef "/%s/%s/%s" (ManifestCreationHandler keyspaceFactory)
                    DELETE >=> routef "/%s/%s/%s/%s" (fun (group, version, kind, name) -> text (sprintf "delete %s %s %s %s" group version kind name))
                    ])

        let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

        let configureApp keyspace (appBuilder: IApplicationBuilder) =
            appBuilder.UseRouting |> ignore
            appBuilder.UseGiraffe (endpoints keyspace)
            appBuilder.UseGiraffe notFoundHandler

        let configureServices (services: IServiceCollection) =
            services.AddRouting().AddGiraffe() |> ignore
            services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(new JsonSerializerOptions())) |> ignore


    type WebApplicationBuilder with
        member x.AddManifestoV1 =
            x.Services |> v1.configureServices |> ignore
    
    type IApplicationBuilder with
        member x.AddManifestoV1 keyspaceFactory =
            x |> v1.configureApp keyspaceFactory |> ignore
