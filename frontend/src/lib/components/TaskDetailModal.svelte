<script lang="ts">
    import { onMount } from 'svelte';
    import { setActiveTask } from '../stores/taskStore';
    import { updateTaskAsync, deleteTaskAsync, type TaskResponse  } from '../api/taskApi';
    import { getCommentsAsync, createCommentAsync, deleteCommentAsync, type CommentResponse } from '../api/commentApi';
    import { authStore } from '../stores/authStore';
    import ConfirmModal from './ConfirmModal.svelte';
    import { validateTaskDueDate, validateTaskTitle, validateTaskDescription } from '../validators';
    import CommentSection from './CommentSection.svelte';

    export let task: TaskResponse;
    export let projectId: string;
    export let isTaskDetailOpen = false;
    export let onClose: () => void = () => {};

    let currentUserId = '';

    let isEditing = false;
    let editTitle = task.title;
    let editDescription = task.description ?? '';
    let editPriority = task.priority ?? '';
    let editEstimateInMinutes = task.estimateInMinutes ?? 0;
    let editDueDate = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : '';

    let modalRef: HTMLElement;

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    onMount(async () => {
        modalRef?.focus();
    });
    
    let isConfirmOpen = false;
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};

    function openConfirm(title: string, message: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        isConfirmOpen = true;
    }

    let error = '';
    let success = '';

    async function handleEdit() {
        error = '';
        success = '';
        let errorOccured: boolean = false;
        const titleError = validateTaskTitle(editTitle)
        const descError = validateTaskDescription(editDescription);
        if(titleError){
            error = error + titleError;
            errorOccured = true;
        }
        if(descError){
            error = error + descError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            const response = await updateTaskAsync(
                projectId, 
                task.id, 
                { 
                    title: editTitle, 
                    description: editDescription, 
                    priority: editPriority !== '' ? editPriority : null, 
                    estimateInMinutes: editEstimateInMinutes ?? null,
                    dueDate: editDueDate ? new Date(editDueDate) : null
                });
            success = "Módosítva";
            setActiveTask(response);
            isEditing = false;
        } catch (e) {
            error = 'Hiba történt a módosítás során!'
        }
    }
    
    async function handleDelete() {
        try {
            await deleteTaskAsync(projectId, task.id);
            closeModal();
        } catch (e) {
            error = 'Hiba történt a törlés során!';
        }
    }

    function closeModal() {
        isTaskDetailOpen = false;
        onClose();
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    //Accessability configolása
    bind:this={modalRef} //Autofocus helyett.
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    >
    <div class="modal-content">
        <div class="modal-header">
            <div class="header-actions">
                <button class="edit-btn" on:click={() => isEditing = !isEditing}>
                    {isEditing ? '✕ Mégse' : '✏ Szerkesztés'}
                </button>
                <button class="delete-task-btn" 
                    on:click={() => openConfirm(
                        'Task törlése',
                        'Biztosan törölni szeretnéd a taskot? Ez a művelet nem visszavonható!',
                        handleDelete)}
                >🗑 Task törlése</button>
            </div>
            <button class="close-btn" on:click={closeModal}>✕</button>
            <h1>{task.title}</h1>
            <p>{task.taskKey} · {task.status}</p>
        </div>

        <div class="left-column">
            {#if !isEditing}
                <p>Létrehozó: <span>{task.createdByName}</span></p>
                <p>Határidő: <span>{task.dueDate ? new Date(task.dueDate).toLocaleDateString('hu-HU') : 'Nincs határidő'}</span></p>
                <p>Létrehozás ideje: <span>{new Date(task.createdAt).toLocaleDateString('hu-HU')}</span></p>
                <p>Adatok legutóbbi változásának ideje: <span>{new Date(task.updatedAt).toLocaleDateString('hu-HU')}</span></p>
                <!-- Assignee-k -->
                <div class="section">
                    <h3>Hozzárendelt személyek</h3>
                    {#if task.assigneeNames.length > 0}
                        {#each task.assigneeNames as name}
                            <span class="tag">{name}</span>
                        {/each}
                    {:else}
                        <p class="empty">Nincs hozzárendelt személy</p>
                    {/if}
                </div>
                <!-- Labelek -->
                <div class="section">
                    <h3>Labelek</h3>
                    {#if task.labelNames.length > 0}
                        {#each task.labelNames as label}
                            <span class="tag">{label}</span>
                        {/each}
                    {:else}
                        <p class="empty">Nincs label</p>
                    {/if}
                </div>
                <div id="optional-fields">
                    <h2>Opcionális mezők</h2>
                    <p>Leírás: {task.description ?? 'Nincs leírás'}</p>
                    <p>Prioritás: <span class="priority priority-{task.priority}">{task.priority}</span></p>
                    <p>Becsült idő: {task.estimateInMinutes ?? 'Nincs becslés'}</p>
                </div>
                <!-- Commit linkek -->
                <div class="section">
                    <h3>Commit linkek</h3>
                    {#if task.commitLinks.length > 0}
                        {#each task.commitLinks as link}
                            <a href={link} target="_blank">{link}</a>
                        {/each}
                    {:else}
                        <p class="empty">Nincs commit link</p>
                    {/if}
                </div>

                <!-- PR linkek -->
                <div class="section">
                    <h3>PR linkek</h3>
                    {#if task.prLinks.length > 0}
                        {#each task.prLinks as link}
                            <a href={link} target="_blank">{link}</a>
                        {/each}
                    {:else}
                        <p class="empty">Nincs PR link</p>
                    {/if}
                </div>
            {:else}
                <h2>Módosítások</h2>
                <form id="edit-form">
                    Task címe
                    <input bind:value={editTitle} placeholder="Maximum 200 karakter">
                    Leírás
                    <textarea bind:value={editDescription} placeholder="Maximum 250 karakter"></textarea>
                    Prioritás
                    <select bind:value={editPriority}>
                        <option value="">Nincs prioritás</option>
                        <option value="low">Alacsony</option>
                        <option value="medium">Közepes</option>
                        <option value="high">Magas</option>
                        <option value="critical">Kritikus</option>
                    </select>
                    Becsült idő
                    <input type="number" bind:value={editEstimateInMinutes}>
                    Task határidő
                    <input type="datetime-local" bind:value={editDueDate}>
                    {#if error}
                        <p id="failed">{error}</p>
                    {/if}
                    {#if success}
                        <p id="success">{success}</p>
                    {/if}
                    <button type="button" on:click={() => openConfirm(
                        'Módosítások mentése',
                        'Biztosan menteni szeretnéd a változtatásokat?',
                        handleEdit)}
                    >Módosítások mentése</button>
                </form>
            {/if}
        </div>

        <div class="right-column">
            <!-- Kommentek -->
            <CommentSection 
                {projectId}
                taskId={task.id}
                {currentUserId}
            />
        </div>
    </div>
</div>
{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Megerősítés"
        onConfirm={confirmAction}
    />
{/if}


<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }


    .modal-content {
        background: #1e1e1e;
        border-radius: 8px;
        width: 900px;
        max-width: 95vw;
        max-height: 85vh;
        overflow-y: auto;
        position: relative;
        padding: 2rem;
        display: grid;
        grid-template-columns: 1fr 1fr;
        grid-template-rows: auto 1fr;
        gap: 2rem;
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
        display: flex;
        gap: 0.5rem;
        z-index: 10;
    }

    .edit-btn, .delete-task-btn {
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
    }

    .edit-btn:hover { color: white; }
    .delete-btn:hover { color: #ff5555; }
    .delete-task-btn:hover { color: #ff5555; }

    #edit-form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        padding: 1rem;
    }

    #edit-form input,
    #edit-form textarea,
    #edit-form select {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    #edit-form input:focus,
    #edit-form textarea:focus,
    #edit-form select:focus {
        outline: none;
        border-color: #666;
    }

    #edit-form textarea {
        resize: vertical;
        min-height: 80px;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
        z-index: 10;
    }
    

    .close-btn:hover {
        color: white;
    }
    
    .modal-header {
        grid-column: 1 / -1;
        border-bottom: 1px solid #333;
        padding-bottom: 1rem;
        padding-top: 2.5rem;
    }

    .left-column {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
    }

    .right-column {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        border-left: 1px solid #333;
        padding-left: 2rem;
        overflow-y: auto;
    }

    .modal-content h1 {
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
    }

    textarea {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    textarea:focus {
        outline: none;
        border-color: #666;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    #optional-fields {
        background: #161616;
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        margin-top: 0.5rem;
        border: 1px solid #2a2a2a;
    }

    #optional-fields h2 {
        font-size: 1rem;
        color: #aaa;
        margin-bottom: 0.25rem;
    }
    span:not([class*="priority"]){
        font-weight: bold;
    }



    #success { color: greenyellow; }
    #failed { color: red; white-space: pre-line; }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-medium { background: #3a3a1a; color: #ffeb3b; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
    .priority-normal { background: #2a2a2a; color: #aaa; }
</style>