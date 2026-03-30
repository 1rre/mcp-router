module Types

type ToolInfo = {
    name: string
    description: string
    enabled: bool
}

type ServerInfo = {
    name: string
    commandLine: string
    tools: ToolInfo array
}
