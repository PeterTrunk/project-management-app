<script lang="ts">
    import type { ProjectResponse } from '../api/projectApi';
    import { validateDescription, validateProjName } from '../validators';
    import { setActiveProject, setProjects, projectStore, setLabels } from '../../lib/stores/projectStore';
    import { updateProjectAsync, archiveProjectAsync, unarchiveProjectAsync, deleteProject, getProjectByIdAsync } from '../../lib/api/projectApi'
    import ConfirmModal from '../components/ConfirmModal.svelte';
    import { getLabelsAsync, deleteLabelAsync, type LabelResponse } from '../api/labelApi';
    import LabelCard from './LabelCard.svelte';
    import CreateLabelModal from './CreateLabelModal.svelte';
    import { onMount } from 'svelte';
    import { integrationStore, setIntegrations } from '../stores/integrationStore';
    import { getIntegrationsAsync } from '../api/integrationApi';
    import type { IntegrationResponse } from '../api/integrationApi';
    import IntegrationCard from './IntegrationCard.svelte';
    import CreateIntegrationModal from './CreateIntegrationModal.svelte';

    import { Settings2, Tag, GitBranch, Plus } from 'lucide-svelte';

    let activeTab: 'general' | 'labels' | 'git' = 'general';

    export let project: ProjectResponse;
    let labels: LabelResponse[] = [];
    let isCreateLabelOpen = false;
    let integrations: IntegrationResponse[] = [];
    let isCreateIntegrationOpen = false;

    integrationStore.subscribe(state => {
        integrations = state.integrations;
    });

    projectStore.subscribe(state => {
        labels = state.labels;
    });

    onMount(async () => {
       const data = await getLabelsAsync(project.id);
       setLabels(data); 
    });

    async function refreshProject() {
        const updated = await getProjectByIdAsync(project.id);
        setActiveProject(updated);
    }
    
    function requestDeleteLabel(labelId: string) {
        labelToDelete = labelId;
        openConfirm(
            'Label törlése',
            'Biztosan törölni szeretnéd? Minden taskról eltávolításra kerül!',
            async () => await handleDeleteLabel(labelToDelete)
        );
    }

    async function handleDeleteLabel(labelId: string) {
        try {
            await deleteLabelAsync(project.id, labelId);
            setLabels(labels.filter(l => l.id !== labelId));
        } catch (e) {
            error = 'Hiba történt a label törlésekor!';
        }
    }

    let isConfirmOpen = false;
    let labelToDelete = '';
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};

    function openConfirm(title: string, message: string, action: () => Promise<void>) {
        confirmTitle = title;
        confirmMessage = message;
        confirmAction = action;
        isConfirmOpen = true;
    }

    let success = '';
    let error = '';

    let description = project.description ?? '';
    let name = project.name;
    let isArchived = project.isArchived;

    async function handleUpdate() {
        error ='';
        success = '';
        let errorOccured: boolean = false;
        const descError = validateDescription(description);
        const nameError = validateProjName(name);
        if(descError!=null){
            error = error + descError;
            errorOccured = true;
        }
        if(nameError!=null){
            error = error + nameError;
            errorOccured = true;
        }
        if(errorOccured){
            return;
        }
        try {
            const response = await updateProjectAsync({ name, description, isArchived }, project.id);
            success = 'Módosítások mentve';
            await refreshProject();
            return;
        } catch (e) {
            error = 'Hiba történt a módosítás során!';
        }
    }

    async function handleArchive() {
        success = '';
        error = '';
        try {
            const response = await archiveProjectAsync(project.id);
            success = 'Projekt arhiválva!';
            await refreshProject();
            return;
        } catch (e) {
            error = 'Hiba történt az arhiválás során!'
        }
    }

    async function handleUnarchive() {
        success = '';
        error = '';
        try {
            const response = await unarchiveProjectAsync(project.id);
            success = 'Projekt aktiválva!';
            await refreshProject();
            return;
        } catch (e) {
            error = 'Hiba történt az aktiválás során!'
        }
    }

    async function handleDelete() {
    error = '';
    try {
        await deleteProject(project.id);
        // Eltávolítjuk a listából
        let currentProjects: ProjectResponse[] = [];
        projectStore.subscribe(state => currentProjects = state.projects)();
        setProjects(currentProjects.filter(p => p.id !== project.id));
        // Nullázzuk az aktív projektet
        setActiveProject(null);
    } catch (e) {
        error = 'Hiba történt a törlés során!';
    }
}

</script>

