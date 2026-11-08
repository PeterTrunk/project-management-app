import { writable, get } from 'svelte/store';
import type { TaskResponse } from '../api/taskApi';
import { projectStore } from './projectStore';
import { insertIfAbsentBy } from './collections';

interface TaskState {
    tasks: TaskResponse[];
    activeTask: TaskResponse | null;
}

const initialState: TaskState = {
    tasks: [],
    activeTask: null
}

export const taskStore = writable<TaskState>(initialState);

/**
 * A STORE INVARIÁNSA: ugyanaz a feladat két helyen van - a `tasks` listában (ezt a tábla
 * mutatja) és az `activeTask`-ban (ezt a részletnézet mutatja). A kettő SOHA nem tarthat
 * különböző verziót ugyanarról a sorról.
 *
 * Ez nem elméleti szabály. A tizenkét SignalR-kezelő eddig is betartotta, a két MANUÁLIS
 * író viszont nem, és mindkettő épp az ütközés-sáv útvonalán van:
 *
 * - a `setTasks` csak a listát írta, tehát egy kézi szinkronizálás vagy egy reconnect utáni
 *   resync után a nyitott szerkesztő a RÉGI tokent látta: a sáv nem jelent meg, a mentés
 *   pedig figyelmeztetés nélkül 409-re futott;
 * - a `setActiveTask` csak az aktív elemet írta, tehát a "Változások betöltése" friss
 *   állapota egyetlen tetszőleges esemény után VISSZAÁLLT a listában maradt elavult sorra
 *   (a `syncActiveTask` onnan olvas újra) - a sáv visszajött, az adat visszaugrott.
 *
 * Ezért mindkét író mindkét helyre ír. A szabály itt van kikényszerítve, nem a hívási
 * helyeken: egy következő hívó így nem tudja kihagyni.
 */
export function setTasks(tasks: TaskResponse[]) {
    taskStore.update(state => ({ ...state, tasks, activeTask: syncActiveTask(state, tasks) }));
}

export function setActiveTask(task: TaskResponse | null) {
    taskStore.update(state => {
        if (task === null) return { ...state, activeTask: null };

        //A friss példány a listába is bekerül. Ha még nem szerepel benne (lezárt sprint
        //feladata, vagy a lista szűkebb hatókörrel töltődött), akkor nem vesszük fel:
        //a lista hatókörét nem ennek a hívásnak a dolga megváltoztatni.
        const tasks = state.tasks.some(t => t.id === task.id)
            ? state.tasks.map(t => (t.id === task.id ? task : t))
            : state.tasks;

        return { ...state, tasks, activeTask: task };
    });
}

export function clearTask() {
    taskStore.set(initialState);
}

// SignalR handle metódusok

export function handleTaskCreated(payload: {
    id: string;
    boardId: string | null;
    columnId: string | null;
    sprintId: string | null;
    taskKey: string;
    title: string;
    priority: string | null;
    dueDate: string | null;
    estimateInMinutes: number | null;
    position: string;
    createdAt: string;
    completedAt: string | null;
    //A szerver küldi; korábban a típus nem deklarálta, és az `as unknown as` cast elnémította.
    rowVersion: number;
}) {
    const projectId = get(projectStore).activeProject?.id ?? '';
    taskStore.update(state => ({
        ...state,
        //A gárda KIHAGY, és nem felülír. Ez itt nem stílus kérdése: az alábbi objektum a
        //payloadból csak azt tudja, amit az esemény hordoz, a többit HELYKITÖLTŐVEL pótolja
        //(leírás, státusz, felelősök, címkék). Ha a feladat már a tárolóban van - mert egy
        //teljes újratöltés pillanatképe tartalmazta -, a felülírás ezekre a helykitöltőkre
        //cserélné a valódi értékeket.
        tasks: insertIfAbsentBy(state.tasks, {
            ...payload,
            projectId,
            description: null,
            status: '',
            assigneeIds: [],
            labelIds: [],
            commitLinks: [],
            prLinks: [],
            attachments: [],
            createdByName: '',
            closedAt: null,
            updatedAt: null
        } as unknown as TaskResponse, t => t.id)
    }));
}

