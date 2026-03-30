module McpDynamicTools.DynamicToolRegistry

open System.Collections.Concurrent
open System.Text.Json
open System.Threading.Tasks

/// A tool registered dynamically at runtime.
type PreparedTool = {
    Name        : string
    Description : string
    /// JSON Schema string for the tool's input arguments
    InputSchema : string
    IsEnabled   : bool
    /// Executes the tool. Receives the raw arguments JsonElement (an object).
    Handler     : JsonElement -> Task<string>
}

/// Thread-safe registry of dynamically registered tools, grouped by owner (server name).
type DynamicToolRegistry() =
    let owners = ConcurrentDictionary<string, ConcurrentDictionary<string, PreparedTool>>()

    member _.Register (owner: string) (tool: PreparedTool) =
        let bucket = owners.GetOrAdd(owner, fun _ -> ConcurrentDictionary<string, PreparedTool>())
        bucket.[tool.Name] <- tool

    /// Returns only enabled tools across all owners (used for MCP tools/list).
    member _.GetAll() =
        owners.Values
        |> Seq.collect (fun bucket -> bucket.Values)
        |> Seq.filter (fun t -> t.IsEnabled)
        |> Seq.toList

    /// Returns all tools for a specific owner regardless of enabled state (used for API).
    member _.GetAllForOwner(owner: string) =
        match owners.TryGetValue owner with
        | true, bucket -> bucket.Values |> Seq.toList
        | _            -> []

    member _.TryFind(name: string) =
        owners.Values
        |> Seq.tryPick (fun bucket ->
            match bucket.TryGetValue name with
            | true, t -> Some t
            | _       -> None)

    member _.RemoveOwner(owner: string) =
        owners.TryRemove owner |> ignore

    member _.SetEnabled(owner: string) (toolName: string) (enabled: bool) =
        match owners.TryGetValue owner with
        | true, bucket ->
            match bucket.TryGetValue toolName with
            | true, t -> bucket.[toolName] <- { t with IsEnabled = enabled }
            | _       -> ()
        | _ -> ()
