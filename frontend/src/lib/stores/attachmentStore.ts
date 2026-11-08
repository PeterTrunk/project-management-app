import { writable } from 'svelte/store';
import type { AttachmentResponse } from '../api/attachmentApi';
import { insertIfAbsentBy } from './collections';

/**
 * A projekt csatolmányai - a projekt-szintűek és a feladatokhoz tartozók EGY listában.
 *
 * **Miért egy lista.** A kettő ugyanaz az entitás, és a különbség egyetlen mező: a `taskId`.
 * A szerver is így tárolja, az esemény is egy nevet használ mindkettőre (`AttachmentUploaded`),
 * és a lekérés is egyben adja őket. Két lista ugyanabból az adatból csak arra lenne jó, hogy a
 * kezelőnek el kelljen döntenie, melyikbe tegye - ami egy újabb elágazás, amit el lehet
 * téveszteni. Ugyanaz a döntés, mint a `gitLinkStore`-nál.
 *
 * **Mit váltott le.** Ugyanaz az adat korábban KÉT helyen élt: a `taskStore` feladatain
 * (`attachments`) és a `TeamResources` lokális listáiban. A komponens minden
 * csatolmány-eseményre újratöltötte a projekt csatolmányait ÉS a teljes feladatgráfot -
 * hatókör nélkül, tehát a lezárt sprintek minden feladatával, hozzárendelésével és címkéjével
 * együtt. Ez volt a réteg legdrágább ismétlődő kérése.
 */

interface AttachmentState {
    attachments: AttachmentResponse[];
}

const initialState: AttachmentState = { attachments: [] };

export const attachmentStore = writable<AttachmentState>(initialState);

export function setAttachments(attachments: AttachmentResponse[]) {
    attachmentStore.set({ attachments });
}

export function clearAttachments() {
    attachmentStore.set(initialState);
}

export function handleAttachmentUploaded(payload: AttachmentResponse) {
    attachmentStore.update(state => ({
        //Kihagyás, nem felülírás: a feltöltő a megerősítés VÁLASZÁBÓL már felvette, és a hub
        //neki is szór. A két alak azonos teljességű (a szerver a feladatot mindkettőhöz
        //külön betölti), tehát a kihagyásnak nincs ára.
        attachments: insertIfAbsentBy(state.attachments, payload, a => a.id)
    }));
}

export function handleAttachmentDeleted(payload: { attachmentId: string }) {
    attachmentStore.update(state => ({
        attachments: state.attachments.filter(a => a.id !== payload.attachmentId)
    }));
}

/** Egy feladat csatolmányai - a részletnézet ebből dolgozik. */
export function attachmentsForTask(
    attachments: AttachmentResponse[], taskId: string
): AttachmentResponse[] {
    return attachments.filter(a => a.taskId === taskId);
}

/** A projekt-szintű csatolmányok: amelyek nem tartoznak feladathoz. */
export function projectAttachments(attachments: AttachmentResponse[]): AttachmentResponse[] {
    return attachments.filter(a => a.taskId === null);
}

/**
 * A feladatokhoz tartozó csatolmányok, feladat szerint csoportosítva.
 *
 * A csoport fejléce a soron érkező `taskKey` / `taskTitle` - nem a feladatlistából. A
 * csoportok sorrendje az első csatolmány érkezési sorrendjét követi, ami a lekérés
 * rendezéséből (legújabb elöl) adódik.
 */
export function attachmentsByTask(attachments: AttachmentResponse[]): {
    taskId: string;
    taskKey: string;
    taskTitle: string;
    attachments: AttachmentResponse[];
}[] {
    const groups = new Map<string, {
        taskId: string; taskKey: string; taskTitle: string; attachments: AttachmentResponse[];
    }>();

    for (const attachment of attachments) {
        if (attachment.taskId === null) continue;

        let group = groups.get(attachment.taskId);
        if (!group) {
            group = {
                taskId: attachment.taskId,
                //Üres sztring, ha a szerver nem adta: a fejléc így is olvasható marad, és a
                //hiány nem dob. A `MapToDto` mindkét forrásban kitölti, tehát ez tartalék.
                taskKey: attachment.taskKey ?? '',
                taskTitle: attachment.taskTitle ?? '',
                attachments: []
            };
            groups.set(attachment.taskId, group);
        }
        group.attachments.push(attachment);
    }

    return [...groups.values()];
}
