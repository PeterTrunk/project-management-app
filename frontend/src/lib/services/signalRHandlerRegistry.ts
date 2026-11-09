/**
 * A kezelők és a pufferelő burkolóik nyilvántartása.
 *
 * **Miért van egyáltalán burkoló.** A teljes újratöltés alatt érkező eseményeket pufferelni
 * kell (lásd `signalREventBuffer`), ezért a SignalR-be nem a hívó kezelője kerül, hanem egy
 * burkoló, amely a hívást sorba teheti.
 *
 * **Miért kell nyilvántartás.** A leválasztás a hívó EREDETI hivatkozását kapja, a SignalR-ben
 * viszont a burkoló van regisztrálva - a kettő összekötése nélkül a leválasztás néma no-op
 * lenne, és a komponensek kilépés után is megkapnák az eseményeket. Ez a hibaosztály már
 * egyszer megtörtént ebben a kódbázisban (a `callback` nélküli `off()` a központi kezelőt is
 * levette), ezért kap külön, tesztelhető modult.
 *
 * **Miért eseménynév szerint tagolva.** Ugyanaz a kezelő több eseményre is feliratkozhat;
 * egyetlen lapos leképezésben a második regisztráció felülírná az első burkolóját, és az
 * elsőről nem lehetne leválasztani.
 */

type Handler = (...args: any[]) => void;

export class SignalRHandlerRegistry {
    private readonly byEvent = new Map<string, Map<Handler, Handler>>();

    remember(event: string, callback: Handler, wrapper: Handler) {
        let perEvent = this.byEvent.get(event);
        if (!perEvent) {
            perEvent = new Map();
            this.byEvent.set(event, perEvent);
        }
        perEvent.set(callback, wrapper);
    }

    /**
     * Amit a leválasztásnak át KELL adnia a SignalR-nek.
     *
     * Visszaesés a kapott kezelőre, ha nincs burkolója: így egy közvetlenül (a szolgáltatás
     * megkerülésével) regisztrált kezelő leválasztása sem válik némán hatástalanná.
     */
    unsubscribeTarget(event: string, callback: Handler): Handler {
        return this.byEvent.get(event)?.get(callback) ?? callback;
    }

    forget(event: string, callback: Handler) {
        this.byEvent.get(event)?.delete(callback);
    }

    forgetEvent(event: string) {
        this.byEvent.delete(event);
    }

    /**
     * A regisztrációk a SignalR kapcsolattal együtt elvesznek, tehát a leképezés is
     * érvénytelen. Enélkül egy újracsatlakozás után a régi kapcsolat burkolóit tartanánk.
     */
    clear() {
        this.byEvent.clear();
    }
}
