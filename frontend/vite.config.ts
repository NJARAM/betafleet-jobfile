import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In dev, /api is proxied to the API container (published on 8092 by docker compose).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5181,
    proxy: { '/api': process.env.API_URL ?? 'http://localhost:8092' }
  }
});
