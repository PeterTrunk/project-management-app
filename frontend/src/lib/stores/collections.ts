/**
 * Listaműveletek a tárolók eseménykezelőihez.
 *
 * **Miért van külön modulban.** Ugyanaz a szabály hét kezelőben, öt tárolóban kell - hét
 * másolat helyett egy hely, amit tesztelni is egyszer kell.
 */

/**
 * Felvétel, ha még nem szerepel; egyébként a lista VÁLTOZATLAN.
 *
 * **Miért kell egyáltalán.** A létrehozási események kezelői vakon fűztek hozzá. Ez addig
 * működött, amíg egy esemény pontosan egyszer érkezett meg - a teljes újratöltés alatti
 * pufferelés (`signalREventBuffer`) viszont a pillanatkép UTÁN játssza vissza az eseményeket,
 * tehát egy olyan elem létrehozása is visszajátszódik, amely a pillanatképben MÁR benne volt.
 * Gárda nélkül ez nem esetleges, hanem biztos duplikáció - ráadásul azonos kulccsal, amitől a
 * Svelte kulcsolt `each` blokkja hibát dob.
 *
 * **Miért kihagyás, és nem felülírás.** Egy létrehozási esemény payloadja soha nem FRISSEBB
 * annál, ami már a tárolóban van, viszont lehet SZEGÉNYEBB: a `TaskCreated` például
 * helykitöltőket hordoz (`description: null`, `status: ''`, `assigneeIds: []`,
 * `createdByName: ''`), a pillanatkép sora pedig a valódi értékeket. A felülírás tehát adatot
 * veszítene.
 *
 * Ez szándékosan MÁS, mint a `taskStore` git-hivatkozásokra használt `upsertById`-ja: egy
 * pull request sora az állapotváltozásaival együtt újra és újra megérkezik, ott tehát a
 * felülírás a helyes művelet.
 */
export function insertIfAbsentBy<T>(items: T[], incoming: T, key: (item: T) => string): T[] {
    const incomingKey = key(incoming);
    if (items.some(item => key(item) === incomingKey)) return items;

    return [...items, incoming];
}
