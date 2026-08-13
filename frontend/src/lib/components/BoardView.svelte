<script lang="ts">
    import { authStore } from '../stores/authStore';
    import { boardStore, setBoards, setActiveBoard, setColumns } from '../stores/boardStore';
    import { getBoardsAsync, } from '../api/boardApi';
    import { getColumnsAsync } from '../api/columnApi';
    import type { BoardResponse } from '../api/boardApi';
    import type { ColumnResponse } from '../api/columnApi';
    import { getTasksAsync, moveTaskAsync, type TaskResponse } from '../api/taskApi';
    import { setTasks, taskStore, setActiveTask } from '../stores/taskStore';
    import { projectStore } from '../stores/projectStore';
    import { onMount } from 'svelte';
    import { reorderColumnsAsync } from '../api/columnApi';
    import { dndzone } from 'svelte-dnd-action';
    import { sprintStore } from '../stores/sprintStore';
    import type { SprintResponse } from '../api/sprintApi';
    import type { LabelResponse } from '../api/labelApi';
    import { teamStore } from '../stores/teamStore';
    import type { MemberResponse } from '../api/teamApi';

    import ColumnCard from './ColumnCard.svelte';

    import CreateColumnModal from './CreateColumnModal.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import CreateBoardModal from './CreateBoardModal.svelte';
    import UpdateBoardModal from './UpdateBoardModal.svelte';
    import ColumnDetailModal from './ColumnDetailModal.svelte';

    import { ChevronDown, Plus, Pencil, ArrowLeftRight, X, Search } from 'lucide-svelte';

    let isColumnCreationOpen = false;
    let isTaskCreationOpen = false;
    let isTaskDetailOpen = false;
    let isBoardCreationOpen = false;
    let isUpdateBoardOpen = false;
    let isColumnDetailOpen = false;

    let selectedColumn: ColumnResponse | null = null;

    function handleColumnClick(column: ColumnResponse) {
        selectedColumn = column;
        isColumnDetailOpen = true;
    }

    onMount(() => {
        const defaultBoard = boards.find(b => b.isDefault) 
            ?? [...boards].sort((a, b) => a.name.localeCompare(b.name))[0];
        if (defaultBoard) {
            setActiveBoard(defaultBoard);
            distributeTasks(tasks);
        }
    });

    // Szűrő state-ek
    let searchQuery = '';
    let filterAssigneeId = '';
    let filterPriority = '';
    let filterLabelId = '';
    let filterDue = '';

    // Reaktív szűrés
    $: filteredTasks = tasks.filter(task => {
        const matchesSearch = searchQuery === '' || 
            task.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
            task.taskKey.toLowerCase().includes(searchQuery.toLowerCase());
        
        const matchesAssignee = filterAssigneeId === '' || 
            task.assigneeIds.includes(filterAssigneeId);
        
        const matchesPriority = filterPriority === '' || 
            task.priority === filterPriority;
        
        const matchesLabel = filterLabelId === '' || 
            task.labelIds.includes(filterLabelId);
        
        const now = new Date();
        const dueDate = task.dueDate ? new Date(task.dueDate) : null;
        
        const matchesDue = filterDue === '' ? true :
            filterDue === 'overdue' 
                ? dueDate != null && dueDate < now && task.completedAt == null
                : filterDue === 'due-soon'
                    ? dueDate != null && !( dueDate < now) && task.completedAt == null &&
                    (dueDate.getTime() - now.getTime()) < 1 * 24 * 60 * 60 * 1000
                    : true;

        return matchesSearch && matchesAssignee && matchesPriority && matchesLabel && matchesDue;
    });

    $: hasActiveFilter = searchQuery !== '' || filterAssigneeId !== '' || 
        filterPriority !== '' || filterLabelId !== '' || filterDue !== '';

    function clearFilters() {
        searchQuery = '';
        filterAssigneeId = '';
        filterPriority = '';
        filterLabelId = '';
        filterDue = '';
    }

    // Labels és members a szűrőkhöz
    let allLabels: LabelResponse[] = [];
    projectStore.subscribe(state => {
        allLabels = state.labels;
    });

    let members: MemberResponse[] = [];
    teamStore.subscribe(state => {
        members = state.members;
    });
    
    $: {
        columns; //columns változásra is reagáljon
        distributeTasks(filteredTasks);
    }

    let isDraggingColumns = false;
    let isDropdownOpen = false;

    let sprints: SprintResponse[] = [];
    let boards: BoardResponse[] = [];
    let activeBoard: BoardResponse | null = null;
    let activeSprint: SprintResponse | null = null;
    let columns: ColumnResponse[] = [];
    // Csak position > 0 oszlopok láthatók
    $: visibleColumns = columns.filter(c => c.position > 0);
    let tasks: TaskResponse[] = [];

    // Oszloponként külön Map-ben tároljuk a taskokat
    let columnTasks: Record<string, TaskResponse[]> = {};

    // Amikor betöltjük a taskokat, szétválogatjuk oszloponként
    function distributeTasks(allTasks: TaskResponse[]) {
        const map: Record<string, TaskResponse[]> = {};
        const cols = columns.filter(c => c.position > 0);  // ← direkt szűrés
        cols.forEach(col => {
            map[col.id] = allTasks
                .filter(t => t.columnId === col.id)
                .sort((a, b) => a.position.localeCompare(b.position));
        });
        columnTasks = { ...map };
    }
    
    // store figyelése
    sprintStore.subscribe(state => {
        sprints = state.sprints;
        const newActiveSprint = state.activeSprint;
        
        if (newActiveSprint?.id !== activeSprint?.id) {
            activeSprint = newActiveSprint;
            distributeTasks(filteredTasks);
        } else {
            activeSprint = newActiveSprint;
        }
    });

    let currentUserId = '';
    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    let activeProjectId = '';
    projectStore.subscribe(state => {
        activeProjectId = state.activeProject?.id ?? '';
    });

    let isDragging = false;
    taskStore.subscribe(state => {
        tasks = state.tasks;
        //if (!isDragging) {
        //    distributeTasks(tasks);
        //}
    });

    //DND action
    let isReordering = false;
    function handleColumnConsider(e: CustomEvent) {
        isDraggingColumns = true;
        columns = e.detail.items;
    }

    function toggleDropdown() {
        isDropdownOpen = !isDropdownOpen;
    }

    boardStore.subscribe(state => {
        boards = state.boards;
        activeBoard = state.activeBoard;
        if (!isDraggingColumns) {
            columns = state.columns;
        }
    });

    async function handleColumnFinalize(e: CustomEvent) {
        columns = e.detail.items;
        isDraggingColumns = false;
        // Reorder API hívás
        const order = visibleColumns.map((col, index) => ({
            id: col.id,
            position: index + 1,
            rowVersion: col.rowVersion ?? 0
            //Ujradolgozott Sprint logika: backlog oszlop fix 0 position, 
            //és ezt nem jelenítjük meg, így a látható oszlopok 1-es indexel kezdődnek!
        }));
        await reorderColumnsAsync(activeProjectId, activeBoard?.id ?? '', order);
    }

    function handleTaskConsider(e: CustomEvent, columnId: string) {
        isDragging = true;
        columnTasks[columnId] = e.detail.items;
        columnTasks = { ...columnTasks };
    }

    async function handleTaskFinalize(e: CustomEvent, columnId: string) {
        const movedTaskId = e.detail.info.id;
        if (e.detail.info.trigger === 'droppedIntoAnother') return;

        columnTasks[columnId] = e.detail.items;
        Object.keys(columnTasks).forEach(colId => {
            if (colId !== columnId) {
                columnTasks[colId] = columnTasks[colId]
                    .filter((t: TaskResponse) => t.id !== movedTaskId);
            }
        });
        columnTasks = { ...columnTasks };

        const movedIndex = columnTasks[columnId]
            .findIndex((t: TaskResponse) => t.id === movedTaskId);
        const afterTaskId = movedIndex > 0
            ? columnTasks[columnId][movedIndex - 1].id
            : null;

        const movedTask = tasks.find(t => t.id === movedTaskId);

        try {
            const response = await moveTaskAsync(activeProjectId, movedTaskId, {
                columnId,
                afterTaskId,
                rowVersion: movedTask?.rowVersion ?? 0
            });

            // Store frissítés a backend válasszal
            isDragging = false;
            const updatedTasks = tasks.map(t =>
                t.id === movedTaskId ? response : t
            );
            setTasks(updatedTasks);
            
            // Explicit distributeTasks a friss adatokkal
            distributeTasks(updatedTasks);

        } catch (err: any) {
            console.error('Backend hiba:', err.response?.data);
            isDragging = false;
            // Nem kell API újrahívás, a store már naprakész.
            distributeTasks(tasks);
        }
    }
   
    async function loadBoards(board: BoardResponse) {
        setActiveBoard(board);
        distributeTasks(tasks);
    }

    async function loadBoard(board: BoardResponse) {
        setActiveBoard(board);
        distributeTasks(tasks);
    }
 
    function handleTaskClick(task: TaskResponse) {
        setActiveTask(task);
        isTaskDetailOpen = true;
    }
