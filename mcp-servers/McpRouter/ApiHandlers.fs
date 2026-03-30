module McpRouter.ApiHandlers

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading
open System.Collections.Generic
open Microsoft.AspNetCore.Http
open ModelContextProtocol.Client
open ModelContextProtocol.Protocol
open McpDynamicTools.DynamicToolRegistry
open McpRouter.UpstreamServerManager

let private log (msg: string) =
    eprintfn "[mcp-router] %s" msg

// ── JSON helpers ─────────────────────────────────────────────────────────────

let private jsonOptions =
    let opts = JsonSerializerOptions()
    opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    opts.DefaultIgnoreCondition <- JsonIgnoreCondition.WhenWritingNull
    opts

let private writeJson (ctx: HttpContext) statusCode value = task {
    ctx.Response.ContentType <- "application/json"
    ctx.Response.StatusCode <- statusCode
    do! ctx.Response.WriteAsync(JsonSerializer.Serialize(value, jsonOptions))
}

// ── Name generation ──────────────────────────────────────────────────────────

let private deriveName (commandLine: string) =
    let parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let projectIdx =
        parts
        |> Array.tryFindIndex (fun p -> p.Equals("--project", StringComparison.OrdinalIgnoreCase))
    match projectIdx with
    | Some i when i + 1 < parts.Length ->
        Path.GetFileNameWithoutExtension parts.[i + 1]
    | _ ->
        parts
        |> Array.filter (fun p -> not (p.StartsWith "-"))
        |> Array.tryLast
        |> Option.map Path.GetFileNameWithoutExtension
        |> Option.defaultValue "server"

// ── Response DTOs ─────────────────────────────────────────────────────────────

type ToolDto = {
    Name: string
    Description: string
    Enabled: bool
}

type ServerDto = {
    Name: string
    CommandLine: string
    Tools: ToolDto list
}

let private toDto (name: string) (entry: UpstreamEntry) (registry: DynamicToolRegistry) =
    let tools =
        registry.GetAllForOwner name
        |> List.map (fun t -> { Name = t.Name; Description = t.Description; Enabled = t.IsEnabled })
    { Name = name; CommandLine = entry.CommandLine; Tools = tools }

// ── Core load/teardown logic ─────────────────────────────────────────────────

let private teardown (name: string) (registry: DynamicToolRegistry) (manager: UpstreamServerManager) = task {
    match manager.TryRemove name with
    | Some entry ->
        log $"  Tearing down '{name}' ({entry.ToolNames.Length} tool(s))"
        registry.RemoveOwner name
        try do! (entry.Client :> IAsyncDisposable).DisposeAsync()
        with ex -> log $"  Warning: dispose error: {ex.Message}"
    | None -> ()
}