export function handleTaskUpdated(payload: {
    taskId: string;
    title?: string;
    description?: string;
    priority?: string;
    dueDate?: string | null;
    estimateInMinutes?: number | null;
    boardId?: string | null;
    columnId?: string | null;
    position?: string;
    sprintId?: string | null;
    rowVersion?: number;
}) {
    const { taskId, ...rest } = payload;
    taskStore.update(state => {
        const tasks = state.tasks.map(t =>
            t.id === taskId
                ? { ...t, ...rest, rowVersion: rest.rowVersion ?? t.rowVersion } as TaskResponse
                : t
        );
        //Az aktív elem is követi a listát. Korábban kimaradt, ezért egy nyitott
        //részletnézet NEM mutatta a másik felhasználó módosítását. Ez most biztonságos:
        //a szerkesztő a mentéskor nem ebből veszi a tokent, hanem a saját alapvonalából.
        return { ...state, tasks, activeTask: syncActiveTask(state, tasks) };
    });
}

export function handleTaskMoved(payload: {
    taskId: string;
    boardId: string | null;
    columnId: string | null;
    sprintId: string | null;
    position: string;
    completedAt: string | null;
    //Az oszlopból származtatott státusz. A DTO denormalizálva tartja, ezért az esemény is
    //küldi - enélkül a mozgatás után a kliensen a régi státusz maradna.
    status?: string;
    rowVersion?: number;
}) {
    taskStore.update(state => {
        const tasks = state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t,
                    boardId: payload.boardId,
                    columnId: payload.columnId ?? t.columnId,
                    sprintId: payload.sprintId,
                    position: payload.position,
                    //A denormalizált státusz: enélkül a mozgatás után a régi érték maradt.
                    status: payload.status ?? t.status,
                    completedAt: payload.completedAt ? new Date(payload.completedAt) : null,
                    rowVersion: payload.rowVersion ?? t.rowVersion
                  } as unknown as TaskResponse
                : t
        );
        //Az aktív elem is követi a listát. Korábban kimaradt, ezért egy NYITOTT
        //részletnézet nem követte a mozgatást: a board mögötte frissült, a fejléc nem.
        //Ez biztonságos: a szerkesztő a mentéskor nem ebből veszi a tokent, hanem a
        //saját alapvonalából.
        return { ...state, tasks, activeTask: syncActiveTask(state, tasks) };
    });
}

export function handleTaskDeleted(payload: { taskId: string }) {
    taskStore.update(state => ({
        ...state,
        tasks: state.tasks.filter(t => t.id !== payload.taskId),
        //A törölt feladat nem maradhat aktív. Enélkül a nyitott részletnézet egy MÁR NEM
        //LÉTEZŐ feladatot mutatott tovább, a mentés pedig 404-et adott - a felhasználó
        //szempontjából értelmezhetetlenül, hiszen a feladat ott volt előtte a képernyőn.
        //A szülők `{#if ... && $taskStore.activeTask}` alatt rendereznek, tehát a nullázás
        //zárja az ablakot; az indokot a SignalR-regisztráció mondja ki a felhasználónak.
        activeTask: state.activeTask?.id === payload.taskId ? null : state.activeTask
    }));
}

export function handleTasksRebalanced(payload: {
    boardId: string;
    columnId: string;
    tasks: { id: string; position: string; rowVersion: number }[];
}) {
    taskStore.update(state => {
        const tasks = state.tasks.map(t => {
            const rebalanced = payload.tasks.find(r => r.id === t.id);
            return rebalanced ? { ...t, position: rebalanced.position, rowVersion: rebalanced.rowVersion } : t;
        });
        return { ...state, tasks, activeTask: syncActiveTask(state, tasks) };
    });
}

export function handleTaskAssigneeAdded(payload: { taskId: string; userId: string }) {
    taskStore.update(state => {
        const updatedTasks = state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, assigneeIds: t.assigneeIds.includes(payload.userId) 
                    ? t.assigneeIds 
                    : [...t.assigneeIds, payload.userId] }
                : t
        );
        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? updatedTasks.find(t => t.id === payload.taskId) ?? state.activeTask
            : state.activeTask;
        return { ...state, tasks: updatedTasks, activeTask: updatedActiveTask };
    });
}

export function handleTaskAssigneeRemoved(payload: { taskId: string; userId: string }) {
    taskStore.update(state => {
        const updatedTasks = state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, assigneeIds: t.assigneeIds.filter(id => id !== payload.userId) }
                : t
        );
        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? updatedTasks.find(t => t.id === payload.taskId) ?? state.activeTask
            : state.activeTask;
        return { ...state, tasks: updatedTasks, activeTask: updatedActiveTask };
    });
}