</script>

<div class="board-toolbar">
    <div class="dropdown">
        <button class="toolbar-btn" on:click={toggleDropdown}>
            {activeBoard?.name ?? 'Válassz boardot'} <ChevronDown size={14} />
        </button>
        {#if isDropdownOpen}
            <div class="dropdown-menu">
                {#each boards as board}
                    <button on:click={() => { loadBoard(board); isDropdownOpen = false; }}>
                        {board.name}
                    </button>
                {/each}
                <hr>
                <button on:click={() => { isBoardCreationOpen = true; isDropdownOpen = false; }}>
                    <Plus size={14} /> Új board
                </button>
            </div>
        {/if}
    </div>
    <button class="toolbar-btn" on:click={() => isColumnCreationOpen = true}>
        <Plus size={14} /> Oszlop hozzáadása
    </button>
    <button class="toolbar-btn" on:click={() => isTaskCreationOpen = true}>
        <Plus size={14} /> Task hozzáadása
    </button>
    <button class="toolbar-btn" on:click={() => isUpdateBoardOpen = true}>
        <Pencil size={14} /> Board módosítása
    </button>
    <button class="toolbar-btn" class:active={isReordering}
        on:click={() => isReordering = !isReordering}>
        <ArrowLeftRight size={14} /> {isReordering ? 'Átrendezés aktív' : 'Átrendezés'}
    </button>
</div>

<div class="filter-toolbar">
    <div class="search-wrapper">
        <Search size={14} class="search-icon" />
        <input
            type="text"
            class="search-input"
            placeholder="Keresés..."
            bind:value={searchQuery}
        />
    </div>

    <select class="filter-select" bind:value={filterAssigneeId}>
        <option value="">Összes assignee</option>
        {#each $teamStore.members as member}
            <option value={member.userId}>{member.displayName}</option>
        {/each}
    </select>

    <select class="filter-select" bind:value={filterPriority}>
        <option value="">Összes prioritás</option>
        <option value="low">Alacsony</option>
        <option value="medium">Közepes</option>
        <option value="high">Magas</option>
        <option value="critical">Kritikus</option>
    </select>

    <select class="filter-select" bind:value={filterLabelId}>
        <option value="">Összes label</option>
        {#each allLabels as label}
            <option value={label.id}>{label.name}</option>
        {/each}
    </select>

    <select class="filter-select" bind:value={filterDue}>
        <option value="">Minden határidő</option>
        <option value="overdue">Lejárt</option>
        <option value="due-soon">Hamarosan lejár</option>
    </select>

    {#if hasActiveFilter}
        <button class="clear-btn" on:click={clearFilters}>
            <X size={13} /> Törlés
        </button>
    {/if}
</div>

<div class="board-container">
    <h1>{activeBoard?.name}</h1>
    <div class="columns-container"
        use:dndzone={{
            items: visibleColumns,
            flipDurationMs: 200,
            dragDisabled: !isReordering,
            dropTargetStyle: { outline: '2px dashed #555' }
        }}
        on:consider={handleColumnConsider}
        on:finalize={handleColumnFinalize}
    >
        {#each visibleColumns as column (column.id)}
            <ColumnCard
                {column}
                tasks={columnTasks[column.id] ?? []}
                onConsider={handleTaskConsider}
                onFinalize={handleTaskFinalize}
                onTaskClick={handleTaskClick}
                onColumnClick={handleColumnClick}
                isReordering={isReordering}
            />
        {/each}
    </div>
</div>

<!-- Modals -->
{#if isColumnCreationOpen}
    <CreateColumnModal
        bind:isColumnCreationOpen={isColumnCreationOpen}
        projectId={activeProjectId}
        boardId={activeBoard?.id ?? ''}
        onClose={async () => {
            const cols = await getColumnsAsync(activeProjectId, activeBoard?.id ?? '');
            const sortedCols = cols.sort((a, b) => a.position - b.position);
            setColumns(sortedCols);
        }}
    />
{/if}
{#if isTaskCreationOpen}
    <CreateTaskModal 
        bind:isTaskCreationOpen={isTaskCreationOpen}
        projectId={activeProjectId}
        boardId={activeBoard?.id ?? ''}
        onClose={async () => {
            const _tasks = await getTasksAsync(activeProjectId, activeBoard?.id ?? '')
            const filtered = _tasks.filter(t => !t.closedAt);
            setTasks(filtered);
            distributeTasks(filtered);
        }}
    />
{/if}
{#if isTaskDetailOpen && $taskStore.activeTask}
    <TaskDetailModal
        bind:isTaskDetailOpen={isTaskDetailOpen}
        projectId={activeProjectId}
        task={$taskStore.activeTask!}
        onClose={async () => {
            isTaskDetailOpen = false;
            const _tasks = await getTasksAsync(activeProjectId, activeBoard?.id ?? '')
            const filtered = _tasks.filter(t => !t.closedAt);
            setTasks(filtered);
            distributeTasks(filtered);
            setActiveTask(null);
        }}
    />
{/if}
{#if isBoardCreationOpen}
    <CreateBoardModal
        bind:isBoardCreationOpen={isBoardCreationOpen}
        projectId={activeProjectId}
        activeProject={$projectStore.activeProject!}
        onClose={async () => {
            const data = await getBoardsAsync(activeProjectId);
            setBoards(data);
        }}
    />
{/if}
{#if isUpdateBoardOpen}
    <UpdateBoardModal
        bind:isUpdateBoardOpen={isUpdateBoardOpen}
        projectId={activeProjectId}
        onClose={async () => {
            const data = await getBoardsAsync(activeProjectId);
            setBoards(data);
        }}
    />
{/if}
{#if isColumnDetailOpen && selectedColumn}
    <ColumnDetailModal
        bind:isColumnDetailOpen={isColumnDetailOpen}
        projectId={activeProjectId}
        boardId={activeBoard?.id ?? ''}
        column={selectedColumn}
        onClose={async () => {
            const cols = await getColumnsAsync(activeProjectId, activeBoard?.id ?? '');
            const sortedCols = cols.sort((a, b) => a.position - b.position);
            setColumns(sortedCols);
        }}
    />
{/if}

<style>
    .board-container {
        flex: 1;
        overflow-x: auto;
        overflow-y: hidden;
        padding: 1rem;
        padding-bottom: 1rem;
    }

    .columns-container {
        display: flex;
        gap: 1rem;
        align-items: flex-start;
        height: calc(95vh - 165px);
        min-width: min-content;
    }

    .board-toolbar {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.5rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        position: sticky;
        top: 0;
        z-index: 10;
        width: 100%;
        flex-wrap: wrap;
    }

    .toolbar-btn {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        font-size: 0.85rem;
        transition: background 0.15s, color 0.15s;
        white-space: nowrap;
    }

    .toolbar-btn:hover {
        background: var(--border-hover);
        color: var(--text-primary);
    }

    .toolbar-btn.active {
        background: var(--accent-blue-bg);
        border-color: var(--accent-blue);
        color: var(--accent-blue);
    }

    .dropdown { position: relative; }

    .dropdown-menu {
        position: absolute;
        top: 100%;
        left: 0;
        background: var(--bg-card);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        min-width: 180px;
        z-index: 100;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        box-shadow: 0 4px 12px var(--shadow);
    }

    .dropdown-menu button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.5rem 1rem;
        text-align: left;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
    }

    .dropdown-menu button:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .dropdown-menu hr {
        border-color: var(--border);
        margin: 0;
    }

    .filter-toolbar {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.4rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-wrap: wrap;
    }

    .search-wrapper {
        position: relative;
        display: flex;
        align-items: center;
    }

    .search-wrapper :global(.search-icon) {
        position: absolute;
        left: 0.5rem;
        color: var(--text-muted);
        pointer-events: none;
    }

    .search-input {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.3rem 0.6rem 0.3rem 1.75rem;
        font-size: 0.85rem;
        width: 180px;
    }

    .search-input:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .filter-select {
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-secondary);
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
        cursor: pointer;
    }

    .filter-select:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .clear-btn {
        display: flex;
        align-items: center;
        gap: 0.3rem;
        background: var(--accent-red-bg);
        border: 1px solid var(--accent-red);
        color: var(--accent-red);
        padding: 0.3rem 0.6rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
        transition: background 0.15s;
    }

    .clear-btn:hover { background: var(--accent-red); color: #fff; }
</style>