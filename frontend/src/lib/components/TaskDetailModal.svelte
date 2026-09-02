<script lang="ts">
    import { onMount } from 'svelte';
    import { setActiveTask, taskStore } from '../stores/taskStore';
    import { updateTaskAsync, deleteTaskAsync, type TaskResponse, addAssigneeAsync, removeAssigneeAsync  } from '../api/taskApi';
    import { authStore } from '../stores/authStore';
    import ConfirmModal from './ConfirmModal.svelte';
    import CommentSection from './CommentSection.svelte';
    import { getLabelsAsync, addLabelToTaskAsync, removeLabelFromTaskAsync, type LabelResponse } from '../api/labelApi';
    import { projectStore, setLabels } from '../stores/projectStore';
    import LabelCard from './LabelCard.svelte';
    import CreateLabelModal from './CreateLabelModal.svelte';
    import { boardStore } from '../stores/boardStore';
    import type { BoardResponse } from '../api/boardApi';
    import { sprintStore } from '../stores/sprintStore';
    import type { SprintResponse } from '../api/sprintApi';
    import { teamStore } from '../stores/teamStore';
    import type { MemberResponse } from '../api/teamApi';
    import { type AttachmentResponse, getTaskPresignedUrlAsync, uploadToMinIOAsync, confirmTaskUploadAsync } from '../api/attachmentApi';
    import AttachmentCard from './AttachmentCard.svelte';
    import CommitCard from './CommitCard.svelte';
    import PrCard from './PrCard.svelte';

    import { X, Pencil, Trash2, Info, Paperclip, GitBranch, Plus, MessageSquare } from 'lucide-svelte';
    
    import { notify } from '../stores/notificationStore';

    let activeDetailTab: 'details' | 'attachments' | 'git' | 'comments' = 'details';

    export let task: TaskResponse;
    $: isBacklogTask = !task.boardId && !task.columnId && !task.sprintId;
   
    let boards: BoardResponse[] = [];
    $: boardName = task.boardId 
        ? (boards.find(b => b.id === task.boardId)?.name ?? 'Ismeretlen board')
        : null;
    
    let sprints: SprintResponse[] = [];
    $: sprintName = task.sprintId
        ? (sprints.find(s => s.id === task.sprintId)?.name ?? 'Ismeretlen sprint')
        : null;

    $: currentTask = $taskStore.activeTask ?? task;

    export let projectId: string;
    export let allLabels: LabelResponse[] = [];
    export let isTaskDetailOpen = false;
    export let isCreateLabelOpen = false;

    export let onClose: () => void = () => {};

    let currentUserId = '';

    let isEditing = false;
    let editTitle = task.title;
    let editDescription = task.description ?? '';
    let editPriority = task.priority ?? '';
    let editEstimateInMinutes = task.estimateInMinutes ?? 0;
    let editDueDate = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : '';

    let attachments: AttachmentResponse[] = [];
    $: attachments = (task.attachments ?? []) as AttachmentResponse[];
    let isUploading = false;
    let uploadProgress = 0;
    let uploadError = '';

    let modalRef: HTMLElement;

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    projectStore.subscribe(state => {
        allLabels = state.labels;
    });

    boardStore.subscribe(state => {
        boards = state.boards;
    });

    sprintStore.subscribe(state => {
        sprints = state.sprints;
    });

    let members: MemberResponse[] = [];
    teamStore.subscribe(state => {
        members = state.members;
    });

    onMount(async () => {
        modalRef?.focus();
        
        try {
            const data = await getLabelsAsync(projectId);
            setLabels(data);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba a labelek lekérésekor!');
        }
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
        try {
            const response = await updateTaskAsync(
                projectId, 
                task.id, 
                { 
                    title: editTitle, 
                    description: editDescription, 
                    priority: editPriority !== '' ? editPriority : null, 
                    estimateInMinutes: editEstimateInMinutes ?? null,
                    dueDate: editDueDate ? new Date(editDueDate) : null,
                    rowVersion: task.rowVersion ?? 0
                });
            success = "Módosítva";
            notify.success('Task módosítva!');
            setActiveTask(response);
            isEditing = false;
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a módosítás során!';
            error = message;
            notify.error(message);
        }
    }
    
    async function handleDelete() {
        try {
            await deleteTaskAsync(projectId, task.id);
            notify.success('Task törölve!');
            closeModal();
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a törlés során!';
            error = message;
            notify.error(message);
        }
    }

    let isUpdatingLabels = false;

    async function handleAddLabel(labelId: string) {
        try {
            isUpdatingLabels = true;
            await addLabelToTaskAsync(projectId, task.id, labelId);
            const updated = { ...task, labelIds: [...task.labelIds, labelId] };
            setActiveTask(updated);
            task = updated;
            notify.success('Label hozzáadva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a tag hozzáadásakor!');
        } finally {
            isUpdatingLabels = false;
        }
    }

    async function handleRemoveLabel(labelId: string) {
        try {
            isUpdatingLabels = true;
            await removeLabelFromTaskAsync(projectId, task.id, labelId);
            const updated = { ...task, labelIds: task.labelIds.filter(id => id !== labelId) };
            setActiveTask(updated);
            task = updated;
            notify.success('Label eltávolítva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a tag eltávolításakor!');
        } finally {
            isUpdatingLabels = false;
        }
    }

    async function handleAddAssignee(userId: string) {
        try {
            await addAssigneeAsync(projectId, task.id, userId);
            task = { ...task, assigneeIds: [...task.assigneeIds, userId] };
            notify.success('Task módosítva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt az assignee hozzáadásakor!');
        }
    }

    async function handleRemoveAssignee(userId: string) {
        try {
            await removeAssigneeAsync(projectId, task.id, userId);
            task = { ...task, assigneeIds: task.assigneeIds.filter(id => id !== userId) };
            notify.success('Task módosítva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt az assignee eltávolításakor!');
        }
    }

    async function handleFileUpload(e: Event) {
        const input = e.target as HTMLInputElement;
        if (!input.files || input.files.length === 0) return;

        const files = Array.from(input.files);  // több fájl támogatás
        isUploading = true;
        uploadError = '';

        for (const file of files) {
            try {
                // 1. Presigned URL kérés
                const { presignedUrl, storageKey } = await getTaskPresignedUrlAsync(
                    projectId, task.id, {
                        fileName: file.name,
                        contentType: file.type,
                        sizeBytes: file.size
                    }
                );

                // 2. Direkt feltöltés MinIO-ra
                await uploadToMinIOAsync(presignedUrl, file, (progress) => {
                    uploadProgress = progress;
                });

                // 3. Confirm
                const uploaded = await confirmTaskUploadAsync(projectId, task.id, { storageKey });
                task = { ...task, attachments: [...task.attachments, uploaded] };
                notify.success(`Fájl feltöltve: ${file.name}`);

            } catch (e: any) {
                const message = e.response?.data ?? e.message ?? 'Hiba történt a feltöltéskor!';
                uploadError = message;
                notify.error(message);
            }
        }
        
        isUploading = false;
        uploadProgress = 0;
        input.value = '';
    }

    function closeModal() {
        isTaskDetailOpen = false;
        onClose();
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    bind:this={modalRef}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    >
    <div class="modal-content">
        <div class="modal-header">
            <div class="header-actions">
                <button class="edit-btn" on:click={() => isEditing = !isEditing}>
                    {#if isEditing}
                        <X size={15} /> Mégse
                    {:else}
                        <Pencil size={15} /> Szerkesztés
                    {/if}
                </button>
                <button class="delete-task-btn"
                    on:click={() => openConfirm(
                        'Task törlése',
                        'Biztosan törölni szeretnéd a taskot?',
                        handleDelete
                    )}>
                    <Trash2 size={15} /> Task törlése
                </button>
            </div>
            <button class="close-btn" on:click={closeModal}>
                <X size={16} />
            </button>
            <h1>{task.title}</h1>
            <p class="task-meta">{task.taskKey} -
                {#if !isBacklogTask}
                    {#if sprintName}{sprintName} - {/if}
                    {#if boardName}{boardName} - {task.status}{:else}Nincs Boardhoz rendelve{/if}
                {:else}
                    Projekt Backlog
                {/if}
            </p>
        </div>

        <div class="modal-body">
            {#if !isEditing}
                <div class="tabs-wrapper">
                    <div class="tabs scroll-x">
                        <button class="tab-btn" class:active={activeDetailTab === 'details'}
                            on:click={() => activeDetailTab = 'details'}>
                            <Info size={14} /> Részletek
                        </button>
                        <button class="tab-btn" class:active={activeDetailTab === 'attachments'}
                            on:click={() => activeDetailTab = 'attachments'}>
                            <Paperclip size={14} /> Csatolmányok
                            {#if task.attachments?.length > 0}
                                <span class="tab-badge">{task.attachments.length}</span>
                            {/if}
                        </button>
                        <button class="tab-btn" class:active={activeDetailTab === 'git'}
                            on:click={() => activeDetailTab = 'git'}>
                            <GitBranch size={14} /> Git
                            {#if task.commitLinks.length + task.prLinks.length > 0}
                                <span class="tab-badge">{task.commitLinks.length + task.prLinks.length}</span>
                            {/if}
                        </button>
                        <button class="tab-btn" class:active={activeDetailTab === 'comments'}
                            on:click={() => activeDetailTab = 'comments'}>
                            <MessageSquare size={14} /> Kommentek
                        </button>
                    </div>
                </div>

                <div class="tab-content">
                    {#if activeDetailTab === 'details'}
                        <div class="meta-grid">
                            <span class="meta-label">Létrehozó</span>
                            <span class="meta-value">{task.createdByName}</span>
                            <span class="meta-label">Határidő</span>
                            <span class="meta-value">{task.dueDate ? new Date(task.dueDate).toLocaleDateString('hu-HU') : 'Nincs határidő'}</span>
                            <span class="meta-label">Létrehozva</span>
                            <span class="meta-value">{new Date(task.createdAt).toLocaleDateString('hu-HU')}</span>
                            <span class="meta-label">Módosítva</span>
                            <span class="meta-value">{new Date(task.updatedAt).toLocaleDateString('hu-HU')}</span>
                            {#if task.completedAt}
                                <span class="meta-label">Elkészült</span>
                                <span class="meta-value">{new Date(task.completedAt).toLocaleDateString('hu-HU')}</span>
                            {/if}
                            {#if task.closedAt}
                                <span class="meta-label">Lezárva</span>
                                <span class="meta-value">{new Date(task.closedAt).toLocaleDateString('hu-HU')}</span>
                            {/if}
                        </div>

                        <div class="section">
                            <h3>Hozzárendelt személyek</h3>
                            {#if task.assigneeIds.length > 0}
                                <div class="assignees-row">
                                    {#each task.assigneeIds as userId}
                                        {@const member = members.find(m => m.userId === userId)}
                                        {#if member}
                                            <span class="assignee-badge">{member.displayName}</span>
                                        {/if}
                                    {/each}
                                </div>
                            {:else}
                                <p class="empty">Nincs hozzárendelt személy</p>
                            {/if}
                        </div>

                        <div class="section">
                            <h3>Labelek</h3>
                            <div class="labels-row">
                                {#if task.labelIds.length > 0}
                                    {#each task.labelIds as labelId}
                                        {@const label = allLabels.find(l => l.id === labelId)}
                                        {#if label}<LabelCard {label} showDelete={false} />{/if}
                                    {/each}
                                {:else}
                                    <p class="empty">Nincs label</p>
                                {/if}
                            </div>
                        </div>

                        <div class="optional-fields">
                            <h3>Opcionális mezők</h3>
                            <div class="meta-grid">
                                <span class="meta-label">Leírás</span>
                                <span class="meta-value">{task.description ?? 'Nincs leírás'}</span>
                                <span class="meta-label">Prioritás</span>
                                <span class="meta-value">
                                    <span class="priority priority-{task.priority}">{task.priority ?? 'Nincs'}</span>
                                </span>
                                <span class="meta-label">Becsült idő</span>
                                <span class="meta-value">{task.estimateInMinutes ? `${task.estimateInMinutes} perc` : 'Nincs becslés'}</span>
                            </div>
                        </div>
                    {/if}

                    {#if activeDetailTab === 'attachments'}
                        <div class="section">
                            {#if task.attachments && task.attachments.length > 0}
                                <div class="attachments-list">
                                    {#each task.attachments as attachment (attachment.id)}
                                        <AttachmentCard
                                            {attachment}
                                            {projectId}
                                            taskId={task.id}
                                            onDelete={(id) => {
                                                task = {
                                                    ...task,
                                                    attachments: task.attachments.filter(a => a.id !== id)
                                                };
                                            }}
                                        />
                                    {/each}
                                </div>
                            {:else}
                                <p class="empty">Nincs csatolmány</p>
                            {/if}

                            <label class="upload-btn" class:loading={isUploading}>
                                {isUploading ? `Feltöltés... ${uploadProgress > 0 ? uploadProgress + '%' : ''}` : '+ Fájl feltöltése'}
                                <input type="file" style="display: none"
                                    multiple
                                    on:change={handleFileUpload}
                                    disabled={isUploading} />
                            </label>
                            
                            {#if isUploading && uploadProgress > 0}
                                <div class="progress-bar">
                                    <div class="progress-fill" style="width: {uploadProgress}%"></div>
                                </div>
                            {/if}

                            {#if uploadError}
                                <p class="msg error">{uploadError}</p>
                            {/if}
                        </div>
                    {/if}

                    {#if activeDetailTab === 'git'}
                        <div class="section">
                            <h3>Commitok</h3>
                            {#if task.commitLinks.length > 0}
                                <div class="git-list">
                                    {#each task.commitLinks as commit (commit.id)}
                                        <div class="git-item">
                                            <CommitCard {commit} />
                                        </div>
                                    {/each}
                                </div>
                            {:else}
                                <p class="empty">Nincs commit</p>
                            {/if}
                        </div>
                        <div class="section">
                            <h3>Pull Requestek</h3>
                            {#if task.prLinks.length > 0}
                                <div class="git-list">
                                    {#each task.prLinks as pr (pr.id)}
                                        <div class="git-item">
                                            <PrCard {pr} />
                                        </div>
                                    {/each}
                                </div>
                            {:else}
                                <p class="empty">Nincs pull request</p>
                            {/if}
                        </div>
                    {/if}

                    {#if activeDetailTab === 'comments'}
                        <CommentSection
                            {projectId}
                            taskId={task.id}
                            {currentUserId}
                        />
                    {/if}
                </div>

            {:else}
                <div class="edit-scroll">
                    <h2 class="edit-title">Szerkesztés</h2>

                    <div class="section">
                        <h3>Hozzárendelt személyek</h3>
                        <div class="member-list">
                            {#each members as member}
                                <div class="member-row">
                                    <span class="assignee-name truncate">{member.displayName}</span>
                                    {#if task.assigneeIds.includes(member.userId)}
                                        <button class="label-remove-btn"
                                            on:click={() => handleRemoveAssignee(member.userId)}>
                                            <X size={12} />
                                        </button>
                                    {:else}
                                        <button class="label-add-btn"
                                            on:click={() => handleAddAssignee(member.userId)}>
                                            <Plus size={12} />
                                        </button>
                                    {/if}
                                </div>
                            {/each}
                        </div>
                    </div>

                    <div class="section">
                        <h3>Labelek</h3>
                        <div class="label-edit-list">
                            {#each allLabels as label}
                                <div class="label-edit-row">
                                    <LabelCard {label} showDelete={false} />
                                    {#if currentTask.labelIds.includes(label.id)}
                                        <button class="label-remove-btn"
                                            on:click={() => handleRemoveLabel(label.id)}>
                                            <X size={12} />
                                        </button>
                                    {:else}
                                        <button class="label-add-btn"
                                            on:click={() => handleAddLabel(label.id)}>
                                            <Plus size={12} />
                                        </button>
                                    {/if}
                                </div>
                            {/each}
                        </div>
                        <button class="btn-add" on:click={() => isCreateLabelOpen = true}>
                            <Plus size={14} /> Új label
                        </button>
                    </div>

                    <form id="edit-form">
                        <div class="field">
                            <label>Task címe 
                                <input bind:value={editTitle} placeholder="Maximum 200 karakter">
                            </label>
                        </div>
                        <div class="field">
                            <label>Leírás 
                                <textarea bind:value={editDescription} placeholder="Maximum 250 karakter"></textarea>
                            </label>
                        </div>
                        <div class="field">
                            <label>Prioritás 
                                <select bind:value={editPriority}>
                                    <option value="">Nincs prioritás</option>
                                    <option value="low">Alacsony</option>
                                    <option value="medium">Közepes</option>
                                    <option value="high">Magas</option>
                                    <option value="critical">Kritikus</option>
                                </select>
                            </label>
                        </div>
                        <div class="field">
                            <label>Becsült idő (perc) 
                                <input type="number" bind:value={editEstimateInMinutes}>
                            </label>
                        </div>
                        <div class="field">
                            <label>Határidő 
                                <input type="datetime-local" bind:value={editDueDate}>
                            </label>
                        </div>
                        {#if error}<p class="msg error">{error}</p>{/if}
                        {#if success}<p class="msg success">{success}</p>{/if}
                        <button type="button" class="btn-primary" on:click={() => openConfirm(
                            'Módosítások mentése',
                            'Biztosan menteni szeretnéd a változtatásokat?',
                            handleEdit
                        )}>Módosítások mentése</button>
                    </form>
                </div>
            {/if}
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
{#if isCreateLabelOpen}
    <CreateLabelModal
        bind:isOpen={isCreateLabelOpen}
        projectId={projectId}
        onClose={async () => {
            const data = await getLabelsAsync(projectId);
            setLabels(data);
        }}
    />
{/if}

<style>
    .modal-overlay {
        position: fixed;
        top: 0; left: 0;
        width: 100%; height: 100%;
        background: var(--shadow);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: var(--bg-card);
        border-radius: 8px;
        width: 680px;
        max-width: 95vw;
        min-height: 520px;
        max-height: 85vh;
        position: relative;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }

    @media (max-width: 480px) {
        .modal-header {
            padding-top: 4.5rem;
        }
    }
        
    /* ── Header ── */
    .modal-header {
        padding: 3.25rem 2rem 1rem;
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
        position: relative;
    }

    .modal-content h1 {
        font-size: 1.5rem;
        margin-bottom: 0.35rem;
        word-break: break-word;
    }

    .task-meta {
        font-size: 0.85rem;
        color: var(--text-muted);
    }

    .header-actions {
        position: absolute;
        top: 0.75rem;
        left: 0.75rem;
        display: flex;
        gap: 0.5rem;
        z-index: 10;
        flex-wrap: wrap;
        right: 2.75rem;
    }

    .edit-btn, .delete-task-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        font-size: 0.85rem;
        cursor: pointer;
        padding: 0.3rem 0.6rem;
        border-radius: 5px;
        transition: background 0.15s, color 0.15s;
    }

    /* Edit mód scrollable wrapper */
    .edit-scroll {
        flex: 1;
        overflow-y: auto;
        padding: 1.5rem 2rem;
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
    }

    /* Assignee sorok – fix layout */
    .member-list {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
    }

    .member-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.35rem 0.5rem;
        border-radius: 5px;
        background: var(--bg-secondary);
        border: 1px solid var(--border);
        gap: 0.5rem;
    }

    .edit-btn:hover { background: var(--bg-hover); color: var(--text-primary); }
    .delete-task-btn:hover { background: var(--accent-red-bg); color: var(--accent-red); }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        z-index: 10;
        padding: 0.25rem;
        border-radius: 4px;
    }

    .close-btn:hover { color: var(--text-primary); background: var(--bg-hover); }

    /* ── Body ── */
    .modal-body {
        flex: 1;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }

    /* ── Tabs ── */
    .tabs-wrapper {
        padding: 0.5rem 2rem 0;
        background: var(--bg-card);
        flex-shrink: 0;
    }

    .tabs {
        display: flex;
        padding: 0.65rem;
        gap: 0.25rem;
        border-bottom: 1px solid var(--border);
        overflow-y: hidden;
    }

    .tab-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.65rem 0.85rem;
        border: none;
        border-bottom: 2px solid transparent;
        background: transparent;
        color: var(--text-secondary);
        font-size: 0.85rem;
        cursor: pointer;
        border-radius: 6px 6px 0 0;
        margin-bottom: -1px;
        transition: color 0.15s, border-color 0.15s, background 0.15s;
        white-space: nowrap;
    }

    .tab-btn:hover { color: var(--text-primary); background: var(--bg-hover); }

    .tab-btn.active {
        color: var(--accent-blue);
        border-bottom-color: var(--accent-blue);
        background: transparent;
    }

    .tab-badge {
        background: var(--bg-hover);
        color: var(--text-muted);
        font-size: 0.7rem;
        padding: 0.1rem 0.4rem;
        border-radius: 10px;
    }

    /* ── Tab content ── */
    .tab-content {
        flex: 1;
        overflow-y: auto;
        padding: 1.5rem 2rem;
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
    }

    /* ── Edit mode ── */
    .edit-title {
        font-size: 1.1rem;
        color: var(--text-primary);
        padding: 1.5rem 2rem 0;
    }

    /* ── Meta grid ── */
    .meta-grid {
        display: grid;
        grid-template-columns: auto 1fr;
        gap: 0.35rem 1rem;
        font-size: 0.9rem;
        background: var(--bg-secondary);
        border: 1px solid var(--border);
        border-radius: 6px;
        padding: 0.75rem 1rem;
    }

    .meta-label { color: var(--text-muted); white-space: nowrap; }
    .meta-value { color: var(--text-primary); font-weight: 500; }

    /* ── Sections ── */
    .section {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    h3 {
        font-size: 0.78rem;
        text-transform: uppercase;
        letter-spacing: 0.07em;
        color: var(--text-muted);
    }

    .optional-fields {
        background: var(--bg-primary);
        border: 1px solid var(--border);
        border-radius: 8px;
        padding: 0.85rem 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    /* ── Form ── */
    #edit-form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        padding: 0;
    }

    .field {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
    }

    label {
        font-size: 0.82rem;
        color: var(--text-secondary);
    }

    #edit-form input,
    #edit-form textarea,
    #edit-form select,
    textarea {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    #edit-form textarea { resize: vertical; min-height: 80px; }

    #edit-form input:focus,
    #edit-form textarea:focus,
    #edit-form select:focus,
    textarea:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    /* ── Buttons ── */
    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    .btn-primary {
        background: var(--accent-blue-bg);
        border: 1px solid var(--accent-blue);
        color: var(--accent-blue);
        display: flex;
        align-items: center;
        gap: 0.4rem;
        font-size: 0.9rem;
        transition: background 0.15s;
    }

    .btn-primary:hover { background: var(--accent-blue); color: #fff; }

    .btn-add {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.35rem 0.75rem;
        border-radius: 6px;
        font-size: 0.82rem;
        cursor: pointer;
        width: fit-content;
        align-self: flex-start;
        margin-top: 0.25rem;
    }

    .btn-add:hover { color: var(--text-primary); background: var(--border-hover); }

    .label-remove-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--accent-red);
        cursor: pointer;
        padding: 0.15rem;
        border-radius: 3px;
    }

    .label-remove-btn:hover { background: var(--accent-red-bg); }

    .label-add-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--accent-green);
        cursor: pointer;
        padding: 0.15rem;
        border-radius: 3px;
    }

    .label-add-btn:hover { background: var(--accent-green-bg); }

    /* ── Assignees / Labels ── */
    .assignees-row, .labels-row {
        display: flex;
        flex-wrap: wrap;
        gap: 0.35rem;
    }

    .labels-grid {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
    }

    .label-select-row {
        display: flex;
        align-items: center;
        gap: 0.25rem;
    }

    .assignee-badge {
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
        padding: 0.2rem 0.6rem;
        border-radius: 4px;
        font-size: 0.8rem;
    }

    .assignee-name {
        font-size: 0.9rem;
        color: var(--text-primary);
        flex: 1;
    }

    /* Label szerkesztő sorok */
    .label-edit-list {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
    }

    .label-edit-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.25rem 0.5rem;
        border-radius: 5px;
        background: var(--bg-secondary);
        border: 1px solid var(--border);
        max-width: 100%;
        min-width: 0;
        gap: 0.5rem;
    }

    /* ── Attachments ── */
    .attachments-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        margin-bottom: 0.5rem;
    }

    .upload-btn {
        display: inline-block;
        background: var(--bg-hover);
        border: 1px dashed var(--border-hover);
        color: var(--text-secondary);
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        width: 100%;
        text-align: center;
        transition: background 0.15s, border-color 0.15s;
    }

    .upload-btn:hover { background: var(--border-hover); border-color: var(--text-muted); }
    .upload-btn.loading { opacity: 0.5; cursor: not-allowed; }

    /* ── Git ── */
    .git-list {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .git-item {
        padding: 0.5rem 0.75rem;
        background: var(--bg-hover);
        border-radius: 6px;
        border: 1px solid var(--border-subtle);
    }

    /* ── Priority ── */
    .priority {
        padding: 0.15rem 0.5rem;
        border-radius: 4px;
        font-size: 0.8rem;
        font-weight: 500;
        text-transform: uppercase;
    }

    .priority-low      { background: var(--accent-green-bg);  color: var(--accent-green); }
    .priority-medium   { background: var(--accent-yellow-bg); color: var(--accent-yellow); }
    .priority-high     { background: var(--accent-red-bg);    color: var(--accent-yellow); }
    .priority-critical { background: var(--accent-red-bg);    color: var(--accent-red); }

    /* ── Misc ── */
    .msg { font-size: 0.875rem; }
    .msg.success { color: var(--accent-green); }
    .msg.error   { color: var(--accent-red); white-space: pre-line; word-break: break-word; }

    .empty {
        font-size: 0.85rem;
        color: var(--text-muted);
        padding: 0.5rem 0;
    }
</style>