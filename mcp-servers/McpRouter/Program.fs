module McpRouter.Program

open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.StaticFiles
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open ModelContextProtocol.Protocol
open McpDynamicTools.McpServerBuilderExtensions
open McpRouter.UpstreamServerManager
open McpRouter.ApiHandlers

let private log (msg: string) =
    eprintfn "[mcp-router] %s" msg

/// Wraps an F# task handler (HttpContext -> Task<unit>) as a RequestDelegate.
let private rd (handler: HttpContext -> System.Threading.Tasks.Task<unit>) : RequestDelegate =
    RequestDelegate(fun ctx -> handler ctx :> System.Threading.Tasks.Task)

[<EntryPoint>]
let main _ =
    log "Starting mcp-router..."

    let builder = WebApplication.CreateBuilder()

    builder.Services.AddSingleton<UpstreamServerManager>() |> ignore

    builder.Services
        .AddMcpServer(fun opts ->
            opts.Capabilities <- ServerCapabilities(
                Tools = ToolsCapability(ListChanged = System.Nullable true)))
        .WithHttpTransport()
        .WithDynamicTools()
        .WithToolsFromAssembly()
    |> ignore

    log "Services registered. Building host..."
    let app = builder.Build()

    // ── MCP endpoint ──────────────────────────────────────────────────────────
    app.MapMcp() |> ignore

    // ── REST API ──────────────────────────────────────────────────────────────
    let registry  = app.Services.GetRequiredService<McpDynamicTools.DynamicToolRegistry.DynamicToolRegistry>()
    let manager   = app.Services.GetRequiredService<UpstreamServerManager>()
    let notifier  = app.Services.GetRequiredService<ToolChangeNotifier>()

    let notifyChanged () : System.Threading.Tasks.Task =
        notifier.NotifyAll()

    app.MapGet("/api/servers", rd (getServers registry manager)) |> ignore

    app.MapPost("/api/servers", rd (addServer registry manager notifyChanged)) |> ignore

    app.MapDelete("/api/servers/{name}", rd (fun ctx ->
        let name = ctx.Request.RouteValues["name"] :?> string
        removeServer name registry manager notifyChanged ctx)) |> ignore

    app.MapPost("/api/servers/{name}/reload", rd (fun ctx ->
        let name = ctx.Request.RouteValues["name"] :?> string
        reloadServer name registry manager notifyChanged ctx)) |> ignore

    app.MapPatch("/api/servers/{name}/tools/{toolName}", rd (fun ctx ->
        let name     = ctx.Request.RouteValues["name"]     :?> string
        let toolName = ctx.Request.RouteValues["toolName"] :?> string
        setToolEnabled name toolName registry manager notifyChanged ctx)) |> ignore

    // ── Static files at /dash ─────────────────────────────────────────────────
    let wwwroot = Path.Combine(System.AppContext.BaseDirectory, "wwwroot", "dash")
    if Directory.Exists wwwroot then
        app.UseStaticFiles(
            StaticFileOptions(
                FileProvider = new PhysicalFileProvider(wwwroot),
                RequestPath  = PathString "/dash")) |> ignore

        app.MapFallback("/dash/{**path}", rd (fun ctx -> task {
            ctx.Response.ContentType <- "text/html"
            do! ctx.Response.SendFileAsync(Path.Combine(wwwroot, "index.html"))
        })) |> ignore

    log "Host built. Ready to accept connections."
    app.Run "http://localhost:6767"
    log "Shutting down."
    0
