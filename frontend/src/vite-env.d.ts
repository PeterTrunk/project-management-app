/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly VITE_API_URL: string;
    readonly VITE_SIGNALR_KEEPALIVE_ENABLED: string;
    readonly VITE_SIGNALR_KEEPALIVE_SECONDS: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}