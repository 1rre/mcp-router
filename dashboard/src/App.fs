module App

open Feliz
open Types
open Components.ServerCard
open Components.AddServerModal
open Fable.Core.JsInterop

importSideEffects "./index.css"

[<ReactComponent>]
let App () =
    let servers, setServers = React.useState<ServerInfo array> [||]
    let loading, setLoading = React.useState true
    let showAddModal, setShowAddModal = React.useState false

    let loadServers () =
        setLoading true
        Api.getServers()
        |> Promise.iter (fun data ->
            setServers data
            setLoading false)
        |> ignore

    React.useEffect(loadServers, [||])

    let handleAdd (server: ServerInfo) =
        setServers (Array.append servers [| server |])
        setShowAddModal false

    let handleReload (name: string) =
        Api.reloadServer name
        |> Promise.iter (fun updated ->
            setServers (servers |> Array.map (fun s -> if s.name = name then updated else s)))
        |> ignore

    let handleRemove (name: string) =
        Api.removeServer name
        |> Promise.iter (fun () ->
            setServers (servers |> Array.filter (fun s -> s.name <> name)))
        |> ignore

    let handleToolToggle (serverName: string) (toolName: string) (enabled: bool) =
        Api.setToolEnabled serverName toolName enabled
        |> Promise.iter (fun () ->
            setServers (
                servers |> Array.map (fun s ->
                    if s.name <> serverName then s
                    else
                        { s with
                            tools = s.tools |> Array.map (fun t ->
                                if t.name = toolName then { t with enabled = enabled } else t) })))
        |> ignore

    Html.div [
        prop.className "min-h-screen bg-gray-50"
        prop.children [
            Html.header [
                prop.className "bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between"
                prop.children [
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            Html.h1 [
                                prop.className "text-lg font-bold text-gray-900"
                                prop.text "MCP Router"
                            ]
                            Html.span [
                                prop.className "text-xs text-gray-400 font-mono"
                                prop.text "localhost:6767"
                            ]
                        ]
                    ]
                    Html.button [
                        prop.className "px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg"
                        prop.onClick (fun _ -> setShowAddModal true)
                        prop.text "+ Add Server"
                    ]
                ]
            ]

            Html.main [
                prop.className "max-w-5xl mx-auto px-6 py-8"
                prop.children [
                    if loading then
                        Html.p [
                            prop.className "text-center text-gray-400 py-16"
                            prop.text "Loading…"
                        ]
                    elif servers.Length = 0 then
                        Html.div [
                            prop.className "text-center py-20"
                            prop.children [
                                Html.p [
                                    prop.className "text-gray-400 text-sm mb-4"
                                    prop.text "No servers loaded yet."
                                ]
                                Html.button [
                                    prop.className "px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg"
                                    prop.onClick (fun _ -> setShowAddModal true)
                                    prop.text "Add your first server"
                                ]
                            ]
                        ]
                    else
                        Html.div [
                            prop.className "grid gap-4 sm:grid-cols-1 lg:grid-cols-2"
                            prop.children [
                                for server in servers do
                                    ServerCard server handleReload handleRemove handleToolToggle
                            ]
                        ]
                ]
            ]

            if showAddModal then
                AddServerModal handleAdd (fun () -> setShowAddModal false)
        ]
    ]

open Browser.Dom

let root = ReactDOM.createRoot (document.getElementById "root")
root.render (App())
