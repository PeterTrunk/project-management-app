<script lang="ts">
    import type { ProjectResponse } from '../api/projectApi';
    import { projectStore } from '../../lib/stores/projectStore';
    import { updateProjectAsync, archiveProjectAsync, unarchiveProjectAsync, deleteProject } from '../../lib/api/projectApi'
    import ConfirmModal from '../components/ConfirmModal.svelte';
    import { deleteLabelAsync, type LabelResponse } from '../api/labelApi';
    import LabelCard from './LabelCard.svelte';
    import CreateLabelModal from './CreateLabelModal.svelte';
    import { integrationStore } from '../stores/integrationStore';
    import type { IntegrationResponse } from '../api/integrationApi';
    import IntegrationCard from './IntegrationCard.svelte';
    import CreateIntegrationModal from './CreateIntegrationModal.svelte';

    import { Settings2, Tag, GitBranch, Plus, Search, X } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    let activeTab: 'general' | 'labels' | 'git' = 'general';

    export let project: ProjectResponse;
    let labels: LabelResponse[] = [];
    let isCreateLabelOpen = false;
    let labelSearch = '';

    //A labelek amugy is a store-ban vannak, igy a kereses nem jar szerverhivassal.
    $: labelQuery = labelSearch.trim().toLowerCase();
    $: filteredLabels = labelQuery === ''
        ? labels
        : labels.filter(l => l.name.toLowerCase().includes(labelQuery));
    let integrations: IntegrationResponse[] = [];
    let isCreateIntegrationOpen = false;

    integrationStore.subscribe(state => {
        integrations = state.integrations;
    });
    
    projectStore.subscribe(state => {
        labels = state.labels;
    });
    
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
            notify.success('Label törölve!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt a label törlésekor!';
            error = message;
            notify.error(message);
        }
    }

    let isConfirmOpen = false;
    let labelToDelete = '';
    let confirmTitle = '';
    let confirmMessage = '';
    let confirmAction: () => Promise<void> = async () => {};

    let isDeleteConfirmOpen = false;
    let deleteConfirmName = '';
    $: canDelete = deleteConfirmName === project.name;
    
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
        try {
            const response = await updateProjectAsync({ name, description, isArchived }, project.id);
            notify.success('Projekt módosítva!');
            return;
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a módosítás során!');
        }
    }

    async function handleArchive() {
        try {
            const response = await archiveProjectAsync(project.id);
            notify.success('Projekt archiválva!');
            return;
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt az archiválás során!');
        }
    }

    async function handleUnarchive() {
        try {
            const response = await unarchiveProjectAsync(project.id);
            notify.success('Projekt aktiválva!');
            return;
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt az aktiválás során!');
        }
    }

    async function handleDelete() {
        if (!canDelete) return;
        try {
            await deleteProject(project.id);
            notify.success('Projekt törölve!');
        } catch (e: any) {
            notify.error(e.response?.data ?? e.message ?? 'Hiba történt a törlés során!');
        } finally {
            isDeleteConfirmOpen = false;
            deleteConfirmName = '';
        }
    }
</script>

