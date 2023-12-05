module stock
open api
open measurements
open System.Text.RegularExpressions

type Material = Material of string

type Stock = {
    location: string
    material: Material
    amount: Amount 
}

type ApiStock = {
    location: string
    material: string
    quantity: string
}

type StockSpecManifest = 
    { spec: ApiStock
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

let toApiStock (model: Stock) : ApiStock = 
    {
        location = model.location
        material = 
            match model.material with 
            | Material (s) -> s
        quantity = model.amount.toString() 
    }

let AmountToString (Amount (Quantity (q), Unit (u))) = sprintf "%i%s" q u

let MaterialToString (Material (mat)) = mat

let fromApiStock (manifest: ApiStock) : Stock = 
    {
        location = manifest.location
        material = Material manifest.material
        amount = manifest.quantity |> toAmount
    }