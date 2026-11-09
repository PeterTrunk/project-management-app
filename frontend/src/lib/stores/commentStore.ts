import { writable } from 'svelte/store';
import type { CommentResponse } from '../api/commentApi';
import { insertIfAbsentBy } from './collections';

/**
 * A feladatok kommentjei, feladat szerint kulcsolva.
 *
 * **Miért van saját tárolója.** A kommentek korábban a `CommentSection` lokális változójában
 * éltek, a két esemény kezelője pedig szintén ott volt - és minden idegen komment érkezésekor
 * ÚJRATÖLTÖTTE az egész listát. Ez szükségtelen volt: a `CommentAdded` payloadja a teljes
 * kommentet hordozza (azonos alakban a REST válasszal), tehát közvetlenül alkalmazható.
 *
 * **Miért feladat szerint, és miért nem töltünk be mindent előre.** A kommentek csak a
 * megnyitott részletnézetben láthatók, és nincs projekt-szintű lekérésük sem. A tároló ezért
 * gyorsítótár: egy feladat kommentjei az első megnyitáskor kerülnek be, és onnantól az
 * események tartják frissen - akkor is, ha a részletnézet közben bezárult.
 *
 * **A hiányzó kulcs jelentése.** Ha egy feladathoz nincs bejegyzés, az azt jelenti, hogy még
 * NEM TÖLTÖTTÜK BE - nem azt, hogy nincs kommentje (az üres tömb). A különbségtétel azért
 * kell, mert egy be nem töltött feladat eseményét el kell dobni: egyetlen komment felvétele
 * azt a látszatot adná, hogy a feladatnak pontosan egy kommentje van.
 */

interface CommentState {
    /** feladat azonosító → a feladat kommentjei. A kulcs hiánya = még nincs betöltve. */
    byTaskId: Record<string, CommentResponse[]>;
}

const initialState: CommentState = { byTaskId: {} };

export const commentStore = writable<CommentState>(initialState);

/** A betöltés eredménye. Innentől ennek a feladatnak az eseményei alkalmazhatók. */
export function setComments(taskId: string, comments: CommentResponse[]) {
    commentStore.update(state => ({
        byTaskId: { ...state.byTaskId, [taskId]: comments }
    }));
}

export function clearComments() {
    commentStore.set(initialState);
}

export function handleCommentAdded(payload: CommentResponse) {
    commentStore.update(state => {
        const existing = state.byTaskId[payload.taskId];

        //Be nem töltött feladat eseményét eldobjuk - lásd a fejkommentet. A következő
        //megnyitás a teljes listát lekéri, tehát nem veszik el semmi.
        if (!existing) return state;

        return {
            byTaskId: {
                ...state.byTaskId,
                //A gárda a saját kommentünk miatt kell: a hub a csoport MINDEN tagjának szór,
                //a szerzőnek is - ő pedig a POST válaszából már felvette.
                [payload.taskId]: insertIfAbsentBy(existing, payload, c => c.id)
            }
        };
    });
}

export function handleCommentDeleted(payload: { taskId: string; commentId: string }) {
    commentStore.update(state => {
        const existing = state.byTaskId[payload.taskId];
        if (!existing) return state;

        return {
            byTaskId: {
                ...state.byTaskId,
                [payload.taskId]: existing.filter(c => c.id !== payload.commentId)
            }
        };
    });
}

/**
 * Egy feladat kommentjei, vagy `undefined`, ha még nincs betöltve.
 *
 * A hívó a kettőt meg tudja különböztetni: az `undefined` betöltést igényel, az üres tömb
 * azt jelenti, hogy nincs komment.
 */
export function commentsForTask(
    state: CommentState, taskId: string
): CommentResponse[] | undefined {
    return state.byTaskId[taskId];
}
