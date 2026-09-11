import { defineConfig, mergeConfig } from 'vitest/config';
import viteConfig from './vite.config.js';

/*
 * Külön fájl, nem a vite.config.js bővítése.
 *
 * Így a `vite build` viselkedése bizonyíthatóan érintetlen marad: 
 * A build konfigurációjához egyetlen sort sem nyúltunk, a teszt-beállítások pedig csak erre a fájlra tartoznak.
 * A mergeConfig gondoskodik arról, hogy a tesztek ugyanazt a plugin és feloldási beállítást lássák,
 * mint a build - így egy import nem viselkedhet másképp a két helyen.
 */
export default mergeConfig(
    viteConfig,
    defineConfig({
        test: {
            //Nem kell jsdom: a tesztelt store-ok és segédfüggvények nem nyúlnak window-hoz, document-hez vagy localStorage-hoz.
            //Az api/* importjaik kizárólag `import type` alakúak, amiket a fordító töröl - tehát az axios sem kerül be a futtatásba.
            environment: 'node',

            //A tesztfájlok a vizsgált kód MELLETT élnek (pl. lib/stores/taskStore.test.ts).
            //Így a tsconfig `src/**/*.ts` include-ja miatt a `npm run check` őket is típusellenőrzi - egy elgépelt payload mező már ott kiderül.
            include: ['src/**/*.test.ts']
        }
    })
);
