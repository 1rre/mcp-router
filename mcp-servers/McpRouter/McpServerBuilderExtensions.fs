module McpDynamicTools.McpServerBuilderExtensions

open System.Runtime.CompilerServices
open System.Collections.Concurrent
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open ModelContextProtocol.Server
open ModelContextProtocol.Protocol

/// Broadcasts tools/list_changed to all currently active MCP sessions.
/// Sessions are registered lazily on their first tools/list request and removed
/// when a notification send fails (i.e. the connection is gone).
type ToolChangeNotifier() =
    let sessions = ConcurrentDictionary<McpServer, unit>()

    member _.Track(server: McpServer) =
        sessions.TryAdd(server, ()) |> ignore

    member _.NotifyAll() : Task = task {
        for kvp in sessions do
            try
                do! kvp.Key.SendNotificationAsync("notifications/tools/list_changed", CancellationToken.None)
            with _ ->
                sessions.TryRemove kvp.Key |> ignore
    }

/// Converts a PreparedTool into a protocol Tool descriptor.
let private toToolDescriptor (t: DynamicToolRegistry.PreparedTool) =
    Tool(
        Name        = t.Name,
        Description = t.Description,
        InputSchema = JsonSerializer.Deserialize<JsonElement> t.InputSchema)

[<Extension>]
type McpServerBuilderExtensions private () =

    /// Registers a DynamicToolRegistry and ToolChangeNotifier as singletons, and wires up
    /// list/call filters so that dynamically registered tools appear in tools/list and
    /// are dispatched by tools/call.
    [<Extension>]
    static member WithDynamicTools(builder: IMcpServerBuilder) : IMcpServerBuilder =
        let registry = DynamicToolRegistry.DynamicToolRegistry()
        let notifier = ToolChangeNotifier()

        builder.Services.AddSingleton<DynamicToolRegistry.DynamicToolRegistry> registry |> ignore
        builder.Services.AddSingleton<ToolChangeNotifier> notifier |> ignore

        builder.WithRequestFilters(fun fb ->

            fb.AddListToolsFilter(
                McpRequestFilter<ListToolsRequestParams, ListToolsResult>(fun next ->
                    McpRequestHandler<ListToolsRequestParams, ListToolsResult>(fun ctx ct ->
                        ValueTask<ListToolsResult>(task {
                            // Register this session so we can notify it later
                            notifier.Track ctx.Server
                            let! result = next.Invoke(ctx, ct)
                            let extra = registry.GetAll() |> List.map toToolDescriptor
                            let merged = System.Collections.Generic.List result.Tools
                            merged.AddRange extra
                            return ListToolsResult(Tools = merged)
                        })))) |> ignore

            fb.AddCallToolFilter(
                McpRequestFilter<CallToolRequestParams, CallToolResult>(fun next ->
                    McpRequestHandler<CallToolRequestParams, CallToolResult>(fun ctx ct ->
                        ValueTask<CallToolResult>(task {
                            match registry.TryFind ctx.Params.Name with
                            | Some tool ->
                                let args = JsonSerializer.SerializeToElement ctx.Params.Arguments
                                let! text = tool.Handler args
                                return CallToolResult(Content = [| TextContentBlock(Text = text) :> ContentBlock |])
                            | None ->
                                return! next.Invoke(ctx, ct)
                        })))) |> ignore

        ) |> ignore

        builder
