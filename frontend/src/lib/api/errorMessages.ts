/**
 * A hálózati és API-hibák emberi nyelvre fordítása.
 *
 * Külön modulban, és nem a `client.ts`-ben: az import időben `axios.create()`-et hív, tehát
 * onnan ezek a tiszta függvények nem tesztelhetők. Itt viszont igen, és pontosan ez a cél -
 * a "[object Object]" visszatérését csak teszt tudja megakadályozni.
 */
//A hívók a `e.response?.data ?? e.message` mintát használják. Ha a kérés el sem jutott a szerverig,
//az axios nyers technikai szövege ("Network Error", "timeout of 0ms exceeded") kerülne a felhasználó elé.
//Ezért itt cseréljük felhasználóbarátabb üzenetre.
export function friendlyMessageFor(error: any): string | null {
    //A megszakított kérés nem hiba, nem is kell róla üzenet
    if (error.code === 'ERR_CANCELED') {
        return null;
    }

    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
        return 'A szerver nem válaszolt időben, próbáld újra!';
    }

    if (!error.response) {
        return 'Nem sikerült elérni a szervert! Ellenőrizd az internetkapcsolatod, vagy próbáld újra pár másodperc múlva.';
    }

    const status = error.response.status;
    if (status === 502 || status === 503 || status === 504) {
        return 'A szerver éppen nem elérhető, próbáld újra pár másodperc múlva!';
    }

    return null;
}

//A backend hibavalasza {"error": "..."} alaku JSON, de NEM minden hiba innen jon: az
//ASP.NET automatikus modell-validacioja ValidationProblemDetails alakot ad (type, title,
//status, errors), a reverse proxy HTML-t, egy halozati hiba pedig semmit.
//
//A hivok mind a `e.response?.data ?? e.message` mintat hasznaljak (92 helyen), tehat ha itt
//objektum marad a data-ban, a felhasznalo "[object Object]"-et lat. Ez meg is tortent: egy
//400-as validacios valasz pontosan igy jelent meg. Ezert ez a fuggveny MINDIG stringet ad.
export function normalizeResponseData(data: unknown): string | null {
    if (data === null || data === undefined) return null;

    if (typeof data === 'string') {
        //Nem a mi API-nktol jott (pl. proxy HTML hibaoldala)
        if (data.trimStart().startsWith('<')) return null;
        return data;
    }

    if (typeof data !== 'object') return String(data);

    const obj = data as Record<string, unknown>;

    //A sajat alakunk
    if (typeof obj.error === 'string') return obj.error;

    //ASP.NET ValidationProblemDetails: { errors: { Mezo: ["uzenet", ...] } }
    //Az elso uzenet eleg - a mezonevet is kiirjuk, mert kulonben nem derul ki, mi a baj.
    if (obj.errors && typeof obj.errors === 'object') {
        const entries = Object.entries(obj.errors as Record<string, unknown>);
        const messages = entries.flatMap(([field, value]) => {
            const list = Array.isArray(value) ? value : [value];
            return list.filter((m): m is string => typeof m === 'string')
                       .map(m => (field && field !== '$' ? `${field}: ${m}` : m));
        });
        if (messages.length > 0) return messages.join(' ');
    }

    //ProblemDetails
    if (typeof obj.detail === 'string') return obj.detail;
    if (typeof obj.title === 'string') return obj.title;
    if (typeof obj.message === 'string') return obj.message;

    //Ismeretlen alak: a reszlet a konzolba megy, a felhasznalo olvashato szoveget kap.
    return null;
}
