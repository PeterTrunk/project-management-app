<script lang="ts">
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

    import CreateColumnModal from './CreateColumnModal.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    let isColumnCreationOpen = false;
    let isTaskCreationOpen = false;
    let isTaskDetailOpen = false;

    onMount(() => {
        if (activeProjectId) {
            loadBoards(activeProjectId);
        }
    });

    let boards: BoardResponse[] = [];
    let activeBoard: BoardResponse | null = null;
    let columns: ColumnResponse[] = [];
    let tasks: TaskResponse[] = [];

    // Oszloponként külön Map-ben tároljuk a taskokat
    let columnTasks: Record<string, TaskResponse[]> = {};

    // Amikor betöltjük a taskokat, szétválogatjuk oszloponként
    function distributeTasks(allTasks: TaskResponse[]) {
        const map: Record<string, TaskResponse[]> = {};
        columns.forEach(col => {
            map[col.id] = allTasks
                .filter(t => t.columnId === col.id)
                .sort((a, b) => a.position - b.position);
        });
        columnTasks = { ...map };  // spread hogy Svelte észrevegye
    }
    
    // store figyelése
    boardStore.subscribe(state => {
        boards = state.boards;
        activeBoard = state.activeBoard;
        columns = state.columns;
        distributeTasks(tasks);
    });

    let activeProjectId = '';
    projectStore.subscribe(state => {
        activeProjectId = state.activeProject?.id ?? '';
    });

    let isDragging = false;
    taskStore.subscribe(state => {
        tasks = state.tasks;
        if (!isDragging) {
            distributeTasks(tasks);
        }
    });

    let isDropdownOpen = false;

    function toggleDropdown() {
        isDropdownOpen = !isDropdownOpen;
    }

    //DND action
    let isReordering = false;
    function handleColumnConsider(e: CustomEvent) {
        columns = e.detail.items;
    }

    async function handleColumnFinalize(e: CustomEvent) {
        columns = e.detail.items;
        // Reorder API hívás
        const order = columns.map((col, index) => ({
            id: col.id,
            position: index
        }));
        await reorderColumnsAsync(activeProjectId, activeBoard?.id ?? '', order);
        setColumns(columns);
    }

    function handleTaskConsider(e: CustomEvent, columnId: string) {
        isDragging = true;
        columnTasks[columnId] = e.detail.items;
        columnTasks = { ...columnTasks };
    }

    async function handleTaskFinalize(e: CustomEvent, columnId: string) {
        const trigger = e.detail.info.trigger;
    
        // Csak a céloszlopban kezeljük a finalize-t
        if (trigger === 'droppedIntoAnother') return;
        
        const movedTaskId = e.detail.info.id;
        
        columnTasks[columnId] = e.detail.items;
        Object.keys(columnTasks).forEach(colId => {
            if (colId !== columnId) {
                columnTasks[colId] = columnTasks[colId].filter((t: TaskResponse) => t.id !== movedTaskId);
            }
        });
        columnTasks = { ...columnTasks };

        const items = columnTasks[columnId];
        const movedIndex = items.findIndex((t: TaskResponse) => t.id === movedTaskId);

        let position: number;

        if (items.length === 1) {
            position = 1;
        } else if (movedIndex === 0) {
            position = items[1].position / 2;
        } else if (movedIndex === items.length - 1) {
            position = items[movedIndex - 1].position + 1;
        } else {
            const before = items[movedIndex - 1].position;
            const after = items[movedIndex + 1].position;
            position = (before + after) / 2;
        }
        try {
            //console.log('Kiszámolt pozició:', position);
            await moveTaskAsync(activeProjectId, movedTaskId, { columnId, position });
            const updatedTasks = tasks.map(t => 
                t.id === movedTaskId ? { ...t, columnId, position } : t
            );
            isDragging = false;
            setTasks(updatedTasks);
        } catch (err: any) {
            console.error('Backend hiba:');
            //console.error('Backend hiba:', err.response?.data*);
            //console.error('Küldött adat:', { columnId, position });
            //console.error('Backend hiba részletek:', JSON.stringify(err.response?.data?.errors));
            isDragging = false;
            const _tasks = await getTasksAsync(activeProjectId, activeBoard?.id ?? '');
            setTasks(_tasks);
        }
    }
   

    async function loadBoards(projectId: string) {
        try {
            const data = await getBoardsAsync(projectId);
            setBoards(data);
            
            // Default board keresése, ha nincs akkor ABC szerint az első betöltése egyből.
            const defaultBoard = data.find(b => b.isDefault) 
                ?? data.sort((a, b) => a.name.localeCompare(b.name))[0];
            
            if (defaultBoard) {
                await loadBoard(defaultBoard);
            }
        } catch (e) {
            console.error('Hiba a boardok lekérésekor!');
        }
    }

    async function loadBoard(board: BoardResponse) {
        setActiveBoard(board);
        try {
            const cols = await getColumnsAsync(activeProjectId, board.id);
            const sortedCols = cols.sort((a, b) => a.position - b.position);
            setColumns(sortedCols);
            const _tasks = await getTasksAsync(activeProjectId, board.id);
            setTasks(_tasks);
        } catch (e) {
            console.error('Hiba az oszlopok/taskok lekérésekor!');
        }
    }

    async function handleUpdate() {
        
    }

    async function handleColAdd() {
        
    }

    async function handleNewBoard() {
        
    }

    function handleTaskClick(task: TaskResponse) {
        setActiveTask(task);
        isTaskDetailOpen = true;
    }

</script>

