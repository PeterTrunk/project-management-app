<script lang="ts">
    import { onMount } from 'svelte';
    import { sprintStore, setSprints } from '../stores/sprintStore';
    import { getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { getTasksAsync, type TaskResponse, deleteTaskAsync } from '../api/taskApi';
    import { projectStore } from '../stores/projectStore';
    import { boardStore, setBoards } from '../stores/boardStore';
    import { getBoardsAsync } from '../api/boardApi';
    import { planSprintAsync, activateSprintAsync, 
            assignTaskToSprintAsync, removeTaskFromSprintAsync,
            deleteSprintAsync, getUnfinishedTasksAsync } from '../api/sprintApi';

    import SprintCard from './SprintCard.svelte';
    import ProjectBacklog from './ProjectBacklog.svelte';
    import CreateSprintModal from './CreateSprintModal.svelte';
    import UpdateSprintModal from './UpdateSprintModal.svelte';
    import CompleteSprintModal from './CompleteSprintModal.svelte';
    import ConfirmModal from './ConfirmModal.svelte';

    export let projectId: string;

    let isCreateSprintOpen = false;
    let isUpdateSprintOpen = false;
    let isCompleteSprintOpen = false;
    let isConfirmOpen = false;

    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};

    let sprints: SprintResponse[] = [];
    let activeSprint: SprintResponse | null = null;
    let allTasks: TaskResponse[] = [];
    let selectedSprint: SprintResponse | null = null;
    let selectedCompleteSprint: SprintResponse | null = null;
    let unfinishedTasks: TaskResponse[] = [];
    let planningCollapsed = true;
    let completedCollapsed = true;

    // Csak store olvasás — NEM async!
    sprintStore.subscribe(state => {
        sprints = state.sprints;
        activeSprint = state.activeSprint;
    });

    async function loadAll() {
        // Sprintek
        const sprintData = await getSprintsAsync(projectId);
        setSprints(sprintData);
        
        // Boardok
        if ($boardStore.boards.length === 0) {
            const boardData = await getBoardsAsync(projectId);
            setBoards(boardData);
        }
        
        // Összes task — board és sprint szűrés NÉLKÜL
        const taskData = await getTasksAsync(projectId);
        allTasks = [...taskData];
    }

    onMount(async () => {
        await loadAll();
    });

    async function refreshTasks() {
        const taskData = await getTasksAsync(projectId);
        allTasks = [...taskData];
    }

    async function handleActivateSprint(sprintId: string) {
        await activateSprintAsync(projectId, sprintId);
        await loadAll();
    }

    async function handlePlanSprint(sprintId: string) {
        await planSprintAsync(projectId, sprintId);
        await loadAll();
    }

    async function handleDeleteSprint(sprintId: string) {
        openConfirm(
            'Sprint törlése',
            'Biztosan törölni szeretnéd a sprintet?',
            async () => {
                await deleteSprintAsync(projectId, sprintId);
                await loadAll();
            }
        );
    }

    async function handleAssignToSprint(taskId: string, sprintId: string) {
        if (sprintId === '') {
            // Backlogba visszarakás
            const task = allTasks.find(t => t.id === taskId);
            if (task?.sprintId) {
                await removeTaskFromSprintAsync(projectId, task.sprintId, taskId);
            }
        } else {
            await assignTaskToSprintAsync(projectId, sprintId, taskId);
        }
        await refreshTasks();
    }

    async function handleTaskDropped(taskId: string, sprintId: string) {
        await assignTaskToSprintAsync(projectId, sprintId, taskId);
        await refreshTasks();
    }

    async function handleRemoveFromSprint(taskId: string, sprintId: string) {
        await removeTaskFromSprintAsync(projectId, sprintId, taskId);
        await refreshTasks();
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
            console.error('Backend hiba:', e.response?.data);
        }
    }

    async function handleDeleteTask(taskId: string) {
        openConfirm(
            'Task törlése',
            'Biztosan törölni szeretnéd a taskot? Ez a művelet nem visszavonható!',
            async () => {
                await deleteTaskAsync(projectId, taskId);
                await refreshTasks();
            }
        );
    }

    function openConfirm(title: string, message: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        isConfirmOpen = true;
    }
</script>

<div class="sprints-container">
    <!-- Toolbar -->
    <div class="sprints-toolbar">
        <button class="toolbar-btn" on:click={() => isCreateSprintOpen = true}>+ Új sprint</button>
    </div>

    <!-- Completed sprintek -->
    <button class="section-toggle" on:click={() => completedCollapsed = !completedCollapsed}>
        {completedCollapsed ? '▶' : '▼'} Befejezett sprintek ({sprints.filter(s => s.state === 'Completed').length})
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
                onBoardAssigned={async () => await loadAll()}
            />
        {/each}
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
            onBoardAssigned={async () => await loadAll()}
        />
    {/if}

    <!-- Planning sprintek -->
    <button class="section-toggle" on:click={() => planningCollapsed = !planningCollapsed}>
       {planningCollapsed ? '▶' : '▼'} Tervezett sprintek ({sprints.filter(s => s.state === 'Planning').length})
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
                onBoardAssigned={async () => await loadAll()}
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
        onRefresh={refreshTasks}
    />

</div>

{#if isCreateSprintOpen}
    <CreateSprintModal
        bind:isSprintCreationOpen={isCreateSprintOpen}
        projectId={projectId}
        onClose={async () => {
            const data = await getSprintsAsync(projectId);
            setSprints(data);
            isCreateSprintOpen = false;
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
        onClose={() => isCompleteSprintOpen = false}
    />
{/if}

{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Törlés"
        onConfirm={confirmAction}
    />
{/if}

<style>

</style>