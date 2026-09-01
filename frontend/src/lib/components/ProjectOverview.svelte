<script lang="ts">
    import type { ProjectResponse } from '../api/projectApi';
    import { taskStore } from '../stores/taskStore';
    import { sprintStore } from '../stores/sprintStore';
    import { authStore } from '../stores/authStore';
    import type { TaskResponse } from '../api/taskApi';
    import type { SprintResponse } from '../api/sprintApi';
    import OverviewStatCard from './OverviewStatCard.svelte';
    import ActivityFeed from './ActivityFeed.svelte';
    import BacklogTaskCard from './BacklogTaskCard.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import { setActiveTask } from '../stores/taskStore';

    import { ClipboardList, CircleAlert, Timer, ChartNoAxesColumn } from 'lucide-svelte';

    export let project: ProjectResponse;
    
    let tasks: TaskResponse[] = [];
    let activeSprint: SprintResponse | null = null;
    let currentUserId = '';
    let displayName = '';

    let isTaskDetailOpen = false;

    taskStore.subscribe(state => {
        tasks = state.tasks.filter(t => !t.closedAt);
    });

    sprintStore.subscribe(state => { activeSprint = state.activeSprint; });

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
        displayName = state.user?.displayName ?? '';
    });

    // Összefoglaló számítások
    $: totalTasks = tasks.length;
    $: completedTasks = tasks.filter(t => t.completedAt != null).length;
    $: inProgressTasks = tasks.filter(t => t.completedAt == null && t.columnId != null).length;
    
    $: overdueTasks = tasks.filter(t =>
        t.dueDate != null &&
        new Date(t.dueDate) < getNow() &&
        t.completedAt == null
    ).length;

    // Sprint progress
    $: sprintTasks = activeSprint
        ? tasks.filter(t => t.sprintId === activeSprint!.id)
        : [];
    $: sprintCompleted = sprintTasks.filter(t => t.completedAt != null).length;
    $: sprintProgress = sprintTasks.length > 0
        ? Math.round((sprintCompleted / sprintTasks.length) * 100)
        : 0;

    // Hozzám rendelt taskok
    $: myTasks = tasks
        .filter(t => t.assigneeIds.includes(currentUserId) && t.closedAt == null)
        .sort((a, b) => {
            // Kész taskok csak a végén
            const aCompleted = a.completedAt != null;
            const bCompleted = b.completedAt != null;
            if (aCompleted && !bCompleted) return 1;
            if (!aCompleted && bCompleted) return -1;

            // Nem kész taskok között: overdue először
            if (!aCompleted && !bCompleted) {
                const aOverdue = a.dueDate && new Date(a.dueDate) < getNow();
                const bOverdue = b.dueDate && new Date(b.dueDate) < getNow();
                if (aOverdue && !bOverdue) return -1;
                if (!aOverdue && bOverdue) return 1;

                // Due soon utána
                const aDueSoon = a.dueDate && !aOverdue;
                const bDueSoon = b.dueDate && !bOverdue;
                if (aDueSoon && !bDueSoon) return -1;
                if (!aDueSoon && bDueSoon) return 1;
            }

            // ABC sorrendben
            return a.title.localeCompare(b.title);
        })
        .slice(0, 8);

    //function getDueStatus(task: TaskResponse): 'overdue' | 'due-soon' | 'normal' | null {
    //    if (!task.dueDate) return null;
    //    const due = new Date(task.dueDate);
    //    if (due < getNow() && !task.completedAt) return 'overdue';
    //    if ((due.getTime() - getNow().getTime()) < 24 * 60 * 60 * 1000 && !task.completedAt) return 'due-soon';
    //    return 'normal';
    //}

    function handleOpenDetail(task: TaskResponse) {
        setActiveTask(task);
        isTaskDetailOpen = true;
    }

    function getNow() {
        return new Date();
    }
</script>

