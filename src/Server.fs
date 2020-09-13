(* 
    Adapted from FSharp.Data.GraphQL project
    HttpHandlers.fs -> https://bit.ly/2ZsDPrO
*)

module BankAPI.GraphQLServer

open System.Text
open System.Collections.Generic
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.FSharp.Reflection
open FSharp.Control.Tasks
open FSharp.Data.GraphQL
open FSharp.Data.GraphQL.Execution
open FSharp.Data.GraphQL.Types
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization
open Giraffe

type HttpHandler = HttpFunc -> HttpContext -> HttpFuncResult

[<AutoOpen>]
module Helpers =
    let tee f x =
        f x
        x

[<AutoOpen>]
module JsonHelpers =
    let tryGetJsonProperty (jobj: JObject) prop =
        match jobj.Property(prop) with
        | null -> None
        | p -> Some(p.Value.ToString())

    let jsonSerializerSettings (converters : JsonConverter seq) =
        JsonSerializerSettings()
        |> tee (fun s ->
            s.Converters <- List<JsonConverter>(converters)
            s.ContractResolver <- CamelCasePropertyNamesContractResolver())

    let jsonSerializer (converters : JsonConverter seq) =
        JsonSerializer()
        |> tee (fun c ->
            Seq.iter c.Converters.Add converters
            c.ContractResolver <- CamelCasePropertyNamesContractResolver())

[<Sealed>]
type OptionConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) =
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.WriteJson(writer, value, serializer) =
        let value =
            if isNull value then null
            else
                let _,fields = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, t, _, serializer) =
        let innerType = t.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then (typedefof<System.Nullable<_>>).MakeGenericType([|innerType|])
            else innerType
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(t)
        if isNull value then FSharpValue.MakeUnion(cases.[0], [||])
        else FSharpValue.MakeUnion(cases.[1], [|value|])

module HttpHandlers =
    let private converters : JsonConverter [] = [| OptionConverter() |]
    let private jsonSettings = jsonSerializerSettings converters

    let okWithStr str : HttpHandler = setStatusCode 200 >=> text str

    let setCorsHeaders : HttpHandler =
        setHttpHeader "Access-Control-Allow-Origin" "*"
        >=> setHttpHeader "Access-Control-Allow-Headers" "content-type"

    let setContentTypeAsJson : HttpHandler =
        setHttpHeader "Content-Type" "application/json"

    let private graphQL (next : HttpFunc) (ctx : HttpContext) = task {
        let serialize d = JsonConvert.SerializeObject(d, jsonSettings)

        let deserialize (data : string) =
            let getMap (token : JToken) =
                let rec mapper (name : string) (token : JToken) =
                    match name, token.Type with
                    | "variables", JTokenType.Object -> token.Children<JProperty>() |> Seq.map (fun x -> x.Name, mapper x.Name x.Value) |> Map.ofSeq |> box
                    | name, JTokenType.Array -> token |> Seq.map (fun x -> mapper name x) |> Array.ofSeq |> box
                    | _ -> (token :?> JValue).Value
                token.Children<JProperty>()
                |> Seq.map (fun x -> x.Name, mapper x.Name x.Value)
                |> Map.ofSeq
            if System.String.IsNullOrWhiteSpace(data)
            then None
            else data |> JToken.Parse |> getMap |> Some

        let json =
            function
            | Direct (data, _) ->
                JsonConvert.SerializeObject(data, jsonSettings)
            | Deferred (data, _, deferred) ->
                deferred |> Observable.add(fun d -> printfn "Deferred: %s" (serialize d))
                JsonConvert.SerializeObject(data, jsonSettings)
            | Stream data ->
                data |> Observable.add(fun d -> printfn "Subscription data: %s" (serialize d))
                "{}"

        let removeWhitespacesAndLineBreaks (str : string) = str.Trim().Replace("\r\n", " ")

        let readStream (s : Stream) =
            use ms = new MemoryStream(4096)
            s.CopyTo(ms)
            ms.ToArray()

        let data = Encoding.UTF8.GetString(readStream ctx.Request.Body) |> deserialize

        let query =
            data |> Option.bind (fun data ->
                if data.ContainsKey("query")
                then
                    match data.["query"] with
                    | :? string as x -> Some x
                    | _ -> failwith "Failure deserializing repsonse. Could not read query - it is not stringified in request."
                else None)

        let variables =
            data |> Option.bind (fun data ->
                if data.ContainsKey("variables")
                then
                    match data.["variables"] with
                    | null -> None
                    | :? string as x -> deserialize x
                    | :? Map<string, obj> as x -> Some x
                    | _ -> failwith "Failure deserializing response. Could not read variables - it is not a object in the request."
                else None)

        match query, variables  with
        | Some query, Some variables ->
            let query = removeWhitespacesAndLineBreaks query
            // let root = { RequestId = System.Guid.NewGuid().ToString() }
            // let result = Schema.executor.AsyncExecute(query, root, variables) |> Async.RunSynchronously
            let result = Schema.executor.AsyncExecute(query, ()) |> Async.RunSynchronously
            return! okWithStr (json result) next ctx
        | Some query, None ->
            let query = removeWhitespacesAndLineBreaks query
            let result = Schema.executor.AsyncExecute(query) |> Async.RunSynchronously
            return! okWithStr (json result) next ctx
        | None, _ ->
            let result = Schema.executor.AsyncExecute(Introspection.IntrospectionQuery) |> Async.RunSynchronously
            return! okWithStr (json result) next ctx
    }

    let webApp : HttpHandler =
        setCorsHeaders
        >=> graphQL
        >=> setContentTypeAsJson

let webApp = choose [ route "/graphql"  >=> HttpHandlers.webApp ]

[<EntryPoint>]
let main args =

    Investec.refreshTokenInBackground ()
    
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .ConfigureKestrel(fun builder ->
                    builder.AllowSynchronousIO <- true)
                .Configure(fun app ->
                    app.UseDefaultFiles()
                       .UseStaticFiles()
                       .UseGiraffe webApp
                )
                .ConfigureServices(fun services ->
                    services.AddGiraffe() |> ignore)
                |> ignore)
        .Build()
        .Run()
    0