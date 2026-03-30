<script lang="ts">
    import { onMount } from 'svelte';
    import { sprintStore, setSprints } from '../stores/sprintStore';
    import { getSprintsAsync, type SprintResponse } from '../api/sprintApi';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import { projectStore } from '../stores/projectStore';
    import { boardStore } from '../stores/boardStore';
    import { taskStore, setTasks } from '../stores/taskStore';
    import { planSprintAsync, activateSprintAsync, 
             assignTaskToSprintAsync, removeTaskFromSprintAsync
              } from '../api/sprintApi';
    import CreateSprintModal from './CreateSprintModal.svelte';

    export let projectId: string;
    let isCreateSprintOpen = false;
    

    let sprints: SprintResponse[] = [];
    let activeSprint: SprintResponse | null = null;
    let tasks: TaskResponse[] = [];

    let planningCollapsed = false;
    let completedCollapsed = false;

    sprintStore.subscribe(state => {
        sprints = state.sprints;
        activeSprint = state.activeSprint;
    });

    projectStore.subscribe(state =>{
         state.activeProject
    });

    taskStore.subscribe(state => {
        tasks = state.tasks;
    });

    onMount(async () => {
        const data = await getSprintsAsync(projectId);
        setSprints(data);
    });

    // Sprint taskjai
    function getSprintTasks(sprintId: string): TaskResponse[] {
        return tasks.filter(t => t.sprintId === sprintId);
    }

    // Sprint taskjai board szerint csoportosítva
    function getSprintTasksByBoard(sprintId: string): [string, TaskResponse[]][] {
        const sprintTasks = getSprintTasks(sprintId);
        //const boards = boardStore;
        
        let boardMap: Record<string, TaskResponse[]> = {};
        
        sprintTasks.forEach(task => {
            const boardName = task.boardId
                ? ($boardStore.boards.find(b => b.id === task.boardId)?.name ?? 'Ismeretlen board')
                : 'Backlog';
            
            if (!boardMap[boardName]) boardMap[boardName] = [];
            boardMap[boardName].push(task);
        });
        
        return Object.entries(boardMap);
    }

    // Handler-ek
    async function handleActivateSprint(sprintId: string) {
        await activateSprintAsync(projectId, sprintId);
        const data = await getSprintsAsync(projectId);
        setSprints(data);
    }

    async function handlePlanSprint(sprintId: string) {
        await planSprintAsync(projectId, sprintId);
        const data = await getSprintsAsync(projectId);
        setSprints(data);
    }

    async function handleDeleteSprint(sprintId: string) {
        // ConfirmModal-lal
    }

    async function handleAssignToSprint(taskId: string, sprintId: string) {
        if (!sprintId) return;
        await assignTaskToSprintAsync(projectId, sprintId, taskId);
        const _tasks = await getTasksAsync(projectId);
        setTasks(_tasks);
    }

    async function handleRemoveFromSprint(taskId: string, sprintId: string) {
        await removeTaskFromSprintAsync(projectId, sprintId, taskId);
        const _tasks = await getTasksAsync(projectId);
        setTasks(_tasks);
    }

    function openEditSprint(sprint: SprintResponse) {
        // TODO: CreateSprintModal megnyitása szerkesztés módban
    }

    function openCompleteSprint(sprint: SprintResponse) {
        // TODO: CompleteSprintModal megnyitása
    }

</script>

