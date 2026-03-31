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

    import SprintCard from './SprintCard.svelte';
    import ProjectBacklog from './ProjectBacklog.svelte';
    import CreateSprintModal from './CreateSprintModal.svelte';

    export let projectId: string;
    let isCreateSprintOpen = false;
    let isCreateTaskOpen = false;
    

    let sprints: SprintResponse[] = [];
    let activeSprint: SprintResponse | null = null;
    let tasks: TaskResponse[] = [];
    
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

    function handleDeleteTask(taskId: string){

    }

</script>

<div class="sprints-container">
    <!-- Toolbar -->
    <div class="sprints-toolbar">
        <button class="toolbar-btn" on:click={() => isCreateSprintOpen = true}>+ Új sprint</button>
    </div>

    <!-- Aktív sprint -->
    {#if activeSprint}
        <SprintCard
            sprint={activeSprint}
            tasks={tasks.filter(t => t.sprintId === activeSprint!.id)}
            boards={$boardStore.boards}
            onActivate={handleActivateSprint}
            onPlan={handlePlanSprint}
            onEdit={openEditSprint}
            onComplete={openCompleteSprint}
            onDelete={handleDeleteSprint}
            onRemoveTask={handleRemoveFromSprint}
        />
    {/if}

    <!-- Planning sprintek -->
    {#each sprints.filter(s => s.state === 'Planning') as sprint}
        <SprintCard
            {sprint}
            tasks={tasks.filter(t => t.sprintId === sprint.id)}
            boards={$boardStore.boards}
            onActivate={handleActivateSprint}
            onPlan={handlePlanSprint}
            onEdit={openEditSprint}
            onComplete={openCompleteSprint}
            onDelete={handleDeleteSprint}
            onRemoveTask={handleRemoveFromSprint}
        />
    {/each}

    <!-- Completed sprintek -->
    {#each sprints.filter(s => s.state === 'Completed') as sprint}
        <SprintCard
            {sprint}
            tasks={tasks.filter(t => t.sprintId === sprint.id)}
            boards={$boardStore.boards}
            onActivate={handleActivateSprint}
            onPlan={handlePlanSprint}
            onEdit={openEditSprint}
            onComplete={openCompleteSprint}
            onDelete={handleDeleteSprint}
            onRemoveTask={handleRemoveFromSprint}
        />
    {/each}

    <!-- Backlog szekció -->
    <ProjectBacklog
        tasks={tasks}
        sprints={sprints}
        boards={$boardStore.boards}
        projectId={projectId}
        onAssignToSprint={handleAssignToSprint}
        onDelete={handleDeleteTask}
    />

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