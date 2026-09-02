<script lang="ts">
    import { sprintStore, setSprints } from '../stores/sprintStore';
    import { getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { type TaskResponse, deleteTaskAsync } from '../api/taskApi';
    import { boardStore } from '../stores/boardStore';
    import { planSprintAsync, activateSprintAsync, 
            assignTaskToSprintAsync, removeTaskFromSprintAsync,
            deleteSprintAsync, getUnfinishedTasksAsync } from '../api/sprintApi';
    import { taskStore } from '../stores/taskStore';
    import SprintCard from './SprintCard.svelte';
    import ProjectBacklog from './ProjectBacklog.svelte';
    import CreateSprintModal from './CreateSprintModal.svelte';
    import UpdateSprintModal from './UpdateSprintModal.svelte';
    import CompleteSprintModal from './CompleteSprintModal.svelte';
    import ConfirmModal from './ConfirmModal.svelte';
    import { getTasksAsync } from '../api/taskApi';
    
    import { Plus, ChevronDown, ChevronRight } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';
    
    export let projectId: string;
    
    let isCreateSprintOpen = false;
    let isUpdateSprintOpen = false;
    let isCompleteSprintOpen = false;
    let isConfirmOpen = false;
    
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmText = '';
    let confirmAction: () => Promise<void> = async () => {};
    
    let sprints: SprintResponse[] = [];
    let activeSprint: SprintResponse | null = null;
    let allTasks: TaskResponse[] = [];
    let selectedSprint: SprintResponse | null = null;
    let selectedCompleteSprint: SprintResponse | null = null;
    let unfinishedTasks: TaskResponse[] = [];
    let planningCollapsed = true;
    let completedCollapsed = true;
    let completedSprintsLoaded = false;
    
    taskStore.subscribe(state => {
        allTasks = state.tasks;
    });

    sprintStore.subscribe(state => {
        sprints = state.sprints;
        activeSprint = state.activeSprint;
    });

    async function handleActivateSprint(sprintId: string) {
        const sprintTasks = allTasks.filter(t => t.sprintId === sprintId);
        const tasksWithoutBoard = sprintTasks.filter(t => !t.boardId);

        if (tasksWithoutBoard.length > 0) {
            openConfirm(
                'Sprint aktiválása',
                `Biztosan aktiválod a sprintet úgy hogy ${tasksWithoutBoard.length} task nincs boardhoz rendelve?
                \nEzek a taskok nem fognak megjelenni egyik boardon se ameddig nincsenek valamely boardhoz hozzárendelve.`,
                'Aktiválás',
                async () => {
                    try {
                        await activateSprintAsync(projectId, sprintId);
                        notify.success('Sprint aktiválva!');
                    } catch (e: any) {
                        notify.error(e.response?.data ?? e.message ?? 'Hiba történt a sprint aktiválásakor!');
                    }
                }
            );
        } else {
            try {
                await activateSprintAsync(projectId, sprintId);
                notify.success('Sprint aktiválva!');
            } catch (e: any) {
                notify.error(e.response?.data ?? e.message ?? 'Hiba történt a sprint aktiválásakor!');
            }
        }
    }

    async function handlePlanSprint(sprintId: string) {
        openConfirm(
        'Sprint visszatervezése',
        'Biztosan visszatervezed a sprintet? Az összes task visszakerül a Board Backlog oszlopba!',
        'Megerősítés',
        async () => {
            try {
                await planSprintAsync(projectId, sprintId);
                notify.success('Sprint visszatervezve!');
            } catch (e: any) {
                notify.error(e.response?.data ?? e.message ?? 'Hiba történt a sprint visszatervezésekor!');
            }
        });
    }

    async function handleDeleteSprint(sprintId: string) {
        openConfirm(
            'Sprint törlése',
            'Biztosan törölni szeretnéd a sprintet?',
            'Törlés',
            async () => {
                try {
                    await deleteSprintAsync(projectId, sprintId);
                    notify.success('Sprint törölve!');
                } catch (e: any) {
                    notify.error(e.response?.data ?? e.message ?? 'Hiba történt a sprint törlésekor!');
                }
            }
        );
    }

    async function handleAssignToSprint(taskId: string, sprintId: string) {
        try {
            if (sprintId === '') {
                // Backlogba visszarakás
                const task = allTasks.find(t => t.id === taskId);
                if (task?.sprintId) {
                    await removeTaskFromSprintAsync(projectId, task.sprintId, taskId, task.rowVersion ?? 0);
                }
            } else {
                const task = allTasks.find(t => t.id === taskId);
                await assignTaskToSprintAsync(projectId, sprintId, taskId, task?.rowVersion ?? 0);
            }
            notify.success('Task módosítva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a task sprinthez rendelésekor!');
        }
    }

    async function handleRemoveFromSprint(taskId: string, sprintId: string) {
        try {
            const task = allTasks.find(t => t.id === taskId);
            await removeTaskFromSprintAsync(projectId, sprintId, taskId, task?.rowVersion ?? 0);
            notify.success('Task módosítva!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a task sprintből eltávolításakor!');
        }
    }

    function openEditSprint(sprint: SprintResponse) {
        selectedSprint = sprint;
        isUpdateSprintOpen = true;
    }

    async function openCompleteSprint(sprint: SprintResponse) {
        selectedCompleteSprint = sprint;
        try {
            unfinishedTasks = await getUnfinishedTasksAsync(projectId, sprint.id);
            isCompleteSprintOpen = true;
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a befejezetlen taskok lekérésekor!');
        }
    }

    async function handleDeleteTask(taskId: string) {
        openConfirm(
            'Task törlése',
            'Biztosan törölni szeretnéd a taskot? Ez a művelet nem visszavonható!',
            'Törlés',
            async () => {
                try {
                    await deleteTaskAsync(projectId, taskId);
                    notify.success('Task törölve!');
                } catch (e: any) {
                    notify.error(e.response?.data ?? e.message ?? 'Hiba történt a task törlésekor!');
                }
            }
        );
    }

    async function toggleCompleted() {
        completedCollapsed = !completedCollapsed;
        if (!completedCollapsed && !completedSprintsLoaded) {
            try {
                const completedSprints = await getSprintsAsync(projectId, 'completed');
                // Merge a meglévő sprintekkel (ne írja felül az active/planning-et)
                sprintStore.update(state => ({
                    ...state,
                    sprints: [
                        ...state.sprints.filter(s => s.state !== 'Completed'),
                        ...completedSprints
                    ]
                }));
                completedSprintsLoaded = true;
            } catch (e: any) {
                notify.error(e.response?.data ?? e.message ?? 'Hiba történt a lezárt sprintek lekérésekor!');
            }
        }
    }

    let completedSprintTasksLoaded = new Set<string>();
    
    async function handleLoadCompletedSprintTasks(sprintId: string) {
        try {
            const tasks = await getTasksAsync(projectId, undefined, sprintId);
            taskStore.update(state => ({
                ...state,
                tasks: [
                    ...state.tasks.filter(t => t.sprintId !== sprintId),
                    ...tasks
                ]
            }));
            completedSprintTasksLoaded = new Set([...completedSprintTasksLoaded, sprintId]);
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a sprint taskjainak lekérésekor!');
        }
    }
    
    function openConfirm(title: string, message: string, text: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmText = text;
        confirmAction = action;
        isConfirmOpen = true;
    }
</script>

<div class="sprints-container">
    <!-- Toolbar -->
    <div class="sprints-toolbar">
        <button class="toolbar-btn" on:click={() => isCreateSprintOpen = true}>
            <Plus size={15} /> Új sprint
        </button>
    </div>

    <div class="sprints-content">
        <!-- Completed sprintek -->
        <button class="section-toggle" on:click={toggleCompleted}>
            {#if completedCollapsed}
                <ChevronRight size={14} />
            {:else}
                <ChevronDown size={14} />
            {/if}
            Befejezett sprintek {completedSprintsLoaded ? `(${sprints.filter(s => s.state === 'Completed').length})` : '(?)'}
        </button>
        {#if !completedCollapsed}
            {#each sprints.filter(s => s.state === 'Completed') as sprint}
                <SprintCard
                    sprint={sprint}
                    tasks={allTasks.filter(t => t.sprintId === sprint.id)}
                    boards={$boardStore.boards}
                    projectId={projectId}
                    onActivate={handleActivateSprint}
                    onPlan={handlePlanSprint}
                    onEdit={openEditSprint}
                    onComplete={openCompleteSprint}
                    onDelete={handleDeleteSprint}
                    onRemoveTask={handleRemoveFromSprint}
                    onDeleteTask={handleDeleteTask}
                    onBoardAssigned={async () => {}}
                    tasksLoaded={completedSprintTasksLoaded.has(sprint.id)}
                    onLoadTasks={sprint.state === 'Completed' ? handleLoadCompletedSprintTasks : null}
                />
            {/each}
            <hr class="completed-divider">
        {/if}

        <!-- Aktív sprint -->
        {#if activeSprint}
            <SprintCard
                sprint={activeSprint}
                sprints={sprints.filter(s => s.state !== 'Completed')}
                tasks={allTasks.filter(t => t.sprintId === activeSprint!.id)}
                boards={$boardStore.boards}
                projectId={projectId}
                onActivate={handleActivateSprint}
                onPlan={handlePlanSprint}
                onEdit={openEditSprint}
                onComplete={openCompleteSprint}
                onDelete={handleDeleteSprint}
                onRemoveTask={handleRemoveFromSprint}
                onDeleteTask={handleDeleteTask}
                onAssignToSprint={handleAssignToSprint}
                onBoardAssigned={async () => {}}
            />
        {/if}

        <!-- Planning sprintek -->
        <button class="section-toggle" on:click={() => planningCollapsed = !planningCollapsed}>
            {#if planningCollapsed}
                <ChevronRight size={14} />
            {:else}
                <ChevronDown size={14} />
            {/if}
            Tervezett sprintek ({sprints.filter(s => s.state === 'Planning').length})
        </button>
        {#if !planningCollapsed}
            {#each sprints.filter(s => s.state === 'Planning') as sprint}
                <SprintCard
                    sprint={sprint}
                    sprints={sprints.filter(s => s.state !== 'Completed')}
                    tasks={allTasks.filter(t => t.sprintId === sprint.id)}
                    boards={$boardStore.boards}
                    projectId={projectId}
                    onActivate={handleActivateSprint}
                    onPlan={handlePlanSprint}
                    onEdit={openEditSprint}
                    onComplete={openCompleteSprint}
                    onDelete={handleDeleteSprint}
                    onRemoveTask={handleRemoveFromSprint}
                    onDeleteTask={handleDeleteTask}
                    onAssignToSprint={handleAssignToSprint}
                    onBoardAssigned={async () => {}}
                    activeSprint={activeSprint}
                />
            {/each}
        {/if}

        <!-- Backlog szekció -->
        <ProjectBacklog
            tasks={allTasks}
            sprints={sprints}
            boards={$boardStore.boards}
            projectId={projectId}
            onAssignToSprint={handleAssignToSprint}
            onDelete={handleDeleteTask}
            onRefresh={async () => {}}
        />
    </div>

</div>

{#if isCreateSprintOpen}
    <CreateSprintModal
        bind:isSprintCreationOpen={isCreateSprintOpen}
        projectId={projectId}
        onClose={async () => {
            const data = await getSprintsAsync(projectId);
            setSprints(data);
        }}
    />
{/if}

{#if isUpdateSprintOpen && selectedSprint}
    <UpdateSprintModal
        bind:isUpdateSprintOpen={isUpdateSprintOpen}
        projectId={projectId}
        sprint={selectedSprint}
        onClose={async () => {
            const data = await getSprintsAsync(projectId);
            setSprints(data);
        }}
    />
{/if}

{#if isCompleteSprintOpen && selectedCompleteSprint}
    <CompleteSprintModal
        bind:isCompleteSprintOpen={isCompleteSprintOpen}
        projectId={projectId}
        sprint={selectedCompleteSprint}
        unfinishedTasks={unfinishedTasks}
        sprints={sprints}
        onClose={async () => { isCompleteSprintOpen = false; }}
    />
{/if}

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText={confirmText}
        onConfirm={confirmAction}
    />
{/if}

<style>
    .sprints-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow: hidden;
    }

    .sprints-toolbar {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: var(--toolbar-padding);
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .toolbar-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
    }

    .toolbar-btn:hover {
        background: var(--border-hover);
        color: var(--text-primary);
    }

    .section-toggle {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: var(--bg-secondary);
        border: none;
        border-bottom: 1px solid var(--border);
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.85rem;
        font-weight: bold;
        padding: 0.6rem 1rem;
        text-align: left;
        width: 100%;
        letter-spacing: 0.03em;
        text-transform: uppercase;
        transition: color 0.15s, background 0.15s;
        word-break: break-word;
        text-align: left;
    }

    .section-toggle:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    .sprints-content {
        padding: 0.75rem 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        overflow-y: auto;
        flex: 1;
    }

    .completed-divider {
        border: none;
        border-top: 1px solid var(--border);
        margin: 0.25rem 0;
    }
</style>