<div class="sprints-container">
    <!-- Toolbar -->
    <div class="sprints-toolbar">
        <button class="toolbar-btn" on:click={() => isCreateSprintOpen = true}>+ Új sprint</button>
    </div>

    <!-- Aktív sprint -->
    {#if activeSprint}
        <div class="sprint-card active">
            <div class="sprint-header">
                <div class="sprint-title">
                    <span class="active-badge">★ AKTÍV</span>
                    <h2>{activeSprint.name}</h2>
                </div>
                <div class="sprint-dates">
                    {activeSprint.startDate ? new Date(activeSprint.startDate).toLocaleDateString('hu-HU') : '?'}
                    —
                    {activeSprint.endDate ? new Date(activeSprint.endDate).toLocaleDateString('hu-HU') : '?'}
                </div>
                <div class="sprint-actions">
                    <button on:click={() => openEditSprint(activeSprint!)}>✏ Szerkesztés</button>
                    <button class="danger-btn" on:click={() => openCompleteSprint(activeSprint!)}>✓ Lezárás</button>
                    <button on:click={() => handlePlanSprint(activeSprint!.id)}>↩ Visszatervezés</button>
                </div>
            </div>
            {#if activeSprint.goal}
                <p class="sprint-goal">Cél: {activeSprint.goal}</p>
            {/if}
            <div class="sprint-tasks">
                {#each getSprintTasksByBoard(activeSprint.id) as [boardName, boardTasks]}
                    <div class="board-group">
                        <h4>{boardName}</h4>
                        {#each boardTasks as task}
                            <div class="sprint-task-card">
                                <span class="task-key">{task.taskKey}</span>
                                <span class="task-title">{task.title}</span>
                                <button class="remove-btn" on:click={() => handleRemoveFromSprint(task.id, activeSprint!.id)}>✕</button>
                            </div>
                        {/each}
                    </div>
                {/each}
                {#if getSprintTasks(activeSprint.id).length === 0}
                    <p class="empty">Nincs task ebben a sprintben</p>
                {/if}
            </div>
        </div>
    {:else}
        <div class="no-active-sprint">
            <p>Nincs aktív sprint</p>
        </div>
    {/if}

    <!-- Planning sprintek -->
    <div class="sprint-section">
        <button class="section-header" on:click={() => planningCollapsed = !planningCollapsed}>
            {planningCollapsed ? '▶' : '▼'} Planning ({sprints.filter(s => s.state === 'Planning').length})
        </button>
        {#if !planningCollapsed}
            {#each sprints.filter(s => s.state === 'Planning') as sprint}
                <div class="sprint-card planning">
                    <div class="sprint-header">
                        <h2>{sprint.name}</h2>
                        <div class="sprint-dates">
                            {sprint.startDate ? new Date(sprint.startDate).toLocaleDateString('hu-HU') : '?'}
                            —
                            {sprint.endDate ? new Date(sprint.endDate).toLocaleDateString('hu-HU') : '?'}
                        </div>
                        <div class="sprint-actions">
                            <button on:click={() => openEditSprint(sprint)}>✏ Szerkesztés</button>
                            <button class="activate-btn" on:click={() => handleActivateSprint(sprint.id)}>▶ Aktiválás</button>
                            <button class="danger-btn" on:click={() => handleDeleteSprint(sprint.id)}>🗑 Törlés</button>
                        </div>
                    </div>
                    {#if sprint.goal}
                        <p class="sprint-goal">Cél: {sprint.goal}</p>
                    {/if}
                    <div class="sprint-tasks">
                        {#each getSprintTasksByBoard(sprint.id) as [boardName, boardTasks]}
                            <div class="board-group">
                                <h4>{boardName}</h4>
                                {#each boardTasks as task}
                                    <div class="sprint-task-card">
                                        <span class="task-key">{task.taskKey}</span>
                                        <span class="task-title">{task.title}</span>
                                        <button class="remove-btn" on:click={() => handleRemoveFromSprint(task.id, sprint.id)}>✕</button>
                                    </div>
                                {/each}
                            </div>
                        {/each}
                        {#if getSprintTasks(sprint.id).length === 0}
                            <p class="empty">Nincs task ebben a sprintben</p>
                        {/if}
                    </div>
                </div>
            {/each}
            {#if sprints.filter(s => s.state === 'Planning').length === 0}
                <p class="empty-section">Nincs tervezett sprint</p>
            {/if}
        {/if}
    </div>

    <!-- Completed sprintek -->
    <div class="sprint-section">
        <button class="section-header" on:click={() => completedCollapsed = !completedCollapsed}>
            {completedCollapsed ? '▶' : '▼'} Befejezett ({sprints.filter(s => s.state === 'Completed').length})
        </button>
        {#if !completedCollapsed}
            {#each sprints.filter(s => s.state === 'Completed') as sprint}
                <div class="sprint-card completed">
                    <div class="sprint-header">
                        <h2>{sprint.name}</h2>
                        <div class="sprint-dates">
                            {sprint.startDate ? new Date(sprint.startDate).toLocaleDateString('hu-HU') : '?'}
                            —
                            {sprint.endDate ? new Date(sprint.endDate).toLocaleDateString('hu-HU') : '?'}
                        </div>
                    </div>
                    {#if sprint.goal}
                        <p class="sprint-goal">Cél: {sprint.goal}</p>
                    {/if}
                    <div class="sprint-tasks">
                        {#each getSprintTasksByBoard(sprint.id) as [boardName, boardTasks]}
                            <div class="board-group">
                                <h4>{boardName}</h4>
                                {#each boardTasks as task}
                                    <div class="sprint-task-card">
                                        <span class="task-key">{task.taskKey}</span>
                                        <span class="task-title">{task.title}</span>
                                    </div>
                                {/each}
                            </div>
                        {/each}
                        {#if getSprintTasks(sprint.id).length === 0}
                            <p class="empty">Nincs task ebben a sprintben</p>
                        {/if}
                    </div>
                </div>
            {/each}
            {#if sprints.filter(s => s.state === 'Completed').length === 0}
                <p class="empty-section">Nincs befejezett sprint</p>
            {/if}
        {/if}
    </div>

    <!-- Backlog szekció -->
    <div class="sprint-section">
        <div class="section-header-static">
            <h3>Projekt Backlog</h3>
        </div>
        <div class="backlog-tasks">
            {#each tasks.filter(t => !t.boardId && !t.columnId && !t.sprintId) as task}
                <div class="backlog-task-card">
                    <span class="task-key">{task.taskKey}</span>
                    <span class="task-title">{task.title}</span>
                    <div class="backlog-actions">
                        <select on:change={(e) => handleAssignToSprint(task.id, e.currentTarget.value)}>
                            <option value="">→ Sprinthez adás</option>
                            {#each sprints.filter(s => s.state !== 'Completed') as sprint}
                                <option value={sprint.id}>{sprint.name}</option>
                            {/each}
                        </select>
                    </div>
                </div>
            {/each}
            {#if tasks.filter(t => !t.boardId && !t.columnId && !t.sprintId).length === 0}
                <p class="empty">Nincs backlog task</p>
            {/if}
        </div>
    </div>
</div>

{#if isCreateSprintOpen}
    <CreateSprintModal
        bind:isSprintCreationOpen={isCreateSprintOpen}
        projectId={projectId}
        onClose={async () => {
            const data = await getSprintsAsync(projectId);
            isCreateSprintOpen = false;
        }}
    />
{/if}

<style>

</style>