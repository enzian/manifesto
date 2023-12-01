module logistics
open api

type Transport = {
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