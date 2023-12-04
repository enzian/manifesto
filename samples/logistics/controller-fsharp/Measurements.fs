module measurements


type Unit = Unit of string

type Quantity = 
    | Quantity of int
    member this.Value = 
        match this with
        | Quantity (q) -> q

type Amount =
    | Amount of Quantity * Unit
    member this.toString() = 
        match this with
        | Amount (Quantity (q), Unit (u)) -> sprintf "%i%s" q u 

module units =
    open System
    [<Literal>]
    let Km = "km"
    [<Literal>]
    let M = "m"
    [<Literal>]
    let Cm = "cm"
    [<Literal>]
    let Mm = "mm"
    let UnitsOfLength = [Km; M; Cm; Mm]
    let lengthConversionRatio (Unit source) (Unit target) =
        match source, target with
        | Km, M -> 1000.0
        | Km, Cm -> 100000.0
        | Km, Mm -> 1000000.0
        | M, Km -> 0.001
        | M, Cm -> 100.0
        | M, Mm -> 1000.0
        | Cm, Km -> 0.00001
        | Cm, M -> 0.01
        | Cm, Mm -> 10.0
        | Mm, Km -> 0.000001
        | Mm, M -> 0.001
        | Mm, Cm -> 0.1
        | _, _ -> failwithf "Cannot convert %s to %s" (source |> string) (target |> string)
    
    [<Literal>]
    let Kg = "kg"
    [<Literal>]
    let G = "g"
    [<Literal>]
    let Mg = "mg"
    [<Literal>]
    let T = "t"
    let UnitsOfMass = [Kg; G; Mg; T]
    let massConversionRatio (Unit source) (Unit target) =
        match source, target with
        | Kg, G -> 1000.0
        | Kg, Mg -> 1000000.0
        | Kg, T -> 0.001
        | G, Kg -> 0.001
        | G, Mg -> 1000.0
        | G, T -> 0.000001
        | Mg, Kg -> 0.000001
        | Mg, G -> 0.001
        | Mg, T -> 0.000000001
        | T, Kg -> 1000.0
        | T, G -> 1000000.0
        | T, Mg -> 1000000000.0
        | _, _ -> failwithf "Cannot convert %s to %s" (source |> string) (target |> string)
    
    [<Literal>]
    let L = "l"
    [<Literal>]
    let Ml = "ml"
    [<Literal>]
    let Cl = "cl"
    [<Literal>]
    let Dl = "dl"
    [<Literal>]
    let Hl = "hl"
    let UnitsOfVolume = [L; Ml; Cl; Dl; Hl]
    let volumeConversionRatio (Unit source) (Unit target) =
        match source, target with
        | L, Ml -> 1000.0
        | L, Cl -> 100.0
        | L, Dl -> 10.0
        | L, Hl -> 0.1
        | Ml, L -> 0.001
        | Ml, Cl -> 0.1
        | Ml, Dl -> 0.01
        | Ml, Hl -> 0.001
        | Cl, L -> 0.01
        | Cl, Ml -> 10.0
        | Cl, Dl -> 0.1
        | Cl, Hl -> 0.01
        | Dl, L -> 0.1
        | Dl, Ml -> 100.0
        | Dl, Cl -> 10.0
        | Dl, Hl -> 0.1
        | Hl, L -> 10.0
        | Hl, Ml -> 10000.0
        | Hl, Cl -> 1000.0
        | Hl, Dl -> 100.0
        | _, _ -> failwithf "Cannot convert %s to %s" (source |> string) (target |> string)

    let all = UnitsOfLength @ UnitsOfMass @ UnitsOfVolume

    let convert (amount: Amount) (target: Unit) = 
        let (Amount (Quantity (q), u)) = amount

        let entityOf (Unit u) = 
            if UnitsOfLength |> List.exists (fun x -> x = u) then "length"
            elif UnitsOfMass |> List.exists (fun x -> x = u) then "mass"
            elif UnitsOfVolume |> List.exists (fun x -> x = u) then "volume"
            else failwith "Unknown unit"

        if (entityOf u) = (entityOf target) then
            let ratio = 
                match entityOf u with
                | "length" -> lengthConversionRatio u target
                | "mass" -> massConversionRatio u target
                | "volume" -> volumeConversionRatio u target
                | _ -> failwith "Unknown unit"
            let quantity = Math.Abs(((float q) * ratio)) |> int 
            Amount (Quantity (quantity), target)
        else failwithf "Cannot convert %s to %s" (u|>string) (target|>string)
