module Api

open Fable.Core
open Fable.Core.JsInterop
open Fetch
open Types

let private fetchJson<'T> (url: string) (init: RequestProperties list) : JS.Promise<'T> =
    promise {
        let! response = fetch url init
        let! json = response.json<'T>()
        return json
    }

let getServers () : JS.Promise<ServerInfo array> =
    fetchJson "/api/servers" []

let addServer (commandLine: string) (friendlyName: string) : JS.Promise<ServerInfo> =
    let body = JS.JSON.stringify {| commandLine = commandLine; friendlyName = friendlyName |}
    fetchJson "/api/servers" [
        Method HttpMethod.POST
        Fetch.requestHeaders [ ContentType "application/json" ]
        Body !!body
    ]

let removeServer (name: string) : JS.Promise<unit> =
    promise {
        let! _ = fetch $"/api/servers/{name}" [ Method HttpMethod.DELETE ]
        return ()
    }

let reloadServer (name: string) : JS.Promise<ServerInfo> =
    fetchJson $"/api/servers/{name}/reload" [ Method HttpMethod.POST ]

let setToolEnabled (serverName: string) (toolName: string) (enabled: bool) : JS.Promise<unit> =
    promise {
        let body = JS.JSON.stringify {| enabled = enabled |}
        let! _ = fetch $"/api/servers/{serverName}/tools/{toolName}" [
            Method HttpMethod.PATCH
            Fetch.requestHeaders [ ContentType "application/json" ]
            Body !!body
        ]
        return ()
    }
