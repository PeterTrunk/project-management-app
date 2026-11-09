import { describe, it, expect } from 'vitest';
import { SignalRHandlerRegistry } from './signalRHandlerRegistry';

/**
 * A kezelő-nyilvántartás tesztjei.
 *
 * **Miért érdemel saját tesztet.** Ennek a leképezésnek a hibája NÉMA: ha a leválasztás a
 * burkoló helyett az eredeti kezelőt adja át a SignalR-nek, a hívás nem hibázik - csak nem
 * csinál semmit, és a kezelő a komponens megszűnése után is fut tovább. Ez a hibaosztály már
 * egyszer megtörtént ebben a kódbázisban.
 */
describe('SignalRHandlerRegistry', () => {
    const handler = () => {};
    const wrapper = () => {};

    it('a leválasztáshoz a BURKOLÓT adja vissza, nem a kezelőt', () => {
        //EZ A LÉNYEGI ÁLLÍTÁS. A SignalR-ben a burkoló van regisztrálva; ha itt a kezelő jön
        //vissza, a leválasztás némán hatástalan.
        const registry = new SignalRHandlerRegistry();
        registry.remember('TaskMoved', handler, wrapper);

        expect(registry.unsubscribeTarget('TaskMoved', handler)).toBe(wrapper);
    });

    it('ismeretlen kezelőre a kapott kezelőt adja vissza', () => {
        //Visszaesés: egy közvetlenül, a szolgáltatás megkerülésével regisztrált kezelő
        //leválasztása se váljon hatástalanná.
        const registry = new SignalRHandlerRegistry();

        expect(registry.unsubscribeTarget('TaskMoved', handler)).toBe(handler);
    });

    it('ugyanaz a kezelő KÉT eseményen külön burkolót tart', () => {
        //Ez az indoka az eseménynév szerinti tagolásnak. Egyetlen lapos leképezésben a
        //második regisztráció felülírná az első burkolóját, és az első eseményről a
        //leválasztás rossz hivatkozást adna át.
        const registry = new SignalRHandlerRegistry();
        const wrapperA = () => {};
        const wrapperB = () => {};

        registry.remember('CommitLinked', handler, wrapperA);
        registry.remember('PrLinked', handler, wrapperB);

        expect(registry.unsubscribeTarget('CommitLinked', handler)).toBe(wrapperA);
        expect(registry.unsubscribeTarget('PrLinked', handler)).toBe(wrapperB);
    });

    it('két KÜLÖNBÖZŐ kezelő ugyanazon az eseményen egymástól független', () => {
        //Ez a központi és a komponens-szintű kezelő együttélése: ugyanarra az eseményre
        //mindkettő fel van iratkozva, és a komponens leválasztása nem viheti el a központit.
        const registry = new SignalRHandlerRegistry();
        const central = () => {};
        const component = () => {};
        const centralWrapper = () => {};
        const componentWrapper = () => {};

        registry.remember('AttachmentUploaded', central, centralWrapper);
        registry.remember('AttachmentUploaded', component, componentWrapper);

        registry.forget('AttachmentUploaded', component);

        expect(registry.unsubscribeTarget('AttachmentUploaded', central)).toBe(centralWrapper);
        //A leválasztott kezelő már nem ismert, tehát önmagát adja vissza.
        expect(registry.unsubscribeTarget('AttachmentUploaded', component)).toBe(component);
    });

    it('a forgetEvent az esemény ÖSSZES kezelőjét elfelejti', () => {
        //A `callback` nélküli `off()` párja: kijelentkezéskor mindent letakarítunk.
        const registry = new SignalRHandlerRegistry();
        registry.remember('TaskMoved', handler, wrapper);

        registry.forgetEvent('TaskMoved');

        expect(registry.unsubscribeTarget('TaskMoved', handler)).toBe(handler);
    });

    it('a clear minden eseményt elfelejt', () => {
        //A regisztrációk a kapcsolattal együtt elvesznek. Enélkül egy újracsatlakozás után a
        //leképezés a RÉGI kapcsolat burkolóit tartaná, és azokat adná át leválasztásra.
        const registry = new SignalRHandlerRegistry();
        registry.remember('TaskMoved', handler, wrapper);
        registry.remember('TaskUpdated', handler, wrapper);

        registry.clear();

        expect(registry.unsubscribeTarget('TaskMoved', handler)).toBe(handler);
        expect(registry.unsubscribeTarget('TaskUpdated', handler)).toBe(handler);
    });
});
