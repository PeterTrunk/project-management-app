<script lang="ts">
    import { onMount } from 'svelte';
    import { sprintStore } from '../stores/sprintStore';
    import type { SprintResponse } from '../api/sprintApi';
    import {
        getTaskStatusDistributionAsync,
        getBurndownAsync,
        getWorkloadAsync,
        getVelocityAsync,
        getCumulativeFlowAsync,
        type TaskStatusDistribution,
        type BurndownDataPoint,
        type WorkloadDataPoint,
        type VelocityDataPoint,
        type CumulativeFlowDataPoint
    } from '../api/statisticsApi';

    import TaskStatusPieChart from './TaskStatusPieChart.svelte';
    import SprintBurndownChart from './SprintBurndownChart.svelte';
    import TeamWorkloadChart from './TeamWorkloadChart.svelte';
    import VelocityChart from './VelocityChart.svelte';
    import CumulativeFlowChart from './CumulativeFlowChart.svelte';
    import type { BoardResponse } from '../api/boardApi';
    import { boardStore } from '../stores/boardStore';

    export let projectId: string;

    // Szűrők
    let selectedSprintId: string = '';
    let burndownMode: 'burndown' | 'burnup' = 'burndown';
    let dateFrom: string = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)
        .toISOString().split('T')[0];
    let dateTo: string = new Date().toISOString().split('T')[0];

    // Adatok
    let taskStatusData: TaskStatusDistribution[] = [];
    let burndownData: BurndownDataPoint[] = [];
    let workloadData: WorkloadDataPoint[] = [];
    let velocityData: VelocityDataPoint[] = [];
    let cumulativeFlowData: CumulativeFlowDataPoint[] = [];

    // Loading states
    let loadingStatus = false;
    let loadingBurndown = false;
    let loadingWorkload = false;
    let loadingVelocity = false;
    let loadingCFD = false;
    let selectedBoardId: string ='';

    let sprints: SprintResponse[] = [];
    sprintStore.subscribe(state => {
        sprints = state.sprints;
    });

    let boards: BoardResponse[] = [];
    boardStore.subscribe(state => {
        boards = state.boards;
    });

    onMount(async () => {
        await loadAll();
    });

    async function loadAll() {
        await Promise.all([
            loadTaskStatus(),
            loadWorkload(),
            loadVelocity(),
            loadCumulativeFlow()
        ]);
    }

    async function loadTaskStatus() {
        loadingStatus = true;
        try {
            taskStatusData = await getTaskStatusDistributionAsync(
                projectId,
                selectedSprintId || undefined
            );
        } catch (e) {
            console.error('Hiba a task státusz lekérésekor!');
        } finally {
            loadingStatus = false;
        }
    }

    async function loadBurndown() {
        if (!selectedSprintId) return;
        loadingBurndown = true;
        try {
            burndownData = await getBurndownAsync(projectId, selectedSprintId);
        } catch (e) {
            console.error('Hiba a burndown lekérésekor!');
        } finally {
            loadingBurndown = false;
        }
    }

    async function loadWorkload() {
        loadingWorkload = true;
        try {
            workloadData = await getWorkloadAsync(
                projectId,
                selectedSprintId || undefined
            );
        } catch (e) {
            console.error('Hiba a workload lekérésekor!');
        } finally {
            loadingWorkload = false;
        }
    }

    async function loadVelocity() {
        loadingVelocity = true;
        try {
            velocityData = await getVelocityAsync(projectId);
        } catch (e) {
            console.error('Hiba a velocity lekérésekor!');
        } finally {
            loadingVelocity = false;
        }
    }

    async function loadCumulativeFlow() {
        loadingCFD = true;
        try {
            console.log('CFD params:', { projectId, dateFrom, dateTo, boardId: selectedBoardId });
            cumulativeFlowData = await getCumulativeFlowAsync(
                projectId, dateFrom, dateTo, selectedBoardId
            );
            console.log('CFD data:', cumulativeFlowData);
        } catch (e) {
            console.error('Hiba a CFD lekérésekor!');
        } finally {
            loadingCFD = false;
        }
    }

    async function handleSprintChange() {
        await Promise.all([
            loadTaskStatus(),
            loadBurndown(),
            loadWorkload()
        ]);
    }

    async function handleDateChange() {
        await loadCumulativeFlow();
    }
</script>

