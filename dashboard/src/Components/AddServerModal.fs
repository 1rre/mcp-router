module Components.AddServerModal

open Feliz
open Types

[<ReactComponent>]
let AddServerModal (onAdd: ServerInfo -> unit) (onClose: unit -> unit) =
    let commandLine, setCommandLine = React.useState ""
    let friendlyName, setFriendlyName = React.useState ""
    let loading, setLoading = React.useState false
    let error, setError = React.useState<string option> None

    let handleSubmit (e: Browser.Types.Event) =
        e.preventDefault()
        if commandLine.Trim() = "" then
            setError (Some "Command line is required")
        else
            setLoading true
            setError None
            Api.addServer (commandLine.Trim()) (friendlyName.Trim())
            |> Promise.iter (fun server ->
                setLoading false
                onAdd server)
            |> ignore

    Html.div [
        prop.className "fixed inset-0 bg-black/50 flex items-center justify-center z-50"
        prop.onClick (fun _ -> onClose())
        prop.children [
            Html.div [
                prop.className "bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4 p-6"
                prop.onClick (fun e -> e.stopPropagation())
                prop.children [
                    Html.div [
                        prop.className "flex items-center justify-between mb-5"
                        prop.children [
                            Html.h2 [
                                prop.className "text-lg font-semibold text-gray-900"
                                prop.text "Add MCP Server"
                            ]
                            Html.button [
                                prop.className "text-gray-400 hover:text-gray-600 text-xl leading-none"
                                prop.onClick (fun _ -> onClose())
                                prop.text "×"
                            ]
                        ]
                    ]

                    Html.form [
                        prop.onSubmit handleSubmit
                        prop.children [
                            Html.div [
                                prop.className "mb-4"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-gray-700 mb-1"
                                        prop.text "Command line"
                                    ]
                                    Html.input [
                                        prop.className "w-full border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        prop.placeholder "dotnet run --project /path/to/Server.fsproj"
                                        prop.value commandLine
                                        prop.onChange setCommandLine
                                        prop.autoFocus true
                                    ]
                                ]
                            ]

                            Html.div [
                                prop.className "mb-5"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-gray-700 mb-1"
                                        prop.text "Friendly name (optional)"
                                    ]
                                    Html.input [
                                        prop.className "w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                                        prop.placeholder "Auto-detected from command"
                                        prop.value friendlyName
                                        prop.onChange setFriendlyName
                                    ]
                                ]
                            ]

                            match error with
                            | Some msg ->
                                Html.p [
                                    prop.className "text-red-600 text-sm mb-4"
                                    prop.text msg
                                ]
                            | None -> ()

                            Html.div [
                                prop.className "flex gap-3 justify-end"
                                prop.children [
                                    Html.button [
                                        prop.type' "button"
                                        prop.className "px-4 py-2 text-sm text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg"
                                        prop.onClick (fun _ -> onClose())
                                        prop.text "Cancel"
                                    ]
                                    Html.button [
                                        prop.type' "submit"
                                        prop.disabled loading
                                        prop.className "px-4 py-2 text-sm text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 rounded-lg"
                                        prop.text (if loading then "Loading…" else "Add Server")
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
