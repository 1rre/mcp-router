module Components.ServerCard

open Feliz
open Types

[<ReactComponent>]
let ServerCard
    (server: ServerInfo)
    (onReload: string -> unit)
    (onRemove: string -> unit)
    (onToolToggle: string -> string -> bool -> unit)
    =
    let toolsExpanded, setToolsExpanded = React.useState true

    Html.div [
        prop.className "bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden"
        prop.children [
            // Header
            Html.div [
                prop.className "px-5 py-4 flex items-start justify-between gap-3"
                prop.children [
                    Html.div [
                        prop.className "min-w-0"
                        prop.children [
                            Html.h3 [
                                prop.className "text-base font-semibold text-gray-900 truncate"
                                prop.text server.name
                            ]
                            Html.p [
                                prop.className "text-xs text-gray-400 font-mono truncate mt-0.5"
                                prop.title server.commandLine
                                prop.text server.commandLine
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "flex gap-2 shrink-0"
                        prop.children [
                            Html.button [
                                prop.className "px-3 py-1.5 text-xs font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-lg"
                                prop.onClick (fun _ -> onReload server.name)
                                prop.text "Reload"
                            ]
                            Html.button [
                                prop.className "px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 hover:bg-red-100 rounded-lg"
                                prop.onClick (fun _ -> onRemove server.name)
                                prop.text "Remove"
                            ]
                        ]
                    ]
                ]
            ]

            // Tools section
            Html.div [
                prop.className "border-t border-gray-100"
                prop.children [
                    Html.button [
                        prop.className "w-full px-5 py-2.5 flex items-center justify-between text-xs font-medium text-gray-500 hover:bg-gray-50"
                        prop.onClick (fun _ -> setToolsExpanded (not toolsExpanded))
                        prop.children [
                            Html.span [ prop.text $"Tools ({server.tools.Length})" ]
                            Html.span [ prop.text (if toolsExpanded then "▲" else "▼") ]
                        ]
                    ]

                    if toolsExpanded then
                        Html.div [
                            prop.className "divide-y divide-gray-50"
                            prop.children [
                                if server.tools.Length = 0 then
                                    Html.p [
                                        prop.className "px-5 py-3 text-xs text-gray-400 italic"
                                        prop.text "No tools registered"
                                    ]
                                else
                                    for tool in server.tools do
                                        Html.div [
                                            prop.key tool.name
                                            prop.className "px-5 py-3 flex items-center justify-between gap-3"
                                            prop.children [
                                                Html.div [
                                                    prop.className "min-w-0"
                                                    prop.children [
                                                        Html.p [
                                                            prop.className "text-sm font-mono text-gray-800 truncate"
                                                            prop.text tool.name
                                                        ]
                                                        if tool.description <> "" then
                                                            Html.p [
                                                                prop.className "text-xs text-gray-400 truncate mt-0.5"
                                                                prop.title tool.description
                                                                prop.text tool.description
                                                            ]
                                                    ]
                                                ]
                                                // Toggle switch
                                                Html.button [
                                                    prop.role "switch"
                                                    prop.ariaChecked tool.enabled
                                                    prop.className (
                                                        "relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full transition-colors " +
                                                        (if tool.enabled then "bg-blue-600" else "bg-gray-200"))
                                                    prop.onClick (fun _ -> onToolToggle server.name tool.name (not tool.enabled))
                                                    prop.children [
                                                        Html.span [
                                                            prop.className (
                                                                "block h-4 w-4 rounded-full bg-white shadow ring-0 transition-transform mt-0.5 " +
                                                                (if tool.enabled then "translate-x-4 ml-0.5" else "translate-x-0.5"))
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                            ]
                        ]
                ]
            ]
        ]
    ]
