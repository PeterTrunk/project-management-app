/**
 * Esemény-puffer a teljes állapot újratöltésének idejére.
 *
 * **Miért van.** Az újratöltés és a valós idejű értesítés KÉT KÜLÖN CSATORNA, és nincs
 * közöttük sorrendi garancia. A szerver a lekérdezést egy pillanatban hajtja végre, a válasz
 * viszont csak később ér a böngészőig - ha ebben az ablakban megérkezik egy esemény, a kezelő
 * alkalmazza, majd a KÉSŐBB beérkező, de RÉGEBBI pillanatkép felülírja. A módosítás ilyenkor
 * eltűnik a felületről, jelzés nélkül.
 *
 * A megoldás nem az, hogy a betöltés nyer, és nem is az, hogy az esemény nyer: a betöltés
 * alatt érkező eseményeket PUFFEREZZÜK, és a pillanatkép alkalmazása UTÁN játsszuk vissza -
 * vagyis pontosan abban a sorrendben, ahogy a szerver látta őket.
 *
 * **Miért tiszta modul.** Nincs SignalR-, store- és böngésző-függősége: a visszajátszandó
 * hívás egy thunk. Így a sorrend és a biztonsági szelep mockolás nélkül, a gyors tesztkörben
 * mérhető - ugyanaz a minta, mint a `gitLinks.ts`-nél és a store-kezelőknél.
 *
 * **Egyetlen közös sor kell, nem eseményenként egy.** Egy `TaskCreated` + `TaskMoved` pár
 * ugyanarra a feladatra külön sorokban felcserélődhetne.
 */

/** Egy pufferelt hívás. Az `event` csak diagnosztika; a `run` hordozza a kezelőt és az argumentumokat. */
interface BufferedCall {
    event: string;
    run: () => void;
}

export interface EventBufferOptions {
    /**
     * Ennyi pufferelt hívás után a puffer kényszerből kiürít.
     * Egy nagy löket ne tudjon korlátlan memóriát lefoglalni.
     */
    maxQueueLength?: number;

    /**
     * Ennyi ideig lehet a puffer aktív. A felső korlát azért KELL, mert az axios kliensnek
     * nincs beállított időkorlátja (`lib/api/client.ts`), tehát egy beakadt kérés ígérete
     * SOSEM rendeződik - a `finally`-ben álló `end()` nem futna le, és a puffer örökre nyitva
     * maradna. A kliens onnantól élő kapcsolattal, de EGYETLEN frissítés nélkül működne,
     * jelzés nélkül. Az a hibamód rosszabb, mint amit a puffer javít.
     */
    maxBufferingMs?: number;

    /** Beinjektálható óra - a tesztek enélkül valódi időre várnának. */
    now?: () => number;

    /** A kényszerű kiürítés ne legyen észrevétlen: a hívó naplózhatja. */
    onForceFlush?: (reason: 'timeout' | 'overflow', queued: number) => void;
}

const DEFAULT_MAX_QUEUE_LENGTH = 500;
const DEFAULT_MAX_BUFFERING_MS = 15_000;

export class SignalREventBuffer {
    private queue: BufferedCall[] = [];

    /**
     * Beágyazott betöltések száma, nem logikai zászló. A teljes újraszinkronizálás
     * (`resyncAfterReconnectAsync`) a saját hatókörén BELÜL hív további betöltéseket, tehát a
     * belső `end()` nem ürítheti ki a puffert a külső befejezése előtt.
     */
    private depth = 0;

    private startedAt = 0;

    private readonly maxQueueLength: number;
    private readonly maxBufferingMs: number;
    private readonly now: () => number;
    private readonly onForceFlush?: (reason: 'timeout' | 'overflow', queued: number) => void;

    constructor(options: EventBufferOptions = {}) {
        this.maxQueueLength = options.maxQueueLength ?? DEFAULT_MAX_QUEUE_LENGTH;
        this.maxBufferingMs = options.maxBufferingMs ?? DEFAULT_MAX_BUFFERING_MS;
        this.now = options.now ?? (() => Date.now());
        this.onForceFlush = options.onForceFlush;
    }

    get isBuffering() {
        return this.depth > 0;
    }

    get queueLength() {
        return this.queue.length;
    }

    begin() {
        if (this.depth === 0) this.startedAt = this.now();
        this.depth++;
    }

    end() {
        //Math.max, mert egy kényszerű kiürítés nullázza a mélységet - az azután megérkező
        //`end()` (a beakadt betöltésé, ha mégis befejeződik) ne vigye negatívba.
        this.depth = Math.max(0, this.depth - 1);
        if (this.depth === 0) this.flush();
    }

    /**
     * @returns `true`, ha a hívást sorba tettük; `false`, ha a hívónak azonnal futtatnia kell.
     */
    capture(event: string, run: () => void): boolean {
        if (this.depth === 0) return false;

        //A biztonsági szelep ITT van, és nem időzítőn. Ez a pont az, ahol a pufferelés kárt
        //okozhat: egy MOST érkező eseményt nyelnénk el. Ha a határt átlépte, inkább adjuk fel
        //a pufferelést - a visszakapott hibamód (lehetséges elavult felülírás) az, ami ma is
        //van, a másik viszont teljesen élettelen kliens.
        if (this.queue.length >= this.maxQueueLength) {
            this.forceFlush('overflow');
            return false;
        }

        if (this.now() - this.startedAt > this.maxBufferingMs) {
            this.forceFlush('timeout');
            return false;
        }

        this.queue.push({ event, run });
        return true;
    }

    /**
     * A sor visszajátszása ÉRKEZÉSI sorrendben.
     *
     * Egy kezelő hibája nem buktathatja el a többit: a visszajátszás ugyanazt a
     * "az értesítés ne vigye el a többit" elvet követi, mint a szerveroldali kiküldés.
     */
    flush() {
        if (this.queue.length === 0) return;

        //Előbb kiürítjük, és csak utána futtatunk: ha egy kezelő közben eseményt váltana ki,
        //az ne ugyanebbe a sorba kerüljön vissza.
        const pending = this.queue;
        this.queue = [];

        for (const call of pending) {
            try {
                call.run();
            } catch (e) {
                console.error(`SignalR esemény visszajátszási hiba (${call.event}):`, e);
            }
        }
    }

    private forceFlush(reason: 'timeout' | 'overflow') {
        const queued = this.queue.length;
        this.depth = 0;
        this.flush();
        this.onForceFlush?.(reason, queued);
    }
}
