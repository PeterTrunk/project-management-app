
<script lang="ts">

    import { authStore, logout } from '../lib/stores/authStore';
    import { getProjectsAsync } from '../lib/api/projectApi';
    import { projectStore, setProjects, setActiveProject } from '../lib/stores/projectStore';
    import type { ProjectResponse } from '../lib/api/projectApi';

    import { push } from 'svelte-spa-router';

    // authStore-ból kinyerjük a user adatokat
    let displayName = '';
    authStore.subscribe(state => {
        displayName = state.user?.displayName ?? '';
    });

    function handleLogout() {
        logout();
        push('/');
    }

    let projects: ProjectResponse[] = [];
    // Oldal betöltésekor lekéri a projekteket
    async function loadProjects() {
        try {
            const data = await getProjectsAsync();
            setProjects(data);
        } catch (e) {
            console.error('Hiba a projektek lekérésekor!');
        }
    }

    // projectStore figyelése
    projectStore.subscribe(state => {
        projects = state.projects;
    });

    loadProjects();

    let activeView = 'overview';
    let activeProject = null;
</script>

<div class="app-container">
    <!-- Bal oldal -->
    <aside class="sidebar">
    <!-- Felső rész: projekt lista -->
        <div class="sidebar-projects">
            <h2>Projektek</h2>
            {#each projects as project}
                <button on:click={() => setActiveProject(project)}>
                    {project.name}
                </button>
            {/each}
            {#if projects.length === 0}
                <p>Nincs még projekt!</p>
            {/if}
        </div>
        
        <!-- Alsó rész: user info -->
        <div class="sidebar-user">
            <span> Logged in as: {displayName}</span>
            <button on:click={handleLogout}>Kijelentkezés</button>
        </div>
    </aside>

    <!-- Jobb oldal -->
    <div class="main">
        <!-- Navbar -->
        <nav class="topbar">
            <button on:click={() => activeView = 'overview'}>Overview</button>
            <button on:click={() => activeView = 'board'}>Board</button>
            <button on:click={() => activeView = 'team'}>Team</button>
        </nav>
        
        <!-- Dinamikus tartalom -->
        <div class="content">
            {#if activeView === 'overview'}
                <p>Overview nézet</p>
            {:else if activeView === 'board'}
                <p>Board nézet</p>
            {:else if activeView === 'team'}
                <p>Team nézet</p>
            {/if}
        </div>
    </div>
</div>

<style>

    .sidebar {
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        height: 100vh;
    }

</style>