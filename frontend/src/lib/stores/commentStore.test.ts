import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import {
    commentStore, setComments, clearComments, commentsForTask,
    handleCommentAdded, handleCommentDeleted
} from './commentStore';
import type { CommentResponse } from '../api/commentApi';

/**
 * A kommentek tárolója.
 *
 * A LÉNYEGI ÁLLÍTÁS az, hogy az esemény payloadja KÖZVETLENÜL alkalmazható - enélkül a
 * komponens minden idegen komment érkezésekor újratöltötte a teljes listát. Ehhez a szerver
 * payloadja a REST válasz DTO-jával azonos alakot küld, mezőnévre is; ezért van a `comment()`
 * segédfüggvény típusa `CommentResponse` mind a betöltésnél, mind az eseménynél.
 */
describe('commentStore', () => {
    beforeEach(() => clearComments());

    const comment = (over: Partial<CommentResponse> = {}): CommentResponse => ({
        id: 'c1',
        taskId: 't1',
        userId: 'u1',
        userName: 'Teszt Elek',
        body: 'Egy megjegyzés',
        createdAt: new Date('2026-11-08T12:00:00Z'),
        updatedAt: new Date('2026-11-08T12:00:00Z'),
        ...over
    });

    describe('betöltés és a "még nincs betöltve" állapot', () => {
        it('betöltés előtt undefined, nem üres tömb', () => {
            //A különbségtétel nem formalitás: ebből tudja a komponens, hogy le kell kérni,
            //és ebből tudja a kezelő, hogy el kell dobnia az eseményt.
            expect(commentsForTask(get(commentStore), 't1')).toBeUndefined();
        });

        it('betöltés után üres tömb, ha nincs komment', () => {
            setComments('t1', []);

            expect(commentsForTask(get(commentStore), 't1')).toEqual([]);
        });

        it('a feladatok egymástól függetlenek', () => {
            setComments('t1', [comment()]);

            expect(commentsForTask(get(commentStore), 't1')).toHaveLength(1);
            expect(commentsForTask(get(commentStore), 't2')).toBeUndefined();
        });
    });

    describe('handleCommentAdded', () => {
        it('felveszi a kommentet a payloadból, újratöltés nélkül', () => {
            // EZ A LÉNYEGI ÁLLÍTÁS: a payload teljes, tehát nincs szükség lekérésre.
            setComments('t1', []);

            handleCommentAdded(comment({ body: 'Új megjegyzés' }));

            const comments = commentsForTask(get(commentStore), 't1')!;
            expect(comments).toHaveLength(1);
            expect(comments[0].body).toBe('Új megjegyzés');
            expect(comments[0].userName).toBe('Teszt Elek');
        });

        it('a SAJÁT kommentet nem duplikálja', () => {
            //A hub a csoport MINDEN tagjának szór, a szerzőnek is - ő pedig a POST
            //válaszából már felvette. Gárda nélkül a saját komment kétszer jelenne meg.
            setComments('t1', []);

            handleCommentAdded(comment());
            handleCommentAdded(comment());

            expect(commentsForTask(get(commentStore), 't1')).toHaveLength(1);
        });

        it('a BE NEM TÖLTÖTT feladat eseményét eldobja', () => {
            //Egyetlen komment felvétele azt a látszatot adná, hogy a feladatnak pontosan
            //egy kommentje van. A következő megnyitás a teljes listát lekéri, tehát nem
            //veszik el semmi.
            handleCommentAdded(comment());

            expect(commentsForTask(get(commentStore), 't1')).toBeUndefined();
        });

        it('más feladat kommentje nem kerül ehhez a feladathoz', () => {
            setComments('t1', []);
            setComments('t2', []);

            handleCommentAdded(comment({ id: 'c2', taskId: 't2' }));

            expect(commentsForTask(get(commentStore), 't1')).toHaveLength(0);
            expect(commentsForTask(get(commentStore), 't2')).toHaveLength(1);
        });

        it('megtartja a már bent lévő kommentek sorrendjét', () => {
            setComments('t1', [comment({ id: 'c1' }), comment({ id: 'c2' })]);

            handleCommentAdded(comment({ id: 'c3' }));

            expect(commentsForTask(get(commentStore), 't1')!.map(c => c.id))
                .toEqual(['c1', 'c2', 'c3']);
        });
    });

    describe('handleCommentDeleted', () => {
        it('kiveszi a kommentet', () => {
            setComments('t1', [comment({ id: 'c1' }), comment({ id: 'c2' })]);

            handleCommentDeleted({ taskId: 't1', commentId: 'c1' });

            expect(commentsForTask(get(commentStore), 't1')!.map(c => c.id)).toEqual(['c2']);
        });

        it('ismeretlen azonosítóra nem változtat semmit', () => {
            setComments('t1', [comment()]);

            handleCommentDeleted({ taskId: 't1', commentId: 'nincs-ilyen' });

            expect(commentsForTask(get(commentStore), 't1')).toHaveLength(1);
        });

        it('a BE NEM TÖLTÖTT feladat eseményét eldobja', () => {
            handleCommentDeleted({ taskId: 't1', commentId: 'c1' });

            expect(commentsForTask(get(commentStore), 't1')).toBeUndefined();
        });

        it('nem nyúl más feladat kommentjeihez', () => {
            setComments('t1', [comment({ id: 'c1' })]);
            setComments('t2', [comment({ id: 'c1', taskId: 't2' })]);

            handleCommentDeleted({ taskId: 't1', commentId: 'c1' });

            //Ugyanaz az azonosító két feladat alatt elvben nem fordul elő, de a szűrésnek
            //akkor sem szabad átnyúlnia: a kulcs a feladat, nem a komment.
            expect(commentsForTask(get(commentStore), 't1')).toHaveLength(0);
            expect(commentsForTask(get(commentStore), 't2')).toHaveLength(1);
        });
    });
});
