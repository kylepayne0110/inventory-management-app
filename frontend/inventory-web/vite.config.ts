import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The dev server forwards /api requests to the local .NET API, so the browser only talks to one origin and no CORS setup is needed.
const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5127'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/api': apiTarget, '/health': apiTarget },
  },
})
