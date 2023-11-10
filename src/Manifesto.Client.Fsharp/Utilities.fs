module utilities
open api

let mostRecentRevision<'TManifest when 'TManifest:> Manifest > (manifests: 'TManifest seq) = 
    manifests
    |> Seq.map (
        fun x -> 
            match x.metadata.revision with 
            | Some r -> r |> uint
            | None -> 0u
        )
    |> (fun s -> 
        match s with 
        | seqence when Seq.isEmpty seqence -> 0u
        | seqence -> seqence |> Seq.max
        )

let mapEventToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m -> d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> d.Remove(m.metadata.name)
