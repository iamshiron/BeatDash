import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { TanStackRouterVite } from '@tanstack/router-vite-plugin';
import path from 'path';

export default defineConfig({
    envDir: '../../',
    plugins: [TanStackRouterVite({ autoCodeSplitting: true }), tailwindcss(), react()],
    resolve: {
        alias: {
            '@': path.resolve(import.meta.dirname, './src'),
            '@shiron/ui': path.resolve(import.meta.dirname, '../../packages/ui/src'),
        },
    },
    server: {
        port: 1911,
        proxy: {
            '/api': {
                target: 'http://localhost:1811',
                changeOrigin: true,
                ws: true,
            },
        },
    },
});