<div class="statistics-container">
    <!-- Toolbar -->
    <div class="statistics-toolbar">
        <h2>Statisztikák</h2>
        <div class="filters">
            <div class="filter-group">
                <label for="sprintFilter">Sprint:</label>
                <select 
                    id="sprintFilter"
                    bind:value={selectedSprintId}
                    on:change={handleSprintChange}>
                    <option value="">Összes sprint</option>
                    {#each sprints as sprint}
                        <option value={sprint.id}>{sprint.name}</option>
                    {/each}
                </select>
            </div>
        </div>
    </div>

    <div class="statistics-content">
        <!-- Első sor: Task státusz + Workload -->
        <div class="charts-row">
            <div class="chart-card">
                {#if loadingStatus}
                    <div class="loading">Betöltés...</div>
                {:else if taskStatusData.length === 0}
                    <div class="empty">Nincs adat</div>
                {:else}
                    <TaskStatusPieChart data={taskStatusData} />
                {/if}
            </div>

            <div class="chart-card">
                {#if loadingWorkload}
                    <div class="loading">Betöltés...</div>
                {:else if workloadData.length === 0}
                    <div class="empty">Nincs hozzárendelt task</div>
                {:else}
                    <TeamWorkloadChart data={workloadData} />
                {/if}
            </div>
        </div>

        <!-- Második sor: Burndown/Burnup -->
        <div class="chart-card full-width">
            <div class="chart-header">
                <div class="mode-toggle">
                    <button
                        class:active={burndownMode === 'burndown'}
                        on:click={() => burndownMode = 'burndown'}>
                        Burndown
                    </button>
                    <button
                        class:active={burndownMode === 'burnup'}
                        on:click={() => burndownMode = 'burnup'}>
                        Burnup
                    </button>
                </div>
            </div>
            {#if !selectedSprintId}
                <div class="empty">Válassz egy sprintet a burndown/burnup megjelenítéséhez!</div>
            {:else if loadingBurndown}
                <div class="loading">Betöltés...</div>
            {:else if burndownData.length === 0}
                <div class="empty">Nincs adat</div>
            {:else}
                <SprintBurndownChart data={burndownData} mode={burndownMode} />
            {/if}
        </div>

        <!-- Harmadik sor: Velocity -->
        <div class="chart-card full-width">
            {#if loadingVelocity}
                <div class="loading">Betöltés...</div>
            {:else if velocityData.length === 0}
                <div class="empty">Nincs befejezett sprint</div>
            {:else}
                <VelocityChart data={velocityData} />
            {/if}
        </div>

        <!-- Negyedik sor: CFD -->
        <div class="chart-card full-width">
            <div class="chart-header">
                <div class="date-filters">
                    <label for="dateFrom">Tól:</label>
                    <input
                        id="dateFrom"
                        type="date"
                        bind:value={dateFrom}
                        on:change={handleDateChange}
                    />
                    <label for="dateTo">Ig:</label>
                    <input
                        id="dateTo"
                        type="date"
                        bind:value={dateTo}
                        on:change={handleDateChange}
                    />
                    <select bind:value={selectedBoardId} on:change={loadCumulativeFlow}>
                        <option value="">Összes board</option>
                        {#each boards as board}
                            <option value={board.id}>{board.name}</option>
                        {/each}
                    </select>
                </div>
            </div>
            {#if loadingCFD}
                <div class="loading">Betöltés...</div>
            {:else if cumulativeFlowData.length === 0}
                <div class="empty">Nincs adat a megadott intervallumra</div>
            {:else}
                <CumulativeFlowChart data={cumulativeFlowData} />
            {/if}
        </div>
    </div>
</div>

<style>
    .statistics-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow: hidden;
    }

    .statistics-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
        gap: 1rem;
        flex-wrap: wrap;
    }

    .statistics-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: var(--text-secondary);
    }

    .filters {
        display: flex;
        gap: 1rem;
        align-items: center;
        flex-wrap: wrap;
    }

    .filter-group {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .filter-group label {
        font-size: 0.85rem;
        color: var(--text-muted);
    }

    select, input[type="date"] {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.3rem 0.5rem;
        font-size: 0.85rem;
    }

    select:focus, input[type="date"]:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .statistics-content {
        padding: 1rem;
        overflow-y: auto;
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .charts-row {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1rem;
    }

    .chart-card {
        background: var(--bg-card);
        border: 1px solid var(--border);
        border-radius: 8px;
        padding: 1rem;
    }

    .chart-card.full-width {
        width: 100%;
    }

    .chart-header {
        display: flex;
        justify-content: flex-end;
        margin-bottom: 0.5rem;
        gap: 1rem;
        align-items: center;
    }

    .mode-toggle {
        display: flex;
        gap: 0.25rem;
    }

    .mode-toggle button {
        padding: 0.25rem 0.75rem;
        border-radius: 4px;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-muted);
        cursor: pointer;
        font-size: 0.8rem;
        transition: background 0.15s, color 0.15s;
    }

    .mode-toggle button.active {
        background: var(--accent-blue-bg);
        border-color: var(--accent-blue);
        color: var(--accent-blue);
    }

    .date-filters {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .date-filters label {
        font-size: 0.85rem;
        color: var(--text-muted);
    }

    .loading, .empty {
        text-align: center;
        padding: 3rem;
        color: var(--text-muted);
        font-size: 0.9rem;
    }
</style>