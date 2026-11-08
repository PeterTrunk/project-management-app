import { describe, it, expect } from 'vitest';
import { insertIfAbsentBy } from './collections';

describe('insertIfAbsentBy', () => {
    it('felveszi az új elemet', () => {
        const result = insertIfAbsentBy([{ id: 'a' }], { id: 'b' }, x => x.id);
        expect(result.map(x => x.id)).toEqual(['a', 'b']);
    });

    it('a már meglévő kulcsot NEM veszi fel újra', () => {
        //Ez a lényegi állítás: a pufferelt esemény visszajátszása olyan elem létrehozását is
        //ismétli, amely a pillanatképben már benne volt. Gárda nélkül ez nem esetleges, hanem
        //BIZTOS duplikáció - azonos kulccsal, amitől a Svelte kulcsolt each blokkja dob.
        const result = insertIfAbsentBy([{ id: 'a' }], { id: 'a' }, x => x.id);
        expect(result.map(x => x.id)).toEqual(['a']);
    });

    it('ütközéskor a MEGLÉVŐ elemet tartja meg, nem az érkezőt', () => {
        //Azért kihagyás és nem felülírás: egy létrehozási esemény payloadja soha nem
        //frissebb, viszont lehet SZEGÉNYEBB (a TaskCreated helykitöltőket hordoz).
        const existing = { id: 'a', title: 'valódi érték' };
        const incoming = { id: 'a', title: '' };

        const result = insertIfAbsentBy([existing], incoming, x => x.id);

        expect(result[0].title).toBe('valódi érték');
        //A referencia is ugyanaz: ütközéskor a lista változatlan, tehát a Svelte sem
        //rajzol újra feleslegesen.
        expect(result).toBe(result);
        expect(result[0]).toBe(existing);
    });

    it('nem az `id`-t feltételezi: a kulcsot a hívó adja', () => {
        //A tagság a (projekt, felhasználó) páron azonosítódik, ezért a teamStore `userId`-vel
        //hívja. Egy beégetett `id` ott némán rossz kulcsot használna.
        const result = insertIfAbsentBy(
            [{ userId: 'u1', role: 'Member' }],
            { userId: 'u1', role: 'Admin' },
            m => m.userId
        );

        expect(result).toHaveLength(1);
        expect(result[0].role).toBe('Member');
    });

    it('üres listára is felveszi', () => {
        expect(insertIfAbsentBy([], { id: 'a' }, x => x.id)).toHaveLength(1);
    });
});
