import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import {
    attachmentStore, setAttachments, clearAttachments,
    handleAttachmentUploaded, handleAttachmentDeleted,
    attachmentsForTask, projectAttachments, attachmentsByTask
} from './attachmentStore';
import type { AttachmentResponse } from '../api/attachmentApi';

/**
 * A csatolmányok tárolója.
 *
 * Ezek a tesztek a `taskStore` csatolmány-blokkjából kerültek át, és ugyanaz történt velük,
 * mint a git hivatkozásoknál: az állítás egyszerűbb lett. A feladatok sorain élő listánál a
 * kezelőnek külön ágat kellett tartania arra, hogy a projekt-szintű feltöltésnek nincs
 * `taskId`-ja (`if (!payload.taskId) return;`) - különben minden feladatra felkerült volna a
 * fájl. A lapos tárolóban a `taskId` egyszerű mező: a projekt-szintű csatolmány egy sor,
 * amelynek `null` a `taskId`-ja, és a nézetek ebből szűrnek.
 */
describe('attachmentStore', () => {
    beforeEach(() => clearAttachments());

    const file = (over: Partial<AttachmentResponse> = {}): AttachmentResponse => ({
        id: 'a1',
        projectId: 'p1',
        taskId: 't1',
        taskKey: 'PMA-1',
        taskTitle: 'Hibajavítás',
        fileName: 'terv.pdf',
        contentType: 'application/pdf',
        sizeBytes: 1024,
        attachmentType: 'task',
        uploadedByName: 'Teszt Elek',
        version: 1,
        createdAt: '2026-11-08T12:00:00Z',
        ...over
    });

    describe('handleAttachmentUploaded', () => {
        it('felveszi a csatolmányt', () => {
            handleAttachmentUploaded(file());

            expect(get(attachmentStore).attachments).toHaveLength(1);
        });

        it('nem duplikálja ugyanazt a csatolmányt', () => {
            //A feltöltő a megerősítés válaszából már felvette, és a hub neki is szór.
            handleAttachmentUploaded(file());
            handleAttachmentUploaded(file());

            expect(get(attachmentStore).attachments).toHaveLength(1);
        });

        it('a PROJEKT-szintű csatolmány is bekerül, nem külön ágon', () => {
            //A régi kezelőben ez `if (!payload.taskId) return;` volt, mert a feladatok sorain
            //nem volt hova tenni. Itt egy sor, aminek null a taskId-ja.
            handleAttachmentUploaded(file({ id: 'a2', taskId: null, taskKey: null, taskTitle: null }));

            expect(get(attachmentStore).attachments).toHaveLength(1);
            expect(projectAttachments(get(attachmentStore).attachments)).toHaveLength(1);
        });
    });

    describe('handleAttachmentDeleted', () => {
        it('kiveszi a csatolmányt', () => {
            setAttachments([file({ id: 'a1' }), file({ id: 'a2' })]);

            handleAttachmentDeleted({ attachmentId: 'a1' });

            expect(get(attachmentStore).attachments.map(a => a.id)).toEqual(['a2']);
        });

        it('ismeretlen azonosítóra nem változtat semmit', () => {
            setAttachments([file()]);

            handleAttachmentDeleted({ attachmentId: 'nincs-ilyen' });

            expect(get(attachmentStore).attachments).toHaveLength(1);
        });
    });

    describe('nézetek', () => {
        it('attachmentsForTask csak az adott feladat fájljait adja', () => {
            setAttachments([
                file({ id: 'a1', taskId: 't1' }),
                file({ id: 'a2', taskId: 't2' }),
                file({ id: 'a3', taskId: null })
            ]);

            expect(attachmentsForTask(get(attachmentStore).attachments, 't1').map(a => a.id))
                .toEqual(['a1']);
        });

        it('projectAttachments csak a feladathoz NEM tartozókat adja', () => {
            setAttachments([
                file({ id: 'a1', taskId: 't1' }),
                file({ id: 'a3', taskId: null })
            ]);

            expect(projectAttachments(get(attachmentStore).attachments).map(a => a.id))
                .toEqual(['a3']);
        });

        it('attachmentsByTask feladat szerint csoportosít', () => {
            setAttachments([
                file({ id: 'a1', taskId: 't1', taskKey: 'PMA-1', taskTitle: 'Első' }),
                file({ id: 'a2', taskId: 't2', taskKey: 'PMA-2', taskTitle: 'Második' }),
                file({ id: 'a3', taskId: 't1', taskKey: 'PMA-1', taskTitle: 'Első' })
            ]);

            const groups = attachmentsByTask(get(attachmentStore).attachments);

            expect(groups).toHaveLength(2);
            expect(groups[0].taskId).toBe('t1');
            expect(groups[0].taskKey).toBe('PMA-1');
            expect(groups[0].attachments.map(a => a.id)).toEqual(['a1', 'a3']);
            expect(groups[1].attachments.map(a => a.id)).toEqual(['a2']);
        });

        it('attachmentsByTask a projekt-szintűeket kihagyja', () => {
            //Azok a saját szekciójukban jelennek meg, nem egy "null" nevű csoportban.
            setAttachments([file({ id: 'a3', taskId: null, taskKey: null, taskTitle: null })]);

            expect(attachmentsByTask(get(attachmentStore).attachments)).toHaveLength(0);
        });

        it('a csoport fejléce a CSATOLMÁNY soráról jön, nem a feladatlistából', () => {
            // EZ A LÉNYEGI ÁLLÍTÁS a payload kiterjesztésére: a kliens oldali feladatlista a
            // lezárt sprintek feladatait nem tartalmazza, tehát a kulcsot nem lehetne belőle
            // származtatni - márpedig a régebbi munkához tartozik a legtöbb csatolmány.
            setAttachments([file({ taskId: 'lezart-sprint-feladata', taskKey: 'PMA-99' })]);

            expect(attachmentsByTask(get(attachmentStore).attachments)[0].taskKey).toBe('PMA-99');
        });

        it('hiányzó kulcs esetén üres sztringet ad, nem dob', () => {
            setAttachments([file({ taskId: 't1', taskKey: null, taskTitle: null })]);

            const group = attachmentsByTask(get(attachmentStore).attachments)[0];
            expect(group.taskKey).toBe('');
            expect(group.taskTitle).toBe('');
        });
    });
});
