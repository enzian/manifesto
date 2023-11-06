module stock
open api

type Unit =
    | Unit of string

    member x.Value =
        match x with
        | Unit s -> s

type Material =
    | Material of string

    member x.Value =
        match x with
        | Material s -> s

type Quantity =
    | Quantity of double

    member x.Value =
        match x with
        | Quantity s -> s

    static member (+)(Quantity (left), Quantity (right)) = left + right |> Quantity
    static member (-)(Quantity (left), Quantity (right)) = left - right |> Quantity


type Amount = {
    qty: Quantity
    unit: Unit
}

type Stock = {
    Location: string
    Material: Material
    Amount: Amount 
}

type ApiStockAmount = {
    qty: double
    unit: string
} 
type ApiStock = {
    Location: string
    Material: string
    Amount: ApiStockAmount
}

type StockSpecManifest = 
    { spec: ApiStock
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

let toApiStock (model: Stock) = 
    let (Quantity q) = model.Amount.qty
    let (Unit u) = model.Amount.unit
    
    {
        Location = model.Location
        Material = 
            match model.Material with 
            | Material (s) -> s
        Amount = {
            qty = q
            unit = u
        }
    }