<div class="settings-container">
    <h1>{project.name}</h1>

    <div class="tabs scroll-x">
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

                <div class="meta-grid">
                    <span class="meta-label">Tulajdonos</span>
                    <span class="meta-value">
                        {project.ownerName}
                    </span>
                    <span class="meta-label">Projekt kulcs</span>
                    <span class="meta-value">
                        {project.projKey}
                    </span>
                    <span class="meta-label">Létrehozva</span>
                    <span class="meta-value">
                        {new Date(project.createdAt).toLocaleDateString('hu-HU')}
                    </span>
                    
                    <span class="meta-label">Módosítva</span>
                    <span class="meta-value">
                        {new Date(project.updatedAt).toLocaleDateString('hu-HU')}
                    </span>                   
                </div>
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
                        <div class="danger-row stack-480">
                            <div>
                                <p class="danger-title">Projekt aktiválása</p>
                                <p class="danger-desc">A projekt újra aktív lesz.</p>
                            </div>
                            <button class="btn-warning" type="button" on:click={() => openConfirm(
                                'Projekt aktiválás',
                                'Biztosan aktiválni szeretnéd a projektet?',
                                handleUnarchive
                            )}>Aktiválás</button>
                        </div>
                    {:else}
                        <div class="danger-row stack-480">
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
                    <div class="danger-row stack-480">
                        <div>
                            <p class="danger-title">Projekt törlése</p>
                            <p class="danger-desc">Végleges törlés, visszavonhatatlan művelet.</p>
                        </div>
                        {#if isDeleteConfirmOpen}
                            <button on:click={() => { isDeleteConfirmOpen = false; deleteConfirmName = ''; }}>
                                Mégsem
                            </button>
                        {:else}
                            <button class="btn-danger" on:click={() => isDeleteConfirmOpen = true}>
                                Törlés
                            </button>
                        {/if}
                    </div>
                    
                    {#if isDeleteConfirmOpen}
                        <div class="delete-confirm">
                            <p>A törlés megerősítéséhez írd be a projekt nevét:</p>
                            <p class="project-name-hint"><strong>{project.name}</strong></p>
                            <input
                                type="text"
                                bind:value={deleteConfirmName}
                                placeholder="Projekt neve..."
                            />
                            <div class="delete-confirm-actions">
                                <button
                                    class="btn-danger"
                                    disabled={!canDelete}
                                    on:click={() => openConfirm(
                                        'Projekt Törlése',
                                        'Biztosan törlöd véglegesen a projektet?',
                                        handleDelete
                                    )}>
                                    Végleges törlés
                                </button>
                            </div>
                        </div>
                    {/if}
                </div>
            </div>
        {/if}

        {#if activeTab === 'labels'}
            <div class="tab-section">
                <div class="section-header stack-480 wrap-480">
                    <h2>Labelek</h2>
                    <button class="btn-add" on:click={() => isCreateLabelOpen = true}>
                        <Plus size={15} />
                        Új label
                    </button>
                </div>
                {#if labels.length > 0}
                    <div class="search-wrapper">
                        <Search size={14} />
                        <input
                            type="text"
                            placeholder="Keresés a labelek között..."
                            bind:value={labelSearch}
                            class="search-input"
                        />
                        {#if labelSearch !== ''}
                            <button class="search-clear" title="Keresés törlése"
                                on:click={() => labelSearch = ''}>
                                <X size={14} />
                            </button>
                        {/if}
                    </div>
                {/if}
                <div class="labels-list">
                    {#if filteredLabels.length > 0}
                        {#each filteredLabels as label (label.id)}
                            <LabelCard {label} onDelete={requestDeleteLabel} />
                        {/each}
                    {:else if labels.length > 0}
                        <p class="empty">Nincs a keresésnek megfelelő label</p>
                    {:else}
                        <p class="empty">Nincs még label</p>
                    {/if}
                </div>
            </div>
        {/if}

        {#if activeTab === 'git'}
            <div class="tab-section">
                <div class="section-header stack-480 wrap-480">
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

<!--Modálok -->
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
    <!-- Nincs onClose-beli újralekérés: a LabelCreated esemény a projekt teljes csoportjának
         megy ki, tehát a létrehozót is eléri, és a projectStore magától felveszi az új
         címkét. Ez a nézet a store-ból olvas (lásd a feliratkozást feljebb). -->
    <CreateLabelModal
        bind:isOpen={isCreateLabelOpen}
        projectId={project.id}
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
        padding: var(--content-padding);
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

    .tabs {
        display: flex;
        gap: 0.25rem;
        padding: 0.7rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0;
        flex-wrap: nowrap;
        overflow-y: hidden;
    }

    .tab-btn {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        padding: 0.7rem 1rem;
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

    .meta-grid {
        display: grid;
        grid-template-columns: auto 1fr;
        gap: 0.35rem 1rem;
        font-size: 0.9rem;
        border-radius: 6px;
    }

    .meta-label { color: var(--text-secondary); white-space: nowrap; }
    .meta-value { color: var(--text-primary); font-weight: 500; }

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
    .empty {
        font-size: 0.85rem;
        color: var(--text-muted);
        padding: 1rem 0;
    }

    .search-wrapper {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        background: var(--bg-input);
        border: 1px solid var(--border);
        border-radius: var(--border-radius);
        padding: 0.35rem 0.75rem;
        color: var(--text-muted);
        min-width: 0;
    }

    .search-input {
        background: transparent;
        border: none;
        color: var(--text-primary);
        font-size: var(--font-size-sm);
        width: 100%;
        outline: none;
    }

    .search-clear {
        display: flex;
        align-items: center;
        background: none;
        border: none;
        padding: 0;
        cursor: pointer;
        color: var(--text-muted);
    }

    .search-clear:hover {
        color: var(--text-primary);
    }

    .labels-list, .integrations-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .delete-confirm {
        background: var(--accent-red-bg);
        border: 1px solid var(--accent-red);
        border-radius: var(--border-radius);
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
    }

    .project-name-hint {
        color: var(--accent-red);
        font-size: var(--font-size-sm);
    }

    .delete-confirm-actions {
        display: flex;
        gap: 0.5rem;
        justify-content: flex-end;
    }
</style>