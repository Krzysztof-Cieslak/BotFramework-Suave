#r "packages/Suave/lib/net40/Suave.dll"
#r "packages/Microsoft.Bot.Connector/lib/net45/Microsoft.Bot.Connector.dll"
#r "packages/Microsoft.Bot.Builder/lib/net45/Microsoft.Bot.Builder.dll"
#r "packages/Microsoft.Rest.ClientRuntime/lib/net45/Microsoft.Rest.ClientRuntime.dll"
#r "packages/Microsoft.WindowsAzure.ConfigurationManager/lib/net40/Microsoft.WindowsAzure.Configuration.dll"
#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

open Suave                 // always open suave
open Suave.Successful      // for OK-result
open Suave.Web             // for config
open Suave.Operators
open Suave.Filters
open Newtonsoft.Json
open Newtonsoft.Json.Serialization

open Microsoft.Bot.Connector
open Microsoft.Bot.Builder.Dialogs

open System
open System.Threading.Tasks

[<AutoOpen>]
module Helpers = 
    let toJson v =
        let jsonSerializerSettings = new JsonSerializerSettings()
        jsonSerializerSettings.ContractResolver <- new CamelCasePropertyNamesContractResolver()

        JsonConvert.SerializeObject(v, jsonSerializerSettings) |> OK
        >=> Writers.setMimeType "application/json; charset=utf-8"

    let fromJson<'a> json =
        JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

    let getResourceFromReq<'a> (req : HttpRequest) =
        let getString rawForm =
            System.Text.Encoding.UTF8.GetString(rawForm)
        req.rawForm |> getString |> fromJson<'a>
        
    let inline startAsPlainTask (work : Async<unit>) = Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

[<Serializable>]
type MyBot () =
   let t = 1

   member this.messageRecived (ctx : IDialogContext) (a : IAwaitable<Message>) = 
       Task.Factory.StartNew(fun () ->
           let msg = a.GetAwaiter().GetResult()
           let t = (sprintf "You said: %s" msg.Text) |> ctx.PostAsync 
           t.Start ()
               
           ctx.Wait <| ResumeAfter(this.messageRecived)
       )

   interface IDialog with
       member this.StartAsync ctx = 
           Task.Factory.StartNew(fun () ->
               ctx.Wait <| ResumeAfter(this.messageRecived)
           )

let botHandler (msg : Message) =
    async {
        let! m = Conversation.SendAsync(msg, (fun _ -> MyBot () :> IDialog), Threading.CancellationToken()) |> Async.AwaitTask
        return m
    } |> Async.RunSynchronously 

let app = 
    path "/api/messages" >=> request (getResourceFromReq >> botHandler >> toJson )


startWebServer defaultConfig app
