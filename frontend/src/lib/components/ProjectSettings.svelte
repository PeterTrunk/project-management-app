<script lang="ts">
    import type { ProjectResponse } from '../api/projectApi';
    import { validateDescription, validateProjName } from '../validators';

    import { setActiveProject, setProjects, projectStore } from '../../lib/stores/projectStore';

    import { updateProjectAsync, archiveProjectAsync, unarchiveProjectAsync, deleteProject, getProjectByIdAsync } from '../../lib/api/projectApi'

    export let project: ProjectResponse;

    async function refreshProject() {
        const updated = await getProjectByIdAsync(project.id);
        setActiveProject(updated);
    }

    import ConfirmModal from '../components/ConfirmModal.svelte';

    let isConfirmOpen = false;
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

<div>
    <h1>{project.name}</h1>
    <div class="divider">
        <form>
            <p>
                Projekt neve:
                <input bind:value={name} placeholder="Max 120 karakter">
            </p>
            <p>
                Leírás: 
                <textarea bind:value={description} placeholder="Max 1000 karakter" id="desc"></textarea>
            </p>
            <p>Projekt tulajdonosa: <span>{project.ownerName}</span> (Nem állítható)</p>
            <p>Projekthez tartozó kulcs: <span>{project.projKey}</span> (Nem állítható)</p>
            <p>
            {#if project.isArchived}
                A Projekt <span>Arhivált</span> állapotú
                <button type="button" on:click={() => openConfirm(
                    'Projekt dearchiválása',
                    'Biztosan dearchiválni szeretnéd a projektet?',
                    handleUnarchive
                )}>Projekt dearchiválása</button>
            {:else}
                A Projekt <span>Aktív</span> állapotú.
                <button type="button" on:click={() => openConfirm(
                    'Projekt archiválása',
                    'Biztosan archiválni szeretnéd a projektet?',
                    handleArchive
                )}>Projekt archiválása</button>
            {/if}
            </p>
            <p>Létrehozás ideje: <span>{new Date(project.createdAt).toLocaleDateString('hu-HU')}</span></p>
            <p>Adatok legutóbbi változásának ideje: <span>{new Date(project.updatedAt).toLocaleDateString('hu-HU')}</span></p>
            {#if error}
                <p id="failed">{error}</p>
            {/if}
            {#if success}
                <p id="success">{success}</p>
            {/if}
            <button type="button" on:click={() => openConfirm(
                'Módosítások mentése',
                'Biztosan menteni szeretnéd a változtatásokat?',
                handleUpdate
            )}>Módosítások mentése</button>
        </form>
    </div>
    <div class="divider">
    <button id="delete" on:click={() => openConfirm('Projekt Törlése'
        ,'Biztosan törlöd véglegesen a projektet? (arhiválás funkció: megtekintésre elérhető marad a projekt, nincs adattörlés, projekt újra aktiválható, felhasználói akciók letiltva lesznek)'
        , handleDelete
        )}>Projekt Törlése</button>
    </div>
    <!--Modal-->
    {#if isConfirmOpen}
    <ConfirmModal
        bind:isOpen={isConfirmOpen}
        title={confirmTitle}
        message={confirmMessage}
        confirmText="Megerősítés"
        onConfirm={confirmAction}
        />
    {/if}
</div>

<style>
    div {
        max-width: 600px;
        padding: 2rem;
        display: flex;
        flex-direction: column;
        gap: 1rem;
        margin: 0 auto;
    }

    h1 {
        margin-bottom: 0.5rem;
        font-size: 1.8rem;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    p {
        display: flex;
        flex-direction: column;
        gap: 0.3rem;
    }

    input, textarea {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    textarea {
        resize: vertical;
        min-height: 180px;
        width: 100%;
    }

    input:focus, textarea:focus {
        outline: none;
        border-color: #666;
    }

    span {
        font-weight: bold;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    .divider {
        width: calc(100vw - 250px);
        position: relative;
        left: 50%;
        transform: translateX(-50%);
        border-top: 1px solid #333;
        padding-top: 1rem;
    }

    #success { 
        color: greenyellow; 
    }
    #failed { 
        color: red; white-space: 
        pre-line; 
    }
    #delete { 
        color: red; 
        margin-top: 0.5rem; 
    }
</style>