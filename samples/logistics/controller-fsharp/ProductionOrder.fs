module production
open api

type ProductionLine = {
    material: string
    quantity: string
}

type ProductionOrder = {
    id : string
    bom: ProductionLine list
    material: string
    quantity: string
    from: string
    target: string
}

type ProductionOrderSpecManifest = 
    { spec: ProductionOrder
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 