module measurements


type Unit = Unit of string

type Quantity = 
    | Quantity of double
    member this.Value = 
        match this with
        | Quantity (q) -> q

type Amount =
    | Amount of Quantity * Unit
    member this.toString() = 
        match this with
        | Amount (Quantity (q), Unit (u)) -> sprintf "%f%s" q u 

module units =
    module length =
        let km = Unit "km"
        let m = Unit "m"
        let cm = Unit "cm"
        let mm = Unit "mm"

        let convert (amount: Amount) (target: Unit) : Amount = 
            let (Amount (Quantity q, u)) = amount
            let factor = 
                match (u, target) with
                | (Unit "km", Unit "m") -> 1000.0
                | (Unit "km", Unit "cm") -> 100000.0
                | (Unit "km", Unit "mm") -> 1000000.0
                | (Unit "m", Unit "km") -> 0.001
                | (Unit "m", Unit "cm") -> 100.0
                | (Unit "m", Unit "mm") -> 1000.0
                | (Unit "cm", Unit "km") -> 0.00001
                | (Unit "cm", Unit "m") -> 0.01
                | (Unit "cm", Unit "mm") -> 10.0
                | (Unit "mm", Unit "km") -> 0.000001
                | (Unit "mm", Unit "m") -> 0.001
                | (Unit "mm", Unit "cm") -> 0.1
                | _ -> failwithf "Cannot %s to %s" (u |> string) (target |> string)
            Amount(Quantity (q * factor), target)
    
    module weight =
        let kg = Unit "kg"
        let g = Unit "g"
        let mg = Unit "mg"
        let t = Unit "t"

        let convert (amount: Amount) (target: Unit) : Amount = 
            let (Amount (Quantity q, u)) = amount
            let factor = 
                match (u, target) with
                | (Unit "kg", Unit "g") -> 1000.0
                | (Unit "kg", Unit "mg") -> 1000000.0
                | (Unit "kg", Unit "t") -> 0.001
                | (Unit "g", Unit "kg") -> 0.001
                | (Unit "g", Unit "mg") -> 1000.0
                | (Unit "g", Unit "t") -> 0.000001
                | (Unit "mg", Unit "kg") -> 0.000001
                | (Unit "mg", Unit "g") -> 0.001
                | (Unit "mg", Unit "t") -> 0.000000001
                | (Unit "t", Unit "kg") -> 1000.0
                | (Unit "t", Unit "g") -> 1000000.0
                | (Unit "t", Unit "mg") -> 1000000000.0
                | _ -> failwithf "Cannot %s to %s" (u |> string) (target |> string)
            Amount(Quantity (q * factor), target)
    
    module volume =
        let l = Unit "l"
        let ml = Unit "ml"
        let cl = Unit "cl"
        let dl = Unit "dl"
        let hl = Unit "hl"

        let convert (amount: Amount) (target: Unit) : Amount = 
            let (Amount (Quantity q, u)) = amount
            let factor = 
                match (u, target) with
                | (Unit "l", Unit "ml") -> 1000.0
                | (Unit "l", Unit "cl") -> 100.0
                | (Unit "l", Unit "dl") -> 10.0
                | (Unit "l", Unit "hl") -> 0.1
                | (Unit "ml", Unit "l") -> 0.001
                | (Unit "cl", Unit "l") -> 0.01
                | (Unit "dl", Unit "l") -> 0.1
                | (Unit "hl", Unit "l") -> 10.0
                | _ -> failwithf "Cannot %s to %s" (u |> string) (target |> string)
            Amount(Quantity (q * factor), target)
    
