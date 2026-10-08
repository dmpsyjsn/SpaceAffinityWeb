import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    // Forward /api calls to the .NET API, mirroring what Traefik does in production
    proxy: {
      '/api': 'http://localhost:5247',
    },
  },
})