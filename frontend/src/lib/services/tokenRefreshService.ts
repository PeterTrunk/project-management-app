import { tokenStore } from '../stores/tokenStore';
import { refreshTokenOnce } from '../api/tokenRefresh';
import { nextRefreshDelayMs } from './tokenSchedule';

let refreshTimer: ReturnType<typeof setTimeout> | null = null;

//Végszükség: csak akkor számít, ha a tokenből nem olvasható ki a lejárat - vagyis ha
//nincs is használható tokenünk. Szándékosan a RÖVIDEBB érték, a backend saját
//alapértékével egyezően: korán megújítani ártalmatlan, későn nem az.
const FALLBACK_LIFETIME_MINUTES = 15;

function fallbackLifetimeMinutes(): number {
    //A .env konvenciója az ÜRES érték, amit a ?? nem fog meg - a parseInt NaN-t ad rá.
    const configured = parseInt(import.meta.env.VITE_JWT_ACCESS_TOKEN_LIFETIME ?? '');

    return Number.isFinite(configured) && configured > 0 ? configured : FALLBACK_LIFETIME_MINUTES;
}

export function scheduleTokenRefresh() {
    if (refreshTimer) clearTimeout(refreshTimer);

    //A lejárat a token saját `exp` mezőjéből jön, nem egy beégetett élettartamból:
    //így a szerveroldali JWT_EXPIRY_MINUTES bármikor átállítható anélkül, hogy a
    //frontendet újra kellene építeni. Lásd tokenSchedule.ts.
    const refreshInMs = nextRefreshDelayMs(
        tokenStore.get(),
        fallbackLifetimeMinutes(),
        Date.now()
    );

    refreshTimer = setTimeout(async () => {
        try {
            await refreshTokenOnce();
            scheduleTokenRefresh();
        } catch {
            tokenStore.clear();
            window.location.href = '/#/';
        }
    }, refreshInMs);
}

export function cancelTokenRefresh() {
    if (refreshTimer) {
        clearTimeout(refreshTimer);
        refreshTimer = null;
    }
}