<div class="overview-container">
    <!-- Fejléc -->
    <div class="overview-header">
        <div>
            <h1>Üdv, {displayName}!</h1>
            <p class="project-status">
                {project.name} —
                <span class:archived={project.isArchived} class:active={!project.isArchived}>
                    {project.isArchived ? 'Archivált' : 'Aktív'}
                </span>
            </p>
        </div>
        <div class="project-meta">
            <span class="meta-item">Projekt-kulcs: {project.projKey}</span>
            <span class="meta-item">Tulajdonos: {project.ownerName}</span>
            <span class="meta-item">Létrehozás ideje: {new Date(project.createdAt).toLocaleDateString('hu-HU')}</span>
        </div>
    </div>

    <!-- Stat kártyák -->
    <div class="stat-cards">
        <OverviewStatCard
            icon={ClipboardList}
            title="Összes task"
            value={totalTasks}
            subtitle="{completedTasks} kész / {inProgressTasks} folyamatban"
            color="blue"
        />
        <OverviewStatCard
            icon={CircleAlert}
            title="Lejárt taskok"
            value={overdueTasks}
            subtitle={overdueTasks > 0 ? 'Figyelmet igényel!' : 'Minden rendben!'}
            color={overdueTasks > 0 ? 'red' : 'green'}
        />
        {#if activeSprint}
            <OverviewStatCard
                icon={Timer}
                title={activeSprint.name}
                value="{sprintCompleted}/{sprintTasks.length}"
                subtitle="{sprintProgress}% kész"
                color={sprintProgress === 100 ? 'green' : sprintProgress > 50 ? 'blue' : 'yellow'}
            />
        {:else}
            <OverviewStatCard
                icon={Timer}
                title="Aktív sprint"
                value="Nincs"
                subtitle="Hozz létre egy sprintet!"
                color="default"
            />
        {/if}
    </div>

    <div class="overview-content">
        <!-- Hozzám rendelt taskok -->
        <div class="section tasks-section">
            <h3><ClipboardList size={14} /> Hozzám rendelt taskok ({myTasks.length})</h3>
            {#if myTasks.length === 0}
                <p class="empty">Nincs hozzád rendelt task!</p>
            {:else}
                <div class="tasks-scroll">
                    <div class="my-tasks-list">
                        {#each myTasks as task (task.id)}
                            <BacklogTaskCard
                                {task}
                                boards={[]}
                                sprints={[]}
                                projectId={project.id}
                                showMenu={false}
                                onAssignToSprint={() => {}}
                                onDelete={() => {}}
                                onBoardAssigned={async () => {}}
                                onOpenDetail={handleOpenDetail}
                            />
                        {/each}
                    </div>
                </div>
            {/if}
        </div>

        <!-- Recent Activity -->
        <div class="section activity-section">
            <h3><ChartNoAxesColumn size={14} /> Recent Activity</h3>
            <div class="activity-scroll">
                <ActivityFeed projectId={project.id} />
            </div>
        </div>
    </div>
</div>

{#if isTaskDetailOpen && $taskStore.activeTask}
    <TaskDetailModal
        bind:isTaskDetailOpen={isTaskDetailOpen}
        projectId={project.id}
        task={$taskStore.activeTask}
        onClose={() => {
            isTaskDetailOpen = false;
            setActiveTask(null);
        }}
    />
{/if}

<style>
    .overview-container {
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        padding: var(--content-padding);
        overflow-y: auto;
        height: 100%;
    }

    .overview-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        flex-wrap: wrap;
        gap: 1rem;
    }

    h1 {
        font-size: 1.5rem;
        margin: 0;
    }

    .project-status {
        color: var(--text-secondary);
        margin: 0.25rem 0 0;
        font-size: 0.95rem;
    }

    .active   { color: var(--accent-green); font-weight: bold; }
    .archived { color: var(--accent-yellow); font-weight: bold; }

    .project-meta {
        display: flex;
        gap: 1rem;
        flex-wrap: wrap;
        align-items: center;
    }

    .meta-item {
        font-size: 0.85rem;
        background: var(--bg-hover);
        color: var(--text-secondary);
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
    }

    .stat-cards {
        display: flex;
        gap: 1rem;
        flex-wrap: wrap;
    }

    .overview-content {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1.5rem;
    }

    @media (max-width: 480px) {
        .overview-content {
            grid-template-columns: 1fr;
        }
    }

    .section h3 {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        font-size: 0.9rem;
        color: var(--text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin: 0 0 0.75rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0.5rem;
    }

    .my-tasks-list {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    .section.tasks-section {
        display: flex;
        flex-direction: column;
        max-height: 500px;
        overflow: hidden;
    }

    .tasks-scroll {
        overflow-y: auto;
        flex: 1;
    }

    .section.activity-section {
        display: flex;
        flex-direction: column;
        max-height: 500px;
        overflow: hidden;
    }

    .activity-scroll {
        overflow-y: auto;
        flex: 1;
    }

    .empty {
        color: var(--text-muted);
        font-size: 0.9rem;
        text-align: center;
        padding: 1rem;
    }
</style>