module Tests

open Xunit
open Manifesto.AspNet.api.v1.models
open FsUnit

[<Theory>]
[<InlineData("test=none", "test", "none")>]
[<InlineData(" test = none ", "test", "none")>]
[<InlineData("test.uri.org/key=notempty ", "test.uri.org/key", "notempty")>]
let ``Parse Equality Filter Conditions`` (selector:string) (key:string) (value:string) =
    let parsed = stringToConditions selector
    parsed |> should equal [|Equals (key, value)|]
    

[<Theory>]
[<InlineData("test!=none", "test", "none")>]
[<InlineData(" test != none ", "test", "none")>]
let ``Parse Inequality Filter Conditions`` (selector:string) (key:string) (value:string) =
    let parsed = stringToConditions selector
    parsed |> should equal [|NotEquals (key, value)|]

[<Theory>]
[<InlineData("test", "test")>]
[<InlineData(" test ", "test")>]
let ``Parse KeyExists Filter Conditions`` (selector:string) (key:string) =
    let parsed = stringToConditions selector
    parsed |> should equal [|Exists key|]

[<Theory>]
[<InlineData("!test", "test")>]
[<InlineData(" !test ", "test")>]
[<InlineData(" ! test ", "test")>]
let ``Parse KeyNotExists Filter Conditions`` (selector:string) (key:string) =
    let parsed = stringToConditions selector
    parsed |> should equal [|DoesNotExist key|]

[<Theory>]
[<InlineData("test in (1,2,3)", "test", "1,2,3")>]
[<InlineData("test in ( 1, 2, 3 3)", "test", "1,2,3 3")>]
[<InlineData("test in (none, any)", "test", "none,any")>]
let ``Parse IsInSet Filter Conditions`` (selector:string) (key:string) (value:string) =
    let set = value.Split(',') |> Seq.map (fun x -> x.Trim())
    let parsed = stringToConditions selector
    parsed |> should equal [|In (key, set)|]

[<Theory>]
[<InlineData("test notin (1,2,3)", "test", "1,2,3")>]
[<InlineData("test notin (none, any)", "test", "none,any")>]
let ``Parse IsNotInSet Filter Conditions`` (selector:string) (key:string) (value:string) =
    let set = value.Split(',') |> Seq.map (fun x -> x.Trim())
    let parsed = stringToConditions selector
    parsed |> should equal [|NotIn (key, set)|]