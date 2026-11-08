import { describe, it, expect, vi } from 'vitest';
import { SignalREventBuffer } from './signalREventBuffer';

/**
 * Az esemény-puffer tesztjei.
 *
 * A LÉNYEGI ÁLLÍTÁS az utolsó blokkban van: a pillanatkép alkalmazása után a visszajátszás
 * ÉRKEZÉSI sorrendben fut, tehát a betöltés alatt érkezett módosítás nem veszik el. A javítás
 * előtt ez a forgatókönyv némán elvesztett egy módosítást.
 */
describe('SignalREventBuffer', () => {
    /** A valódi hívást helyettesítő napló: amit a kezelő tenne, azt listába írjuk. */
    function recorder() {
        const calls: string[] = [];
        return { calls, push: (name: string) => () => calls.push(name) };
    }

    describe('pufferelés nélkül', () => {
        it('nem kapja el a hívást, tehát a hívó azonnal futtat', () => {
            const buffer = new SignalREventBuffer();
            const { push } = recorder();

            expect(buffer.capture('TaskMoved', push('a'))).toBe(false);
            expect(buffer.isBuffering).toBe(false);
        });
    });

    describe('pufferelés közben', () => {
        it('elkapja a hívást, és NEM futtatja le', () => {
            const buffer = new SignalREventBuffer();
            const { calls, push } = recorder();

            buffer.begin();
            expect(buffer.capture('TaskMoved', push('a'))).toBe(true);

            //Ez a lényeg: a kezelő még nem futott le, tehát nem írt a tárolóba olyat,
            //amit a pillanatkép felülírhatna.
            expect(calls).toEqual([]);
            expect(buffer.queueLength).toBe(1);
        });

        it('a kiürítés ÉRKEZÉSI sorrendben játssza vissza', () => {
            //Egyetlen közös sor kell, nem eseményenként egy: ugyanarra a feladatra egy
            //létrehozás + mozgatás pár felcserélődve értelmetlen állapotot adna.
            const buffer = new SignalREventBuffer();
            const { calls, push } = recorder();

            buffer.begin();
            buffer.capture('TaskCreated', push('created'));
            buffer.capture('TaskMoved', push('moved'));
            buffer.capture('TaskUpdated', push('updated'));
            buffer.end();

            expect(calls).toEqual(['created', 'moved', 'updated']);
        });

        it('a kiürítés után már nem pufferel', () => {
            const buffer = new SignalREventBuffer();
            const { calls, push } = recorder();

            buffer.begin();
            buffer.end();

            expect(buffer.capture('TaskMoved', push('a'))).toBe(false);
            expect(calls).toEqual([]);
        });

        it('egy kezelő hibája nem buktatja el a többit', () => {
            //Ugyanaz az elv, mint a szerveroldali kiküldésnél: az értesítés ne vigye el a
            //többit. Enélkül egyetlen hibás payload az összes utána következő eseményt
            //elnyelné - és pont egy újracsatlakozás után, amikor a legtöbb jön.
            const buffer = new SignalREventBuffer();
            const { calls, push } = recorder();
            const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

            buffer.begin();
            buffer.capture('TaskCreated', push('elso'));
            buffer.capture('TaskMoved', () => { throw new Error('rossz payload'); });
            buffer.capture('TaskUpdated', push('harmadik'));
            buffer.end();

            expect(calls).toEqual(['elso', 'harmadik']);
            expect(consoleError).toHaveBeenCalled();
            consoleError.mockRestore();
        });
    });

    describe('beágyazott betöltések', () => {
        it('a BELSŐ befejezés nem ürít - csak a külső', () => {
            //A teljes újraszinkronizálás a saját hatókörén belül hív további betöltéseket
            //(`loadProjects`, `loadProjectData`). Ha a belső kiürítene, a visszajátszás a
            //külső pillanatképe ELŐTT futna, és azt a pillanatkép felülírná - vagyis
            //visszakapnánk pontosan azt a hibát, amit a puffer javít.
            const buffer = new SignalREventBuffer();
            const { calls, push } = recorder();

            buffer.begin();   // resync
            buffer.begin();   // loadProjectData
            buffer.capture('TaskMoved', push('moved'));
            buffer.end();     // loadProjectData vége

            expect(calls).toEqual([]);
            expect(buffer.isBuffering).toBe(true);

            buffer.end();     // resync vége
            expect(calls).toEqual(['moved']);
        });
    });

    describe('biztonsági szelep', () => {
        it('az időkorlát átlépése után kiürít és FELADJA a pufferelést', () => {
            //Azért kell, mert az axios kliensnek nincs időkorlátja: egy beakadt kérés ígérete
            //sosem rendeződik, tehát a `finally`-ben álló `end()` nem futna le. Puffer-őr
            //nélkül a kliens élő kapcsolattal, de EGYETLEN frissítés nélkül működne tovább.
            let clock = 1000;
            const onForceFlush = vi.fn();
            const buffer = new SignalREventBuffer({
                maxBufferingMs: 5_000,
                now: () => clock,
                onForceFlush
            });
            const { calls, push } = recorder();

            buffer.begin();
            buffer.capture('TaskCreated', push('korai'));

            clock += 6_000;   //a betöltés beakadt, az `end()` nem fut le

            //A most érkező eseményt NEM nyeljük el: a hívó azonnal futtatja...
            expect(buffer.capture('TaskMoved', push('kesoi'))).toBe(false);
            //...és a korábban sorba tett is kiürült, nem maradt bent.
            expect(calls).toEqual(['korai']);
            expect(buffer.isBuffering).toBe(false);
            expect(onForceFlush).toHaveBeenCalledWith('timeout', 1);
        });

        it('a sorhossz átlépése után kiürít és FELADJA a pufferelést', () => {
            const onForceFlush = vi.fn();
            const buffer = new SignalREventBuffer({ maxQueueLength: 2, onForceFlush });
            const { calls, push } = recorder();

            buffer.begin();
            buffer.capture('A', push('a'));
            buffer.capture('B', push('b'));

            expect(buffer.capture('C', push('c'))).toBe(false);
            expect(calls).toEqual(['a', 'b']);
            expect(onForceFlush).toHaveBeenCalledWith('overflow', 2);
        });

        it('a kényszerű kiürítés utáni end() nem visz negatív mélységbe', () => {
            //A beakadt betöltés `end()`-je később mégis megérkezhet. Ha az -1-re vinné a
            //mélységet, a RÁKÖVETKEZŐ `begin()` után a puffer nem aktiválódna.
            let clock = 0;
            const buffer = new SignalREventBuffer({ maxBufferingMs: 10, now: () => clock });
            const { calls, push } = recorder();

            buffer.begin();
            clock = 100;
            buffer.capture('A', push('a'));   //kényszerű kiürítés
            buffer.end();                      //a beakadt betöltés befejeződik

            buffer.begin();
            expect(buffer.isBuffering).toBe(true);
            expect(buffer.capture('B', push('b'))).toBe(true);
            buffer.end();

            expect(calls).toEqual(['b']);
        });
    });

    describe('a javított forgatókönyv', () => {
        it('a betöltés alatt érkezett módosítás a pillanatkép UTÁN érvényesül', () => {
            //EZ A LÉNYEGI ÁLLÍTÁS. A javítás előtt a sorrend ez volt:
            //   esemény alkalmazása -> pillanatkép felülírja -> a módosítás ELVESZETT.
            //A pufferrel: pillanatkép -> esemény visszajátszása -> a módosítás MEGMARAD.
            const buffer = new SignalREventBuffer();

            //A tárolót egy sima objektum helyettesíti; a lényeg a műveletek sorrendje.
            let board = 'eredeti';

            buffer.begin();
            //A lekérdezés már elindult; közben megjön a másik felhasználó mozgatása.
            buffer.capture('TaskMoved', () => { board = 'mozgatott'; });

            //A pillanatkép megérkezik - RÉGEBBI, mint az esemény - és felülír.
            board = 'pillanatkep';

            buffer.end();

            expect(board).toBe('mozgatott');
        });
    });
});
