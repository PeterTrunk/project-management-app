/// <reference types="vite/client" />

/**
 * A build időben beépülő környezeti változók.
 *
 * FONTOS: a `vite/client` saját ImportMetaEnv definíciójában van egy
 * `[key: string]: any` index-szignatúra, ami elnyeli a NEM deklarált neveket. Ezért egy
 * elgépelt változónév csendben `undefined` lenne, nem fordítási hiba - a típusvédelem
 * tehát pontosan addig ér, ameddig ez a lista naprakész.
 *
 * Ha új VITE_* változó kerül a kódba, ide is fel kell venni.
 * A megfelelő párjuk: `.env.example`, `frontend/Dockerfile` (ARG + ENV) és
 * `docker-compose.prod.yml` (build args).
 */
interface ImportMetaEnv {
    // Az API alapcíme. Hiányában a kód a localhost:5178-ra esik vissza.
    readonly VITE_API_URL: string;

    // Bekapcsolja a SignalR kézi keepalive-ot ("true" esetén).
    readonly VITE_SIGNALR_KEEPALIVE_ENABLED: string;

    // A keepalive periódusa másodpercben. A kód 5 és 30 közé szorítja.
    readonly VITE_SIGNALR_KEEPALIVE_SECONDS: string;

    // A hozzáférési token élettartama percben, a lejárat kijelzéséhez.
    readonly VITE_JWT_ACCESS_TOKEN_LIFETIME: string;

    //A jogi dokumentumokban megjelenő adatkezelői adatok. Nem titkok: az adatkezelő
    //azonosíthatósága jogszabályi követelmény. Üresen hagyva a dokumentumokban feltűnő
    //"[KITÖLTENDŐ: ...]" helyőrző látszik - lásd lib/legal.ts.

    // Az adatkezelő neve.
    readonly VITE_LEGAL_CONTROLLER_NAME: string;

    // Az adatkezelő levelezési címe.
    readonly VITE_LEGAL_CONTROLLER_ADDRESS: string;

    // Kapcsolattartási e-mail cím az érintetti kérésekhez.
    readonly VITE_LEGAL_CONTACT_EMAIL: string;

    // A tárhelyszolgáltató neve és címe, az adatfeldolgozók táblájához.
    readonly VITE_LEGAL_HOSTING_PROVIDER: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
