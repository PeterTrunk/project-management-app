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
    import { getIntegrationsAsync } from '../lib/api/integrationApi';
    import { setIntegrations, clearIntegrations } from '../lib/stores/integrationStore';
    import { integrationStore } from '../lib/stores/integrationStore';
    import { updateIntegration } from '../lib/stores/integrationStore';
    import type { IntegrationResponse } from '../lib/api/integrationApi';
    import { addIntegration, removeIntegration } from '../lib/stores/integrationStore';

    import ProjectOverview from '../lib/components/ProjectOverview.svelte';
    import ProjectSettings from '../lib/components/ProjectSettings.svelte';
    import BoardView from '../lib/components/BoardView.svelte';
    import SprintsView from '../lib/components/SprintsView.svelte';
    import TeamView from '../lib/components/TeamView.svelte';
    import TeamResources from '../lib/components/TeamResources.svelte';
    import GitView from '../lib/components/GitView.svelte';
    import StatisticsView from '../lib/components/StatisticsView.svelte';

    import CreateProjectModal from '../lib/components/CreateProjectModal.svelte';
    import UserSettingsModal from '../lib/components/UserSettingsModal.svelte';

    import { 
        LayoutDashboard, Kanban, Timer, Users, ChartNoAxesColumn, 
        FolderOpen, GitBranch, Settings, LogOut, ChevronLeft, 
        ChevronRight, Plus, FileText, User, Archive
    } from 'lucide-svelte';

    //Ideiglenes Theme Váltó Toggle
    import { themeStore } from '../lib/stores/themeStore';
    import { toggleTheme } from '../lib/stores/themeStore';
    import { Sun, Moon } from 'lucide-svelte';

    let currentTheme = 'dark';
    themeStore.subscribe(t => currentTheme = t);

    
    let sidebarCollapsed = false;

    const navItems = [
        { view: 'overview', label: 'Overview', icon: LayoutDashboard },
        { view: 'board', label: 'Board', icon: Kanban },
        { view: 'sprints', label: 'Sprints', icon: Timer },
        { view: 'team', label: 'Team', icon: Users },
        { view: 'statistics', label: 'Statisztika', icon: ChartNoAxesColumn },
        { view: 'teamResources', label: 'Resources', icon: FileText },
        { view: 'git', label: 'Git', icon: GitBranch },
        { view: 'projectSettings', label: 'Beállítások', icon: Settings },
    ];

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
            
            signalRService.on('IntegrationVerified', (data) => {
                let currentIntegrations: IntegrationResponse[] = [];
                integrationStore.subscribe(state => { currentIntegrations = state.integrations; })();
                
                const integration = currentIntegrations.find(i => i.id === data.integrationId);
                if (integration) {
                    updateIntegration({ ...integration, isVerified: true });
                }
            });

            signalRService.on('IntegrationUpdated', (data) => {
                let currentIntegrations: IntegrationResponse[] = [];
                integrationStore.subscribe(state => { currentIntegrations = state.integrations; })();
                
                const integration = currentIntegrations.find(i => i.id === data.integrationId);
                if (integration) {
                    updateIntegration({ ...integration, isVerified: data.isVerified });
                }
            });
            
            signalRService.on('IntegrationCreated', async (data) => {
                // Friss integráció lekérése és store-ba rakása
                const integrations = await getIntegrationsAsync(currentProjectId);
                setIntegrations(integrations);
            });

            signalRService.on('IntegrationDeleted', (data) => {
                removeIntegration(data.integrationId);
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

    async function loadIntegrations(projectId: string) {
        const integrations = await getIntegrationsAsync(projectId);
        setIntegrations(integrations);
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
        signalRService.off('IntegrationVerified');
        signalRService.off('IntegrationUpdated');
        signalRService.off('IntegrationCreated');
        signalRService.off('IntegrationDeleted');
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
            loadIntegrations(state.activeProject.id).catch(console.error);
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
    <aside class="sidebar" class:collapsed={sidebarCollapsed}>
        <!-- Collapse gomb -->
        <button class="collapse-btn" on:click={() => sidebarCollapsed = !sidebarCollapsed}>
            {#if sidebarCollapsed}
                <ChevronRight size={18} />
            {:else}
                <ChevronLeft size={18} />
            {/if}
        </button>

        <!-- Projektek -->
        <div class="sidebar-projects">
            {#if !sidebarCollapsed}
                <h2>Projektek</h2>
            {/if}
            {#each projects as project}
                <button 
                    class="project-btn"
                    class:active={activeProject?.id === project.id}
                    on:click={() => setActiveProject(project)}
                    title={project.name}
                >
                    <FolderOpen size={18} />
                    {#if !sidebarCollapsed}
                        <span>{project.name}</span>
                    {/if}
                </button>
            {/each}
            {#if projects.length === 0 && !sidebarCollapsed}
                <p class="empty">Nincs még projekt!</p>
            {/if}
            <button 
                class="new-project-btn"
                on:click={() => isProjectCreationOpen = true}
                title="Új projekt"
            >
                <Plus size={18} />
                {#if !sidebarCollapsed}
                    <span>Új projekt</span>
                {/if}
            </button>
        </div>

        <!-- User info -->
        <div class="sidebar-user">
            {#if !sidebarCollapsed}
                <div class="username">
                    <User size={16} />
                    {#if !sidebarCollapsed}
                        <span>{displayName}</span>
                    {/if}
                </div>
            {/if}
            <button 
                class="icon-btn"
                on:click={() => isUserSettingsOpen = true}
                title="Profil"
            >
                <Settings size={18} />
                {#if !sidebarCollapsed}
                    <span>Profil</span>
                {/if}
            </button>
            <button 
                class="icon-btn logout-btn"
                on:click={handleLogout}
                title="Kijelentkezés"
            >
                <LogOut size={18} />
                {#if !sidebarCollapsed}
                    <span>Kijelentkezés</span>
                {/if}
            </button>
            <!-- sidebar-user részbe: -->
            <button class="icon-btn" on:click={toggleTheme} title="Téma váltás">
                {#if currentTheme === 'dark'}
                    <Sun size={18} />
                    {#if !sidebarCollapsed}
                        <span>Light mód</span>
                    {/if}
                {:else}
                    <Moon size={18} />
                    {#if !sidebarCollapsed}
                        <span>Dark mód</span>
                    {/if}
                {/if}
            </button>
        </div>
    </aside>

    <!-- Jobb oldal -->
    <div class="main">
        <!-- Navbar -->
        <nav class="topbar">
            {#each navItems as item}
                <button
                    class="nav-btn"
                    class:active={activeView === item.view}
                    on:click={() => activeView = item.view}
                    title={item.label}
                >
                    <svelte:component this={item.icon} size={18} />
                    <span class="nav-label">{item.label}</span>
                </button>
            {/each}
        </nav>
        {#if activeProject?.isArchived}
            <div class="archived-banner">
                <Archive size={16} />
                <span>Ez a projekt archivált, csak olvasható hozzáférés!</span>
            </div>
        {/if}

        <!-- Dinamikus tartalom -->
        <div 
            class="content"
            class:scrollable={activeView !== 'board'}
            class:no-padding={activeView === 'board' || 
                activeView === 'sprints' || activeView === 'teamResources' || 
                activeView === 'git' || activeView === 'statistics'}
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
                    <GitView projectId={activeProject.id} />
                {:else if activeView === 'statistics'}
                    <StatisticsView projectId={activeProject.id} />
                {:else if activeView === 'teamResources'}
                    <TeamResources projectId={activeProject.id} />
                {:else if activeView === 'projectSettings'}
                    <ProjectSettings project={activeProject} />
                {/if}
            {:else}
                <div class="no-project">
                    <FolderOpen size={48} color="var(--text-muted)" />
                    <p>Válassz egy projektet a bal oldali listából!</p>
                    <button class="new-project-btn" on:click={() => isProjectCreationOpen = true}>
                        <Plus size={18} />
                        <span>Új projekt létrehozása</span>
                    </button>
                </div>
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
        background: var(--bg-primary);
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
        overflow: visible;
        width: 220px;
        min-width: 220px;
        height: 100vh;
        display: flex;
        flex-direction: column;
        justify-content: space-between;
        background: var(--bg-secondary);
        padding: 1rem 0.75rem;
        border-right: 1px solid var(--border);
        transition: width 0.2s ease, min-width 0.2s ease;
        position: relative;
    }

    .sidebar.collapsed {
        width: 60px;
        min-width: 60px;
        padding: 1rem 0.5rem;
    }

    .collapse-btn {
        position: absolute;
        top: 0.75rem;
        right: -12px;
        width: 24px;
        height: 24px;
        border-radius: 50%;
        background: var(--bg-card);
        border: 1px solid var(--border);
        color: var(--text-secondary);
        display: flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        padding: 0;
        z-index: 10;
    }

    .collapse-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .sidebar-projects {
        display: flex;
        padding: 0 0.25rem;
        flex-direction: column;
        gap: 0.25rem;
        flex: 1;
        overflow-y: auto;
        overflow-x: hidden;
    }

    .sidebar-projects h2 {
        font-size: 0.75rem;
        color: var(--text-muted);
        text-transform: uppercase;
        letter-spacing: 0.08em;
        margin-bottom: 0.5rem;
        padding: 0 0.25rem;
    }

    .project-btn {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.5rem 0.5rem;
        border-radius: 6px;
        border: none;
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
        width: 100%;
        text-align: left;
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
        white-space: nowrap;
        overflow: hidden;
    }

    .project-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .project-btn.active {
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
    }

    .new-project-btn {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.5rem 0.5rem;
        border-radius: 6px;
        border: 1px dashed var(--border-hover);
        background: transparent;
        color: var(--text-muted);
        cursor: pointer;
        width: 100%;
        text-align: left;
        font-size: 0.85rem;
        margin-top: 0.25rem;
        transition: background 0.15s, color 0.15s;
    }

    .new-project-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
        border-color: var(--text-secondary);
    }

    .sidebar-user {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        padding-top: 0.75rem;
        border-top: 1px solid var(--border);
        flex-shrink: 0;
    }

    .username {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.8rem;
        color: var(--text-secondary);
        padding: 0.25rem 0.75rem;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
    }

    .icon-btn {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.5rem 0.75rem;
        border-radius: 6px;
        border: none;
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
        width: 100%;
        text-align: left;
        font-size: 0.85rem;
        transition: background 0.15s, color 0.15s;
    }

    .icon-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .logout-btn:hover {
        color: var(--accent-red);
    }

    .main {
        flex: 1;
        display: flex;
        flex-direction: column;
        height: 100vh;
        min-width: 0;
    }

    .topbar {
        height: 48px;
        display: flex;
        align-items: center;
        gap: 0.25rem;
        padding: 0 0.75rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        overflow-x: auto;
        flex-shrink: 0;
    }

    .nav-btn {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.4rem 0.75rem;
        border-radius: 6px;
        border: none;
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
        transition: background 0.15s, color 0.15s;
    }

    .nav-btn:hover {
        background: var(--bg-hover);
        color: var(--text-primary);
    }

    .nav-btn.active {
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
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

    .no-project {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 1rem;
        height: 100%;
        color: var(--text-muted);
    }

    .empty {
        font-size: 0.8rem;
        color: var(--text-muted);
        padding: 0.25rem 0.75rem;
    }

    @media (max-width: 1366px) {
        .sidebar {
            width: 180px;
            min-width: 180px;
        }

        .nav-label {
            font-size: 0.8rem;
        }
    }

    @media (max-width: 768px) {
        .sidebar {
            width: 60px;
            min-width: 60px;
            padding: 1rem 0.5rem;
        }

        .nav-label {
            display: none;
        }
    }

    .archived-banner {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        padding: 0.4rem 1rem;
        background: var(--accent-yellow-bg);
        color: var(--accent-yellow);
        font-size: 0.85rem;
        border-bottom: 1px solid var(--accent-yellow);
        flex-shrink: 0;
    }
</style>