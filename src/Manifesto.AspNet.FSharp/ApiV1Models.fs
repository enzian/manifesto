namespace Manifesto.AspNet.FSharp.api.v1

open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions
open System.Collections.Generic

module models = 
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
    
    type Condition =
        | Equals of string * string
        | NotEquals of string * string
        | Exists of string
        | DoesNotExist of string
        | In of string * string seq
        | NotIn of string * string seq
    
    type Event = {
        eventType: string
        object: Manifest
    }
    
    let (|Regex|_|) pattern input =
        let m = Regex .Match(input, pattern)
        if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
        else None

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