<div class="board-toolbar">
    <!-- Board választó ha több board van, + new board létrehozás -->
    <div class="dropdown">
        <button on:click={toggleDropdown}>
            {activeBoard?.name ?? 'Válassz boardot'} ▼
        </button>
        {#if isDropdownOpen}
            <div class="dropdown-menu">
                {#each boards as board}
                    <button on:click={() => { loadBoard(board); isDropdownOpen = false; }}>
                        {board.name}
                    </button>
                {/each}
                <hr>
                <button on:click={() => {handleNewBoard}}>+ Új board</button>
            </div>
        {/if}
    </div>
    <button class="toolbar-btn" on:click={() => isColumnCreationOpen = true}>+ Oszlop hozzáadása</button>
    <button class="toolbar-btn" on:click={() => isTaskCreationOpen = true}>+ Task hozzáadása</button>
    <button class="toolbar-btn" on:click={() => {handleUpdate()}}>Board módosítása</button>
    <button 
        class="toolbar-btn" 
        class:active={isReordering}
        on:click={() => isReordering = !isReordering}
    > {isReordering ? '🔓 Átrendezés aktív' : '🔒 Átrendezés'} </button>
    
</div>
<div class="board-container">
    <h2>{activeBoard?.name}</h2>
    <!-- Oszlopok -->
    <div class="columns-container" 
            use:dndzone={{
            items: columns, 
            flipDurationMs: 200, 
            dragDisabled: !isReordering,
            dropTargetStyle: { outline: '2px dashed #555' }
        }}
        on:consider={handleColumnConsider}
        on:finalize={handleColumnFinalize}
    >
        {#each columns as column (column.id)}
            <div class="column">
                <h3>{column.name}</h3>
                <!-- Task kártyák -->
                <div class="task-list"
                    use:dndzone={{
                        items: columnTasks[column.id] ?? [], 
                        flipDurationMs: 200, 
                        type: 'task',
                        dropTargetStyle: { outline: '2px dashed #555' }
                    }}
                    on:consider={(e) => handleTaskConsider(e, column.id)}
                    on:finalize={(e) => handleTaskFinalize(e, column.id)}
                >
                    {#each columnTasks[column.id] ?? [] as task (task.id)}
                        <div class="task-card" on:click={() => handleTaskClick(task)}>
                            <div class="task-header">
                                <p class="task-key">{task.taskKey}</p>
                                {#if task.priority}
                                    <span class="priority priority-{task.priority}">{task.priority}</span>
                                {/if}
                            </div>
                            <p class="task-title">{task.title}</p>
                            {#if task.dueDate}
                                <span class="due-date">Határidő: {new Date(task.dueDate).toLocaleDateString('hu-HU')}</span>
                            {/if}
                        </div>
                        {:else}
                        <div class="empty-column-placeholder">
                            Húzz ide egy taskot
                        </div>
                    {/each}
                </div>
            </div>
        {/each}
    </div>
    
</div>
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
            setTasks(_tasks);
        }}
    />
{/if}
{#if isTaskDetailOpen && $taskStore.activeTask}
    <TaskDetailModal
        bind:isTaskDetailOpen={isTaskDetailOpen}
        projectId={activeProjectId}
        task={$taskStore.activeTask!}
        onClose={() => {
            isTaskDetailOpen = false;
            setActiveTask(null);
        }}
    />
{/if}

<style>
    .board-toolbar {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.5rem 1rem;
        background: #1a1a1a;
        border-bottom: 1px solid #333;
        position: relative;
    }

    .toolbar-btn {
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        background: #2a2a2a;
        border: 1px solid #444;
        color: white;
        font-size: 0.9rem;
    }

    .toolbar-btn:hover {
        background: #333;
    }

    .dropdown {
        position: relative;
    }

    .dropdown-menu {
        position: absolute;
        top: 100%;
        left: 0;
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        min-width: 180px;
        z-index: 100;
        display: flex;
        flex-direction: column;
        overflow: hidden;
    }

    .dropdown-menu button {
        padding: 0.5rem 1rem;
        text-align: left;
        background: transparent;
        border: none;
        color: white;
        cursor: pointer;
        font-size: 0.9rem;
    }

    .dropdown-menu button:hover {
        background: #333;
    }

    .dropdown-menu hr {
        border-color: #444;
        margin: 0;
    }

    .board-container {
        flex: 1;
        overflow-x: auto;
        padding: 1rem;
    }

    .columns-container {
        display: flex;
        gap: 1rem;
        align-items: flex-start;
        height: 100%;
    }

    .column {
        background: #1e1e1e;
        border-radius: 8px;
        padding: 1rem;
        min-width: 250px;
        border: 1px solid #333;
        min-height: calc(100vh - 200px);  /* mindig leér a képernyő aljáig */
        display: flex;
        flex-direction: column;
    }
    
    .column h3 {
        margin-bottom: 0.5rem;
        font-size: 1rem;
        color: #ccc;
    }

    .task-card {
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem;
        margin-bottom: 0.5rem;
        border: 1px solid #333;
        cursor: pointer;
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
    }

    .task-list {
        flex: 1;
        min-height: 80px;  /* üres oszlopba is lehet húzni */
    }

    .task-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
    }

    .empty-column-placeholder {
        color: #555;
        text-align: center;
        padding: 1rem;
        font-size: 0.85rem;
        pointer-events: none;
    }

    .task-card:hover {
        border-color: #555;
    }

    .task-key {
        font-size: 0.75rem;
        color: #888;
    }

    .task-title {
        font-size: 0.9rem;
    }

    .priority {
        font-size: 0.75rem;
        padding: 0.2rem 0.5rem;
        border-radius: 4px;
        width: fit-content;
    }

    .priority-low { background: #1a3a1a; color: #4caf50; }
    .priority-medium { background: #3a3a1a; color: #ffeb3b; }
    .priority-high { background: #3a1a1a; color: #ff5722; }
    .priority-critical { background: #4a0000; color: #ff0000; }
</style>