export function handleTaskLabelAdded(payload: { taskId: string; labelId: string }) {
    taskStore.update(state => {
        const updatedTasks = state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, labelIds: t.labelIds.includes(payload.labelId)
                    ? t.labelIds
                    : [...t.labelIds, payload.labelId] }
                : t
        );
        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? updatedTasks.find(t => t.id === payload.taskId) ?? state.activeTask
            : state.activeTask;
        return { ...state, tasks: updatedTasks, activeTask: updatedActiveTask };
    });
}

export function handleTaskLabelRemoved(payload: { taskId: string; labelId: string }) {
    taskStore.update(state => {
        const updatedTasks = state.tasks.map(t =>
            t.id === payload.taskId
                ? { ...t, labelIds: t.labelIds.filter(id => id !== payload.labelId) }
                : t
        );
        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? updatedTasks.find(t => t.id === payload.taskId) ?? state.activeTask
            : state.activeTask;
        return { ...state, tasks: updatedTasks, activeTask: updatedActiveTask };
    });
}

/**
 * A megnyitott részletnézet külön hivatkozáson ül,
 * ezért a frissített listából kell újraolvasni, akkor is ha a hivatkozás épp ELKERÜLT róla.
 */
function syncActiveTask(state: TaskState, tasks: TaskResponse[]): TaskResponse | null {
    if (!state.activeTask) return null;
    return tasks.find(t => t.id === state.activeTask!.id) ?? state.activeTask;
}

//A git hivatkozások kezelői a `gitLinkStore`-ba kerültek át. Ugyanaz az adat korábban KÉT
//helyen élt - itt, a feladatok sorain, és a `GitView` lokális listáiban -, és minden esemény
//mindkettőt frissítette. A `TaskResponse` DTO továbbra is hordozza a `commitLinks` és a
//`prLinks` mezőt, de a felület NEM olvassa: a részletnézet is a `gitLinkStore`-ból
//származtatja őket a `taskId` szerint.

export function handleAttachmentUploaded(payload: {
    attachmentId: string;
    projectId: string;
    taskId: string | null;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    attachmentType: string;
    uploadedByName: string;
    version: number;
    createdAt: string;
}) {
    if (!payload.taskId) return;
    
    taskStore.update(state => {
        const newAttachment = {
            id: payload.attachmentId,
            projectId: payload.projectId,
            taskId: payload.taskId,
            fileName: payload.fileName,
            contentType: payload.contentType,
            sizeBytes: payload.sizeBytes,
            attachmentType: payload.attachmentType,
            uploadedByName: payload.uploadedByName,
            version: payload.version,
            createdAt: payload.createdAt
        };
        
        const updatedTasks = state.tasks.map(task =>
            task.id === payload.taskId
                ? {
                    ...task,
                    attachments: task.attachments.some(a => a.id === payload.attachmentId)
                        ? task.attachments  // már megvan → skip
                        : [...(task.attachments ?? []), newAttachment]
                }
                : task
        );

        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? {
                ...state.activeTask,
                attachments: state.activeTask.attachments.some(a => a.id === payload.attachmentId)
                    ? state.activeTask.attachments  // már megvan → skip
                    : [...(state.activeTask.attachments ?? []), newAttachment]
            }
            : state.activeTask;

        return {
            ...state,
            tasks: updatedTasks,
            activeTask: updatedActiveTask
        };
    });
}

export function handleAttachmentDeleted(payload: {
    attachmentId: string;
    projectId: string;
    taskId: string | null;
}) {
    if (!payload.taskId) return;
    
    taskStore.update(state => {
        const updatedTasks = state.tasks.map(task =>
            task.id === payload.taskId
                ? {
                    ...task,
                    attachments: task.attachments.filter(a => a.id !== payload.attachmentId)
                }
                : task
        );

        //Active Taskot külön.
        const updatedActiveTask = state.activeTask?.id === payload.taskId
            ? updatedTasks.find(t => t.id === payload.taskId) ?? state.activeTask
            : state.activeTask;
        
        return {
            ...state,
            tasks: updatedTasks,
            activeTask: updatedActiveTask
        };
    });
}