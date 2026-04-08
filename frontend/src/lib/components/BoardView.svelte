<script lang="ts">
    import { onDestroy } from 'svelte';
    import { signalRService } from '../services/signalRService';
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
    import { sprintStore, setSprints } from '../stores/sprintStore';
    import { getSprintsAsync } from '../api/sprintApi';
    import type { SprintResponse } from '../api/sprintApi';
    import { getTaskByIdAsync } from '../api/taskApi';

    import ColumnCard from './ColumnCard.svelte';

    import CreateColumnModal from './CreateColumnModal.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    import TaskDetailModal from './TaskDetailModal.svelte';
    import CreateBoardModal from './CreateBoardModal.svelte';
    import UpdateBoardModal from './UpdateBoardModal.svelte';
    import ColumnDetailModal from './ColumnDetailModal.svelte';

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

    onMount(async () => {
        if (activeProjectId) {
            // Először sprintek betöltése
            const sprintData =  await getSprintsAsync(activeProjectId);
            setSprints(sprintData);
            // Utána boardok
            await loadBoards(activeProjectId);
        }
    });

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
    boardStore.subscribe(state => {
        boards = state.boards;
        activeBoard = state.activeBoard;
        columns = state.columns;
        distributeTasks(tasks);
    });

    sprintStore.subscribe(state => {
        sprints = state.sprints;
        activeSprint = state.activeSprint;
    });

    let currentUserId = '';
    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    let activeProjectId = '';
    projectStore.subscribe(state => {
        const newProjectId = state.activeProject?.id ?? '';
        if (newProjectId !== activeProjectId) {
            activeProjectId = newProjectId;
            if (activeProjectId) {
                loadBoards(activeProjectId);
            }
        }
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
        const order = visibleColumns.map((col, index) => ({
            id: col.id,
            position: index + 1 
            //Ujradolgozott Sprint logika: backlog oszlop fix 0 position, 
            //és ezt nem jelenítjük meg, így a látható oszlopok 1-es indexel kezdődnek!
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

        try {
            const response = await moveTaskAsync(activeProjectId, movedTaskId, {
                columnId,
                afterTaskId
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
            const _tasks = await getTasksAsync(activeProjectId, activeBoard?.id ?? '');
            setTasks(_tasks);
            distributeTasks(_tasks);
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
        //Előző elhagyása, ha volt
        if (activeBoard) {
            await signalRService.leaveBoard(activeBoard.id);
        }

        setActiveBoard(board);

        //Csatlakozás egy másikhoz
        await signalRService.joinBoard(board.id);
        try {
            const cols = await getColumnsAsync(activeProjectId, board.id);
            const sortedCols = cols.sort((a, b) => a.position - b.position);
            setColumns(sortedCols);

            // Friss sprint adat lekérése
            let currentActiveSprint: SprintResponse | null = null;
            const unsubscribe = sprintStore.subscribe(state => {
                currentActiveSprint = state.activeSprint;
            });
            unsubscribe();

            const activeSprintId = (currentActiveSprint as SprintResponse | null)?.id ?? undefined;
            
            // Csak aktív sprint taskjai ha van aktív sprint
            const _tasks = await getTasksAsync(
                activeProjectId, 
                board.id,
                activeSprintId ?? undefined, //  sprintId szűrés
            );

            //Lezárt taskok kiszűrése.
            const filteredTasks = _tasks.filter(t => !t.closedAt);
            setTasks(filteredTasks);

        } catch (e) {
            console.error('Hiba az oszlopok/taskok lekérésekor!');
        }
        // SignalR események regisztrálása
        registerSignalREvents();
    }

    function registerSignalREvents() {
        // Előző események törlése
        signalRService.off('TaskMoved');
        signalRService.off('TaskCreated');
        signalRService.off('TaskUpdated');
        signalRService.off('TaskDeleted');
        signalRService.off('TasksRebalanced');
        signalRService.off('ColumnCreated');
        signalRService.off('ColumnUpdated');
        signalRService.off('ColumnDeleted');
        signalRService.off('ColumnsReordered');
        signalRService.off('BoardCreated');
        signalRService.off('BoardUpdated');
        signalRService.off('BoardDeleted');
        signalRService.off('SprintUpdated');
        signalRService.off('TaskLabelAdded');
        signalRService.off('TaskLabelRemoved');

        signalRService.on('TaskLabelAdded', async (data) => {
            let activeTask: TaskResponse | null = null;
            taskStore.subscribe(state => { activeTask = state.activeTask; })();
            
            if ((activeTask as TaskResponse | null)?.id === data.taskId) return;
            
            const updatedTask = await getTaskByIdAsync(activeProjectId, data.taskId);
            let currentTasks: TaskResponse[] = [];
            taskStore.subscribe(state => { currentTasks = state.tasks; })();
            const updated = currentTasks.map(t => t.id === data.taskId ? { ...updatedTask } : t);
            setTasks([...updated]);
            distributeTasks([...updated]);
        });

        signalRService.on('TaskLabelRemoved', async (data) => {
            let activeTask: TaskResponse | null = null;
            taskStore.subscribe(state => { activeTask = state.activeTask as TaskResponse | null; })();
            
            if ((activeTask as TaskResponse | null)?.id === data.taskId) return;
            
            const updatedTask = await getTaskByIdAsync(activeProjectId, data.taskId);
            let currentTasks: TaskResponse[] = [];
            taskStore.subscribe(state => { currentTasks = state.tasks; })();
            const updated = currentTasks.map(t => t.id === data.taskId ? { ...updatedTask } : t);
            setTasks([...updated]);
            distributeTasks([...updated]);
        });

        signalRService.on('SprintUpdated', async (data) => {
            // Sprint store frissítése
            const sprintData = await getSprintsAsync(activeProjectId);
            setSprints(sprintData);
            
            // Taskok újratöltése az új aktív sprint alapján
            if (activeBoard) {
                await loadBoard(activeBoard);
            }
        });
        
        signalRService.on('TaskMoved', (data) => {
            // Store közvetlen olvasása
            let currentTasks: TaskResponse[] = [];
            taskStore.subscribe(state => {
                currentTasks = state.tasks;
            })();
            
            const updatedTasks = currentTasks.map((t: TaskResponse) =>
                t.id === data.taskId 
                    ? { ...t, columnId: data.columnId, position: data.position }
                    : t
            );
            setTasks(updatedTasks);
            distributeTasks(updatedTasks);
        });

        signalRService.on('TaskCreated', (data) => {
            if (data.boardId !== activeBoard?.id) return;
            // Teljes újratöltés mert a teljes DTO kell
            getTasksAsync(activeProjectId, activeBoard?.id, activeSprint?.id ?? undefined)
                .then(t => { setTasks(t); distributeTasks(t); });
        });

        signalRService.on('TaskUpdated', (data) => {
            const updatedTasks = tasks.map((t: TaskResponse) =>
                t.id === data.taskId ? { ...t, ...data } : t
            );
            setTasks(updatedTasks);
            distributeTasks(updatedTasks);
        });

        signalRService.on('TaskDeleted', (data) => {
            const updatedTasks = tasks.filter((t: TaskResponse) => t.id !== data.taskId);
            setTasks(updatedTasks);
            distributeTasks(updatedTasks);
        });

        signalRService.on('ColumnCreated', async () => {
            const cols = await getColumnsAsync(activeProjectId, activeBoard?.id ?? '');
            setColumns(cols.sort((a, b) => a.position - b.position));
        });

        signalRService.on('ColumnUpdated', async () => {
            const cols = await getColumnsAsync(activeProjectId, activeBoard?.id ?? '');
            setColumns(cols.sort((a, b) => a.position - b.position));
        });

        signalRService.on('ColumnDeleted', async () => {
            const cols = await getColumnsAsync(activeProjectId, activeBoard?.id ?? '');
            setColumns(cols.sort((a, b) => a.position - b.position));
        });

        signalRService.on('ColumnsReordered', (data) => {
            let currentCols: ColumnResponse[] = [];
            boardStore.subscribe(state => { currentCols = state.columns; })();
            
            const updated = currentCols.map(c => {
                const found = data.columns.find((d: any) => d.id === c.id);
                return found ? { ...c, position: found.position } : c;
            }).sort((a, b) => a.position - b.position);
            
            setColumns(updated);
        });

        signalRService.on('TasksRebalanced', (data) => {
            let currentTasks: TaskResponse[] = [];
            taskStore.subscribe(state => { currentTasks = state.tasks; })();
            
            const updated = currentTasks.map(t => {
                const found = data.tasks.find((d: any) => d.id === t.id);
                return found ? { ...t, position: found.position } : t;
            });
            setTasks(updated);
            distributeTasks(updated);
        });

        signalRService.on('BoardCreated', async () => {
            const data = await getBoardsAsync(activeProjectId);
            setBoards(data);
        });

        signalRService.on('BoardUpdated', async () => {
            const data = await getBoardsAsync(activeProjectId);
            setBoards(data);
            
            if ( data.find(b => b.id === activeBoard?.id) ) {
                const updatedBoard = data.find(b=> b.id === activeBoard?.id);
                if (updatedBoard) {
                    setActiveBoard(updatedBoard);
                }
            }
        });

        signalRService.on('BoardDeleted', async () => {
            const data = await getBoardsAsync(activeProjectId);
            setBoards(data);
            // Ha az aktív board lett törölve akkor az első boardot töltjük be
            if (!boards.find(b => b.id === activeBoard?.id)) {
                await loadBoards(activeProjectId);
            }
        });
    }

    onDestroy(async () => {
        if (activeBoard) {
            await signalRService.leaveBoard(activeBoard.id);
        }
        signalRService.off('TaskMoved');
        signalRService.off('TaskCreated');
        signalRService.off('TaskUpdated');
        signalRService.off('TaskDeleted');
        signalRService.off('TasksRebalanced');
        signalRService.off('ColumnCreated');
        signalRService.off('ColumnUpdated');
        signalRService.off('ColumnDeleted');
        signalRService.off('ColumnsReordered');
        signalRService.off('BoardCreated');
        signalRService.off('BoardUpdated');
        signalRService.off('BoardDeleted');
        signalRService.off('SprintUpdated');
        signalRService.off('TaskLabelAdded');
        signalRService.off('TaskLabelRemoved');
    });
 
    function handleTaskClick(task: TaskResponse) {
        setActiveTask(task);
        isTaskDetailOpen = true;
    }
</script>

<div class="board-toolbar">
    <!-- Board választó ha több board van, + new board létrehozás -->
    <div class="dropdown">
        <button class="toolbar-btn" on:click={toggleDropdown}>
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
                <button on:click={() => { isBoardCreationOpen = true; isDropdownOpen = false; }}>+ Új board</button>
            </div>
        {/if}
    </div>
    <button class="toolbar-btn" on:click={() => isColumnCreationOpen = true}>+ Oszlop hozzáadása</button>
    <button class="toolbar-btn" on:click={() => isTaskCreationOpen = true}>+ Task hozzáadása</button>
    <button class="toolbar-btn" on:click={() => isUpdateBoardOpen = true}>Board módosítása</button>
    <button 
        class="toolbar-btn" 
        class:active={isReordering}
        on:click={() => isReordering = !isReordering}
    > {isReordering ? 'Átrendezés aktív' : 'Átrendezés'} </button>
    
</div>
<div class="board-container">
    <h2>{activeBoard?.name}</h2>
    <!-- Oszlopok -->
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
        height: calc(100vh - 165px);  /* kicsit több hely alul */
        min-width: min-content;  /* width: max-content helyett */
    }

    .board-toolbar {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.5rem 1rem;
        background: #1a1a1a;
        border-bottom: 1px solid #333;
        position: sticky;
        top: 0;
        z-index: 10;
        width: 100%;
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

    
    
</style>