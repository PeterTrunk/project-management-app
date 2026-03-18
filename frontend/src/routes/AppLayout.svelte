
<script lang="ts">
    import { meAsync } from '../lib/api/authApi';
    import { login } from '../lib/stores/authStore';
    import { authStore, logout } from '../lib/stores/authStore';
    import { getProjectsAsync } from '../lib/api/projectApi';
    import { projectStore, setProjects, setActiveProject } from '../lib/stores/projectStore';
    import type { ProjectResponse } from '../lib/api/projectApi';

    import ProjectOverview from '../lib/components/ProjectOverview.svelte';
    import ProjectSettings from '../lib/components/ProjectSettings.svelte';
    import BoardView from '../lib/components/BoardView.svelte';

    import CreateProjectModal from '../lib/components/CreateProjectModal.svelte';
    import UserSettingsModal from '../lib/components/UserSettingsModal.svelte';
    let isProjectCreationOpen = false;
    let isUserSettingsOpen = false;

    
    import { push } from 'svelte-spa-router';

    async function loadCurrentUser() {
        try {
            const user = await meAsync();
            login(
                localStorage.getItem('token') ?? '',
                localStorage.getItem('refreshToken') ?? '',
                {
                    userId: user.userId,
                    email: user.email,
                    displayName: user.displayName
                }
            );
        } catch (e) {
            // token lejárt, az interceptor kezeli
        }
    }

    loadCurrentUser();
    loadProjects();

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
    let activeProject: ProjectResponse | null = null;

    // projectStore figyelése
    projectStore.subscribe(state => {
        projects = state.projects;
        activeProject = state.activeProject;
    });

    // Oldal betöltésekor lekéri a projekteket
    async function loadProjects() {
        try {
            const data = await getProjectsAsync();
            setProjects(data);
        } catch (e) {
            console.error('Hiba a projektek lekérésekor!');
        }
    }

    loadProjects();

    let activeView = 'overview';
    
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
            <button on:click={() => isProjectCreationOpen = true}>+ Új projekt</button>
        </div>
        
        <!-- Alsó rész: user info -->
        <div class="sidebar-user">
            <span> Bejelentkezve: {displayName}</span>
            <button on:click={() => isUserSettingsOpen = true}>Profil</button>
            <button on:click={handleLogout}>Kijelentkezés</button>
        </div>
    </aside>

    <!-- Jobb oldal -->
    <div class="main">
        <!-- Navbar -->
        <nav class="topbar">
            <button on:click={() => activeView = 'overview'}>Overview</button>
            <button on:click={() => activeView = 'board'}>Board</button>
            <button on:click={() => activeView = 'sprints'}>Sprints</button>
            <button on:click={() => activeView = 'team'}>Team</button>
            <button on:click={() => activeView = 'statistics'}>Statistics</button>
            <button on:click={() => activeView = 'labels'}>Labels</button>
            <button on:click={() => activeView = 'teamResources'}>Team Resources</button>
            <button on:click={() => activeView = 'git'}>Git</button>
            <button on:click={() => activeView = 'projectSettings'}>Project Settings</button>
        </nav>
        
        <!-- Dinamikus tartalom -->
        <!--(Overview, Board, Team, Recent Activity, Statistics, Manager -> Sprints, Team Resources, Project Settings...)-->
        <div class="content">
            {#if activeProject}
                {#if activeView === 'overview'}
                    <ProjectOverview project={activeProject} />
                {:else if activeView === 'board'}
                    <BoardView/>
                {:else if activeView === 'sprints'}
                    <p>Sprintek nézet</p>
                {:else if activeView === 'team'}
                    <p>Team nézet</p>
                {:else if activeView === 'git'}
                    <p>Git nézet</p>
                {:else if activeView === 'statistics'}
                    <p>Statistics nézet</p>
                {:else if activeView === 'teamResources'}
                    <p>Team Resources nézet</p>
                {:else if activeView === 'labels'}
                    <p>Cimkék nézet</p>
                {:else if activeView === 'projectSettings'}
                    <ProjectSettings project={activeProject} />
                {/if}
            {:else}
                <p>Még nincs kiválasztótt projekt!</p>
                <p>Válassz egy projektet a bal oldali listából!</p>
            {/if}
        </div>
    </div>

    <!--Modals-->
    {#if isProjectCreationOpen}
    <CreateProjectModal 
        bind:isProjectCreationOpen={isProjectCreationOpen}
        onClose={loadProjects}
    />
    {/if}
    {#if isUserSettingsOpen}
    <UserSettingsModal 
        bind:isUserSettingsOpen={isUserSettingsOpen}
    />
    {/if}
</div>

<style>
    :global(html) {
        margin: 0;
        padding: 0;
    }

    :global(#app) {
        margin: 0;
        padding: 0;
        height: 100vh;
        width: 100vw;
    }
    :global(*, *::before, *::after) {
        box-sizing: border-box;
        margin: 0;
        padding: 0;
    }

    :global(body) {
        overflow: hidden;
    }

    .app-container {
        display: flex;
        height: 100vh;
        width: 100vw;
    }

    .sidebar {
        width: 250px;
        min-width: 250px;
        height: 100vh;
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        background: #1e1e1e;
        padding: 1rem;
        border-right: 1px solid #333;
    }

    .sidebar-user {
        padding: 1rem 0;
        border-top: 1px solid #333;
        flex-shrink: 0;
    }

    .sidebar-projects {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        overflow-y: auto;
    }

    .sidebar-user {
        padding-top: 1rem;
        border-top: 1px solid #333;
    }

    .main {
        flex: 1;
        display: flex;
        flex-direction: column;
        height: 100vh;
    }

    .topbar {
        height: 50px;
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0 1rem;
        background: #1e1e1e;
        border-bottom: 1px solid #333;
    }

    .content {
        flex: 1;
        overflow-y: auto;
        padding: 1rem;
    }
</style>