<div class="settings-container">
    <h1>{project.name}</h1>

    <div class="tabs">
        <button
            class="tab-btn"
            class:active={activeTab === 'general'}
            on:click={() => activeTab = 'general'}
        >
            <Settings2 size={15} />
            Általános
        </button>
        <button
            class="tab-btn"
            class:active={activeTab === 'labels'}
            on:click={() => activeTab = 'labels'}
        >
            <Tag size={15} />
            Labelek
        </button>
        <button
            class="tab-btn"
            class:active={activeTab === 'git'}
            on:click={() => activeTab = 'git'}
        >
            <GitBranch size={15} />
            Git
        </button>
    </div>

    <div class="tab-content">

        {#if activeTab === 'general'}
            <form>
                <div class="field">
                    <label>Projekt neve<input bind:value={name} placeholder="Max 120 karakter"></label>
                </div>
                <div class="field">
                    <label>Leírás<textarea bind:value={description} placeholder="Max 1000 karakter"></textarea></label>
                </div>
                <div class="meta">
                    <p>Tulajdonos: <span>{project.ownerName}</span></p>
                    <p>Projekt kulcs: <span>{project.projKey}</span></p>
                    <p>Létrehozva: <span>{new Date(project.createdAt).toLocaleDateString('hu-HU')}</span></p>
                    <p>Módosítva: <span>{new Date(project.updatedAt).toLocaleDateString('hu-HU')}</span></p>
                </div>
                {#if error}<p class="msg error">{error}</p>{/if}
                {#if success}<p class="msg success">{success}</p>{/if}
                <button type="button" class="btn-primary" on:click={() => openConfirm(
                    'Módosítások mentése',
                    'Biztosan menteni szeretnéd a változtatásokat?',
                    handleUpdate
                )}>Módosítások mentése</button>
            </form>

            <div class="danger-zone">
                <h3>Veszélyzóna</h3>
                <div class="danger-actions">
                    {#if project.isArchived}
                        <div class="danger-row">
                            <div>
                                <p class="danger-title">Projekt dearchiválása</p>
                                <p class="danger-desc">A projekt újra aktív lesz.</p>
                            </div>
                            <button class="btn-warning" type="button" on:click={() => openConfirm(
                                'Projekt dearchiválása',
                                'Biztosan dearchiválni szeretnéd a projektet?',
                                handleUnarchive
                            )}>Dearchiválás</button>
                        </div>
                    {:else}
                        <div class="danger-row">
                            <div>
                                <p class="danger-title">Projekt archiválása</p>
                                <p class="danger-desc">A projekt csak olvasható módba kerül.</p>
                            </div>
                            <button class="btn-warning" type="button" on:click={() => openConfirm(
                                'Projekt archiválása',
                                'Biztosan archiválni szeretnéd a projektet?',
                                handleArchive
                            )}>Archiválás</button>
                        </div>
                    {/if}
                    <div class="danger-row">
                        <div>
                            <p class="danger-title">Projekt törlése</p>
                            <p class="danger-desc">Végleges törlés, visszavonhatatlan művelet.</p>
                        </div>
                        <button class="btn-danger" on:click={() => openConfirm(
                            'Projekt Törlése',
                            'Biztosan törlöd véglegesen a projektet?',
                            handleDelete
                        )}>Törlés</button>
                    </div>
                </div>
            </div>
        {/if}

        {#if activeTab === 'labels'}
            <div class="tab-section">
                <div class="section-header">
                    <h2>Labelek</h2>
                    <button class="btn-add" on:click={() => isCreateLabelOpen = true}>
                        <Plus size={15} />
                        Új label
                    </button>
                </div>
                <div class="labels-list">
                    {#if labels.length > 0}
                        {#each labels as label}
                            <LabelCard {label} onDelete={requestDeleteLabel} />
                        {/each}
                    {:else}
                        <p class="empty">Nincs még label</p>
                    {/if}
                </div>
            </div>
        {/if}

        {#if activeTab === 'git'}
            <div class="tab-section">
                <div class="section-header">
                    <h2>Git Integráció</h2>
                    <button class="btn-add" on:click={() => isCreateIntegrationOpen = true}>
                        <Plus size={15} />
                        Integráció hozzáadása
                    </button>
                </div>
                {#if integrations.length > 0}
                    <div class="integrations-list">
                        {#each integrations as integration (integration.id)}
                            <IntegrationCard {integration} projectId={project.id} />
                        {/each}
                    </div>
                {:else}
                    <p class="empty">Még nincs git integráció hozzáadva</p>
                {/if}
            </div>
        {/if}

    </div>
</div>

<!--Modálok – változatlan-->
{#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Megerősítés"
        onConfirm={confirmAction}
    />
{/if}
{#if isCreateLabelOpen}
    <CreateLabelModal
        bind:isOpen={isCreateLabelOpen}
        projectId={project.id}
        onClose={async () => {
            const data = await getLabelsAsync(project.id);
            setLabels(data);
        }}
    />
{/if}
{#if isCreateIntegrationOpen}
    <CreateIntegrationModal
        bind:isOpen={isCreateIntegrationOpen}
        projectId={project.id}
        onClose={() => isCreateIntegrationOpen = false}
    />
{/if}

<style>
    .settings-container {
        width: 100%;
        margin: 0;
        padding: 2rem;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
    }

    h1 {
        font-size: 1.8rem;
    }

    h2 {
        font-size: 1.1rem;
        color: var(--text-primary);
    }

    h3 {
        font-size: 0.8rem;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        color: var(--text-muted);
        margin-bottom: 0.75rem;
    }

    /* ── Tabs ── */
    .tabs {
        display: flex;
        gap: 0.25rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0;
    }

    .tab-btn {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.5rem 1rem;
        border: none;
        border-bottom: 2px solid transparent;
        background: transparent;
        color: var(--text-secondary);
        font-size: 0.9rem;
        cursor: pointer;
        border-radius: 6px 6px 0 0;
        margin-bottom: -1px;
        transition: color 0.15s, border-color 0.15s;
    }

    .tab-btn:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    .tab-btn.active {
        color: var(--accent-blue);
        border-bottom-color: var(--accent-blue);
        background: transparent;
    }

    /* ── Tab content ── */
    .tab-content {
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
    }

    .tab-section {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .section-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
    }

    /* ── Form ── */
    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .field {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
    }

    label {
        font-size: 0.85rem;
        color: var(--text-secondary);
    }

    input, textarea {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem 0.75rem;
        font-size: 1rem;
        width: 100%;
    }

    textarea {
        resize: vertical;
        min-height: 120px;
    }

    input:focus, textarea:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .meta {
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
        padding: 0.75rem;
        background: var(--bg-secondary);
        border-radius: 6px;
        border: 1px solid var(--border);
        font-size: 0.9rem;
        color: var(--text-secondary);
    }

    .meta span {
        font-weight: 600;
        color: var(--text-primary);
    }

    /* ── Buttons ── */
    .btn-primary {
        background: var(--accent-blue-bg);
        border: 1px solid var(--accent-blue);
        color: var(--accent-blue);
        padding: 0.5rem 1.25rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        align-self: flex-start;
        transition: background 0.15s;
    }

    .btn-primary:hover {
        background: var(--accent-blue);
        color: #fff;
    }

    .btn-add {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        background: var(--bg-hover);
        border: 1px solid var(--border-hover);
        color: var(--text-secondary);
        padding: 0.4rem 0.85rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        transition: color 0.15s, background 0.15s;
    }

    .btn-add:hover {
        color: var(--text-primary);
        background: var(--border-hover);
    }

    /* ── Danger zone ── */
    .danger-zone {
        border: 1px solid var(--accent-red-bg);
        border-radius: 8px;
        padding: 1rem 1.25rem;
        margin-top: 0.5rem;
    }

    .danger-actions {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .danger-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        padding: 0.75rem 0;
        border-top: 1px solid var(--border);
    }

    .danger-row:first-child {
        border-top: none;
        padding-top: 0;
    }

    .danger-title {
        font-size: 0.9rem;
        color: var(--text-primary);
        font-weight: 500;
    }

    .danger-desc {
        font-size: 0.8rem;
        color: var(--text-muted);
        margin-top: 0.15rem;
    }

    .btn-warning {
        background: var(--accent-yellow-bg);
        border: 1px solid var(--accent-yellow);
        color: var(--accent-yellow);
        padding: 0.4rem 0.85rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
        transition: background 0.15s;
    }

    .btn-warning:hover {
        background: var(--accent-yellow);
        color: #000;
    }

    .btn-danger {
        background: var(--accent-red-bg);
        border: 1px solid var(--accent-red);
        color: var(--accent-red);
        padding: 0.4rem 0.85rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.85rem;
        white-space: nowrap;
        transition: background 0.15s;
    }

    .btn-danger:hover {
        background: var(--accent-red);
        color: #fff;
    }

    /* ── Misc ── */
    .msg {
        font-size: 0.9rem;
        padding: 0.5rem 0;
    }

    .msg.success { color: var(--accent-green); }
    .msg.error { color: var(--accent-red); white-space: pre-line; }

    .empty {
        font-size: 0.85rem;
        color: var(--text-muted);
        padding: 1rem 0;
    }

    .labels-list, .integrations-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }
</style>