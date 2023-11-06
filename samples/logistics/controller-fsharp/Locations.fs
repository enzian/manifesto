module locations
open api

type Location = {
    Id : string
}

type LocationSpecManifest = 
    { spec: Location
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 