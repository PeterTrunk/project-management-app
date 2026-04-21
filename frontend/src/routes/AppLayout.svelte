<script lang="ts">
    import { onMount, onDestroy } from 'svelte';
    import { signalRService } from '../lib/services/signalRService';
    import { push } from 'svelte-spa-router';
    import { meAsync } from '../lib/api/authApi';
    import { login } from '../lib/stores/authStore';
    import { authStore, logout } from '../lib/stores/authStore';
    import { getProjectsAsync } from '../lib/api/projectApi';
    import { projectStore, setProjects, setActiveProject } from '../lib/stores/projectStore';
    import type { ProjectResponse } from '../lib/api/projectApi';
    import { getLabelsAsync } from '../lib/api/labelApi';
    import { setLabels } from '../lib/stores/projectStore';
    import { triggerTeamRefresh } from '../lib/stores/teamStore';
    import { clearTeam } from '../lib/stores/teamStore';
    import { getMembersAsync } from '../lib/api/teamApi';
    import { setMembers } from '../lib/stores/teamStore';

    import ProjectOverview from '../lib/components/ProjectOverview.svelte';
    import ProjectSettings from '../lib/components/ProjectSettings.svelte';
    import BoardView from '../lib/components/BoardView.svelte';
    import SprintsView from '../lib/components/SprintsView.svelte';
    import TeamView from '../lib/components/TeamView.svelte';

    import CreateProjectModal from '../lib/components/CreateProjectModal.svelte';
    import UserSettingsModal from '../lib/components/UserSettingsModal.svelte';
    
    let isProjectCreationOpen = false;
    let isUserSettingsOpen = false;
    
    let token = '';

    let currentUserId = '';
    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    onMount(async () => {
        if (token) {
            await signalRService.connect(token);

            if (activeProject?.id) {
                const labels = await getLabelsAsync(activeProject.id);
                setLabels(labels);
            }

            signalRService.on('LabelCreated', async () => {
                if (currentProjectId) {
                    await loadLabels(currentProjectId);
                }
            });

            signalRService.on('LabelDeleted', async () => {
                if (currentProjectId) {
                    await loadLabels(currentProjectId);
                }
            });

            signalRService.on('ProjectUpdated', async () => {
                const data = await getProjectsAsync();
                setProjects(data);
                if (activeProject?.id) {
                    const updated = data.find(p => p.id === activeProject!.id);
                    if (updated) setActiveProject(updated);
                }
            });

            signalRService.on('ProjectArchived', async () => {
                const data = await getProjectsAsync();
                setProjects(data);
                if (activeProject?.id) {
                    const updated = data.find(p => p.id === activeProject!.id);
                    if (updated) setActiveProject(updated);
                }
            });

            signalRService.on('ProjectUnarchived', async () => {
                const data = await getProjectsAsync();
                setProjects(data);
                if (activeProject?.id) {
                    const updated = data.find(p => p.id === activeProject!.id);
                    if (updated) setActiveProject(updated);
                }
            });

            signalRService.on('MemberRemoved', async (data) => {
                console.log('MemberRemoved:', data.userId, 'currentUserId:', currentUserId);
                if (data.userId === currentUserId) {
                    const projects = await getProjectsAsync();
                    setProjects(projects);
                    setActiveProject(null);
                    await new Promise(resolve => setTimeout(resolve, 100));
                    push('/app');
                } else {
                    triggerTeamRefresh();
                }
            });

            signalRService.on('MemberAdded', () => {
                triggerTeamRefresh();
            });

            signalRService.on('MemberRoleUpdated', () => {
                triggerTeamRefresh();
            });
        }
    });

    async function loadLabels(projectId: string) {
        const labels = await getLabelsAsync(projectId);
        setLabels(labels);
    }

    async function loadMembers(projectId: string) {
        const members = await getMembersAsync(projectId);
        setMembers(members);
    }

    onDestroy(async () => {
        signalRService.off('LabelCreated');
        signalRService.off('LabelDeleted');
        signalRService.off('ProjectUpdated');
        signalRService.off('ProjectArchived');
        signalRService.off('ProjectUnarchived');
        signalRService.off('MemberAdded');
        signalRService.off('MemberRemoved');
        signalRService.off('MemberRoleUpdated');
        await signalRService.disconnect();
    });

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
        token = state.token ?? '';
    });

    function handleLogout() {
        logout();
        push('/');
    }

    let projects: ProjectResponse[] = [];
    let activeProject: ProjectResponse | null = null;
    let currentProjectId = '';

    // projectStore figyelése
    projectStore.subscribe(state => {
        projects = state.projects;
        activeProject = state.activeProject;

        if (state.activeProject?.id && state.activeProject.id !== currentProjectId) {
            currentProjectId = state.activeProject.id;

            signalRService.joinProject(state.activeProject.id).catch(console.error);

            loadLabels(state.activeProject.id).catch(console.error);
            loadMembers(state.activeProject.id).catch(console.error);
        }
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
            <button on:click={() => activeView = 'teamResources'}>Team Resources</button>
            <button on:click={() => activeView = 'git'}>Git</button>
            <button on:click={() => activeView = 'projectSettings'}>Project Settings</button>
        </nav>
        
        <!-- Dinamikus tartalom -->
        <!--(Overview, Board, Team, Statistics, Manager -> Sprints, Team Resources, Project Settings...)-->
        <div 
            class="content"
            class:scrollable={activeView !== 'board'}
            class:no-padding={activeView === 'sprints'}
        >
            {#if activeProject}
                {#if activeView === 'overview'}
                    <ProjectOverview project={activeProject} />
                {:else if activeView === 'board'}
                    <BoardView/>
                {:else if activeView === 'sprints'}
                    <SprintsView projectId={activeProject.id} />
                {:else if activeView === 'team'}
                    <TeamView projectId={activeProject.id} />
                {:else if activeView === 'git'}
                    <p>Git nézet</p>
                {:else if activeView === 'statistics'}
                    <p>Statistics nézet</p>
                {:else if activeView === 'teamResources'}
                    <p>Team Resources nézet</p>
                {:else if activeView === 'projectSettings'}
                    <ProjectSettings project={activeProject} />
                {/if}
            {:else}
                <p>Még nincs kiválasztótt projekt!</p>
                <p>Válassz egy projektet a bal oldali listából!</p>
            {/if}
        </div>
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

<style>
    :global(body){
        margin: 0;
        padding: 0;
        background: #121212;
    }

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
        min-width: 0;
    }

    .topbar {
        height: 50px;
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0 1rem;
        background: #1e1e1e;
        border-bottom: 1px solid #333;
        justify-content: space-evenly;
    }

    .content {
        flex: 1;
        overflow: hidden;
        padding: 0;
        display: flex;
        flex-direction: column;
        min-width: 0;
    }
    .content.scrollable {
        overflow-y: auto;
        padding: 1rem;
    }

    .content.no-padding {
        padding: 0;
        gap: 0;
    }
</style>