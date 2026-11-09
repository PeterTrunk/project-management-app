import { describe, it, expect, beforeEach } from 'vitest';
import { get } from 'svelte/store';
import {
    taskStore, setTasks, setActiveTask, clearTask,
    handleTaskLabelAdded, handleTaskLabelRemoved,
    handleTaskAssigneeAdded, handleTaskAssigneeRemoved
} from './taskStore';
import type { TaskResponse } from '../api/taskApi';

/**
 * A címke- és személy-hozzárendelés TELJES sorrendje, ahogy a részletnézet végzi.
 *
 * **Miért külön tesztosztály.** A `taskStore.test.ts` a kezelőket EGYMAGUKBAN méri. A felületen
 * viszont két írás követi egymást: a komponens először optimistán beírja a saját műveletét
 * (`setActiveTask` a módosított listával), majd megérkezik a szerver eseménye, és a kezelő
 * MÉGEGYSZER alkalmazza ugyanazt. A kettő együtt nem volt lefedve.
 *
 * A sorrend azért kényes, mert a kezelők az aktív elemet a LISTÁBÓL származtatják újra - tehát
 * az optimista írásnak a listába is be kell kerülnie, különben a visszaszármaztatás elveszi a
 * saját műveletünk hatását.
 */
describe('a hozzárendelés felületi sorrendje', () => {
    beforeEach(() => clearTask());

    const task = (id: string): TaskResponse => ({
        id, title: 'Feladat', labelIds: [], assigneeIds: [],
        commitLinks: [], prLinks: [], attachments: [], rowVersion: 5
    } as never);

    /**
     * Amit a komponens tesz a sikeres kérés után: UGYANAZT a kezelőt hívja, amit a szerver
     * eseménye is. Korábban saját objektumot épített (`{ ...task, labelIds: [...] }`), dedup
     * nélkül - lásd a tesztosztály alatti blokkot.
     */
    function optimistic(apply: () => void) {
        apply();
    }

    describe('címke', () => {
        it('hozzáadás: az optimista írás és az esemény együtt sem duplikál', () => {
            setTasks([task('t1')]);
            setActiveTask(task('t1'));

            optimistic(() => handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' }));
            handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' });

            expect(get(taskStore).activeTask!.labelIds).toEqual(['L1']);
            expect(get(taskStore).tasks[0].labelIds).toEqual(['L1']);
        });

        it('eltávolítás: az esemény nem hozza vissza a címkét', () => {
            setTasks([task('t1')]);
            setActiveTask(task('t1'));
            optimistic(() => handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' }));

            optimistic(() => handleTaskLabelRemoved({ taskId: 't1', labelId: 'L1' }));
            handleTaskLabelRemoved({ taskId: 't1', labelId: 'L1' });

            expect(get(taskStore).activeTask!.labelIds).toEqual([]);
            expect(get(taskStore).tasks[0].labelIds).toEqual([]);
        });

        it('a listából KIMARADT feladatnál is megmarad a művelet', () => {
            //Lezárt sprint feladata: a lista szűkebb hatókörrel töltődött, tehát a feladat
            //nincs benne. A kezelő az aktív elemet a listából származtatja újra - ha nem
            //találja, meg kell tartania a meglévőt, különben a saját műveletünk elvész.
            setTasks([]);
            setActiveTask(task('t1'));

            optimistic(() => handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' }));
            handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' });

            expect(get(taskStore).activeTask!.labelIds).toEqual(['L1']);
        });

        it('a KULCSOLT megjelenítés nem kaphat duplikált azonosítót', () => {
            // EZ A LÉNYEGI ÁLLÍTÁS. A board kártyája kulcsolt each blokkban rendereli a
            // címkéket (`as labelId (labelId)`), és egy duplikált kulcs ott futásidejű hibát
            // dob - a kártya frissítése megszakad, ezért csak az ELSŐ címke jelent meg. A
            // részletnézet chips-listája kulcs nélküli, tehát ugyanazt a duplikátumot
            // hibátlanul, KÉTSZER kirenderelte.
            //
            // A hiba abból jött, hogy a komponens dedup nélkül fűzött, az esemény pedig
            // megérkezhetett a kérés ALATT - vagyis a fűzés másodszor is felvette. Mostantól
            // a komponens ugyanezt a kezelőt hívja, tehát a dedup egy helyen van.
            setTasks([task('t1')]);
            setActiveTask(task('t1'));

            //A szerver eseménye a kérés alatt megérkezik...
            handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' });
            //...majd a komponens is alkalmazza a saját műveletét.
            optimistic(() => handleTaskLabelAdded({ taskId: 't1', labelId: 'L1' }));

            const ids = get(taskStore).activeTask!.labelIds;
            expect(ids).toEqual(['L1']);
            expect(new Set(ids).size).toBe(ids.length);
            expect(get(taskStore).tasks[0].labelIds).toEqual(['L1']);
        });

        it('három címke egymás után mind felkerül', () => {
            //A board tünete: "csak az első label kerül fel". Ha a lista bármelyik lépésnél
            //duplikálódik, a kulcsolt blokk elhasal, és a többi nem jelenik meg.
            setTasks([task('t1')]);
            setActiveTask(task('t1'));

            for (const id of ['L1', 'L2', 'L3']) {
                optimistic(() => handleTaskLabelAdded({ taskId: 't1', labelId: id }));
                handleTaskLabelAdded({ taskId: 't1', labelId: id });
            }

            expect(get(taskStore).activeTask!.labelIds).toEqual(['L1', 'L2', 'L3']);
            expect(get(taskStore).tasks[0].labelIds).toEqual(['L1', 'L2', 'L3']);
        });
    });


    describe('hozzárendelt személy', () => {
        it('hozzáadás: az optimista írás és az esemény együtt sem duplikál', () => {
            setTasks([task('t1')]);
            setActiveTask(task('t1'));

            optimistic(() => handleTaskAssigneeAdded({ taskId: 't1', userId: 'u1' }));
            handleTaskAssigneeAdded({ taskId: 't1', userId: 'u1' });

            expect(get(taskStore).activeTask!.assigneeIds).toEqual(['u1']);
        });

        it('eltávolítás: az esemény nem hozza vissza a hozzárendelést', () => {
            setTasks([task('t1')]);
            setActiveTask(task('t1'));
            optimistic(() => handleTaskAssigneeAdded({ taskId: 't1', userId: 'u1' }));

            optimistic(() => handleTaskAssigneeRemoved({ taskId: 't1', userId: 'u1' }));
            handleTaskAssigneeRemoved({ taskId: 't1', userId: 'u1' });

            expect(get(taskStore).activeTask!.assigneeIds).toEqual([]);
        });
    });
});