let private loadServer
    (commandLine: string)
    (name: string)
    (registry: DynamicToolRegistry)
    (manager: UpstreamServerManager)
    (notifyChanged: unit -> Tasks.Task)
    = task {
    log $"=== loadServer: name='{name}', command='{commandLine}' ==="
    do! teardown name registry manager

    let parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
    let command = parts.[0]
    let args = parts.[1..]

    let transport = new StdioClientTransport(StdioClientTransportOptions(Command = command, Arguments = args))
    let! client = McpClient.CreateAsync(transport, cancellationToken = CancellationToken.None)
    let! tools = client.ListToolsAsync()
    let toolNames = ResizeArray<string>()

    for tool in tools do
        let schemaStr = JsonSerializer.Serialize tool.ProtocolTool.InputSchema
        let preparedTool = {
            Name        = tool.Name
            Description = if isNull tool.Description then "" else tool.Description
            InputSchema = schemaStr
            IsEnabled   = true
            Handler     = fun argsEl -> task {
                let argDict =
                    if argsEl.ValueKind = JsonValueKind.Object then
                        argsEl.EnumerateObject()
                        |> Seq.map (fun p -> p.Name, p.Value :> obj)
                        |> dict
                        |> Dictionary<string, obj>
                        :> IReadOnlyDictionary<string, obj>
                    else
                        Dictionary<string, obj>() :> _
                let! result = client.CallToolAsync(tool.Name, argDict, null, null, CancellationToken.None)
                return
                    result.Content
                    |> Seq.choose (fun c ->
                        match c with
                        | :? TextContentBlock as t -> Some t.Text
                        | _ -> None)
                    |> String.concat "\n"
            }
        }
        registry.Register name preparedTool
        toolNames.Add tool.Name

    manager.Add(name, { Client = client; ToolNames = toolNames |> Seq.toList; CommandLine = commandLine })
    do! notifyChanged()
    log $"=== '{name}' loaded: {toolNames.Count} tool(s) ==="
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type AddServerRequest = {
    CommandLine: string
    FriendlyName: string
}

[<CLIMutable>]
type SetToolEnabledRequest = {
    Enabled: bool
}

// ── HTTP Handlers ─────────────────────────────────────────────────────────────

let getServers (registry: DynamicToolRegistry) (manager: UpstreamServerManager) (ctx: HttpContext) = task {
    let dtos = manager.GetAll() |> List.map (fun (n, e) -> toDto n e registry)
    do! writeJson ctx 200 dtos
}

let addServer
    (registry: DynamicToolRegistry)
    (manager: UpstreamServerManager)
    (notifyChanged: unit -> Tasks.Task)
    (ctx: HttpContext)
    = task {
    let! req = ctx.Request.ReadFromJsonAsync<AddServerRequest>()
    if String.IsNullOrWhiteSpace req.CommandLine then
        ctx.Response.StatusCode <- 400
        do! ctx.Response.WriteAsync "commandLine is required"
    else
        let name =
            if String.IsNullOrWhiteSpace req.FriendlyName then deriveName req.CommandLine
            else req.FriendlyName.Trim()
        do! loadServer req.CommandLine name registry manager notifyChanged
        let entry = manager.TryGet name |> Option.get
        do! writeJson ctx 201 (toDto name entry registry)
}

let removeServer
    (name: string)
    (registry: DynamicToolRegistry)
    (manager: UpstreamServerManager)
    (notifyChanged: unit -> Tasks.Task)
    (ctx: HttpContext)
    = task {
    match manager.TryGet name with
    | None ->
        ctx.Response.StatusCode <- 404
        do! ctx.Response.WriteAsync $"Server '{name}' not found"
    | Some _ ->
        do! teardown name registry manager
        do! notifyChanged()
        ctx.Response.StatusCode <- 204
}

let reloadServer
    (name: string)
    (registry: DynamicToolRegistry)
    (manager: UpstreamServerManager)
    (notifyChanged: unit -> Tasks.Task)
    (ctx: HttpContext)
    = task {
    match manager.TryGet name with
    | None ->
        ctx.Response.StatusCode <- 404
        do! ctx.Response.WriteAsync $"Server '{name}' not found"
    | Some entry ->
        do! loadServer entry.CommandLine name registry manager notifyChanged
        let updated = manager.TryGet name |> Option.get
        do! writeJson ctx 200 (toDto name updated registry)
}

let setToolEnabled
    (serverName: string)
    (toolName: string)
    (registry: DynamicToolRegistry)
    (manager: UpstreamServerManager)
    (notifyChanged: unit -> Tasks.Task)
    (ctx: HttpContext)
    = task {
    match manager.TryGet serverName with
    | None ->
        ctx.Response.StatusCode <- 404
        do! ctx.Response.WriteAsync $"Server '{serverName}' not found"
    | Some entry ->
        if not (entry.ToolNames |> List.contains toolName) then
            ctx.Response.StatusCode <- 404
            do! ctx.Response.WriteAsync $"Tool '{toolName}' not found on server '{serverName}'"
        else
            let! req = ctx.Request.ReadFromJsonAsync<SetToolEnabledRequest>()
            registry.SetEnabled serverName toolName req.Enabled
            do! notifyChanged()
            ctx.Response.StatusCode <- 204
}
