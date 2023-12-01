module logistics
open api

type Transport = {
    id : string
    material: string
    quantity: string
    source: string option
    target: string option
}

type TransportSpecManifest = 
    { spec: Transport
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 