/**
 * A konkurencia-token kinyerése kérés előtt.
 *
 * Miért kell külön függvény: a hívási helyeken korábban `task.rowVersion ?? 0` szerepelt,
 * mert a típus nullable volt. A 0-t viszont a szerveroldali validátor elutasítja
 * ("Érvénytelen RowVersion!"), tehát egy kliensoldali hiányból a felhasználó egy
 * értelmezhetetlen 400-as hibát kapott - nem azt, hogy mi a baj.
 *
 * Itt a hiányzó token AZONNAL, a kérés elindítása ELŐTT bukik el, olvasható üzenettel.
 */
export function requireRowVersion(value: number | null | undefined, mi: string): number {
    if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
        throw new Error(
            `Hiányzó verzióadat (${mi}). Töltsd újra az oldalt, és próbáld újra!`
        );
    }
    return value;
}
