<script lang="ts">
    import { boardStore, setBoards, setActiveBoard, setColumns } from '../stores/boardStore';
    import { getBoardsAsync, } from '../api/boardApi';
    import { getColumnsAsync } from '../api/columnApi';
    import type { BoardResponse } from '../api/boardApi';
    import type { ColumnResponse } from '../api/columnApi';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import { setTasks, taskStore } from '../stores/taskStore';
    import { projectStore } from '../stores/projectStore';
    import { onMount } from 'svelte';

    import CreateColumnModal from './CreateColumnModal.svelte';
    import CreateTaskModal from './CreateTaskModal.svelte';
    let isColumnCreationOpen = false;
    let isTaskCreationOpen = false;

    onMount(() => {
        if (activeProjectId) {
            loadBoards(activeProjectId);
        }
    });

    let boards: BoardResponse[] = [];
    let activeBoard: BoardResponse | null = null;
    let columns: ColumnResponse[] = [];
    let tasks: TaskResponse[] = [];

    // store figyelése
    boardStore.subscribe(state => {
        boards = state.boards;
        activeBoard = state.activeBoard;
        columns = state.columns;
    });

    let activeProjectId = '';
    projectStore.subscribe(state => {
        activeProjectId = state.activeProject?.id ?? '';
    });

    taskStore.subscribe(state => {
        tasks = state.tasks;
    });

    let isDropdownOpen = false;

    function toggleDropdown() {
        isDropdownOpen = !isDropdownOpen;
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
            setColumns(cols);
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
</div>
<div class="board-container">
    <h2>{activeBoard?.name}</h2>
    <!-- Oszlopok -->
    <div class="columns-container">
        {#each columns as column}
            <div class="column">
                <h3>{column.name}</h3>
                <!-- task kártyák ide jönnek -->
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
            setColumns(cols);
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
    }
    
    .column h3 {
        margin-bottom: 0.5rem;
        font-size: 1rem;
        color: #ccc;
    }
</style>