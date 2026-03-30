module McpRouter.UpstreamServerManager

open System.Collections.Concurrent
open ModelContextProtocol.Client

type UpstreamEntry = {
    Client: McpClient
    ToolNames: string list
    CommandLine: string
}

type UpstreamServerManager() =
    let servers = ConcurrentDictionary<string, UpstreamEntry>()

    member _.TryRemove(name: string) =
        match servers.TryRemove name with
        | true, entry -> Some entry
        | _ -> None

    member _.TryGet(name: string) =
        match servers.TryGetValue name with
        | true, entry -> Some entry
        | _ -> None

    member _.Add(name: string, entry: UpstreamEntry) =
        servers.[name] <- entry

    member _.GetAll() =
        servers |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList
