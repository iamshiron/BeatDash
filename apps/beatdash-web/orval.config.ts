import { defineConfig } from 'orval';

export default defineConfig({
    beatdash: {
        output: {
            mode: 'tags-split',
            target: 'src/api/beatdash.ts',
            schemas: 'src/api/model',
            client: 'react-query',
            mock: true,
            httpClient: 'axios',
            biome: true
        },
        input: {
            target: 'http://127.0.0.1:1811/openapi/v1.json',
        },
    },
});
