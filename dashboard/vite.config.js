import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/dash/',
  build: {
    outDir: '../mcp-servers/McpRouter/wwwroot/dash',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:6767',
    },
  },
})
