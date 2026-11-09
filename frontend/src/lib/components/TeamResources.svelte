<script lang="ts">
    import { getProjectPresignedUrlAsync, uploadToStorageAsync, confirmProjectUploadAsync } from '../api/attachmentApi';
    import AttachmentCard from './AttachmentCard.svelte';
    import {
        attachmentStore, handleAttachmentUploaded, handleAttachmentDeleted,
        projectAttachments, attachmentsByTask
    } from '../stores/attachmentStore';

    import { notify } from '../stores/notificationStore';

    export let projectId: string;

    let isUploading = false;
    let uploadProgress = 0;

    //A csatolmányok a KÖZPONTI tárolóból jönnek, a projekt betöltésével együtt - a két
    //feliratkozás és a lokális lista megszűnt. A komponens korábban minden
    //csatolmány-eseményre újratöltötte a projekt csatolmányait ÉS a teljes feladatgráfot
    //(hatókör nélkül, tehát a lezárt sprintek minden feladatával); ez volt a réteg legdrágább
    //ismétlődő kérése.
    //
    //A feladat kulcsa a CSATOLMÁNY soráról jön, nem a feladatlistából: az a lezárt sprintek
    //feladatait nem tartalmazza, tehát a csoportok fejléce üresen maradna azoknál.
    $: attachments = projectAttachments($attachmentStore.attachments);
    $: taskAttachments = attachmentsByTask($attachmentStore.attachments);

    //A kártya a sikeres törlés után hívja. A tárolóból vesszük ki, nem lokális listából:
    //a SAJÁT törlésünk így azonnal látszik, a rá érkező esemény pedig idempotens.
    function handleAttachmentDeletedLocally(attachmentId: string) {
        handleAttachmentDeleted({ attachmentId });
    }

    let searchQuery = '';

    $: filteredAttachments = attachments.filter(a =>
        a.fileName.toLowerCase().includes(searchQuery.toLowerCase())
    );

    $: filteredTaskAttachments = taskAttachments.map(ta => ({
        ...ta,
        attachments: ta.attachments.filter(a =>
            a.fileName.toLowerCase().includes(searchQuery.toLowerCase())
        )
    })).filter(ta => ta.attachments.length > 0);

    async function handleFileUpload(e: Event) {
        const input = e.target as HTMLInputElement;
        if (!input.files || input.files.length === 0) return;

        const files = Array.from(input.files);
        isUploading = true;

        for (const file of files) {
            try {
                // 1. Presigned URL kérés
                const { uploadUrl, fields, storageKey } = await getProjectPresignedUrlAsync(
                    projectId, {
                        fileName: file.name,
                        contentType: file.type,
                        sizeBytes: file.size
                    }
                );

                // 2. Direkt feltöltés az objektumtárolóba
                await uploadToStorageAsync({ uploadUrl, fields }, file, (progress) => {
                    uploadProgress = progress;
                });

                // 3. Confirm
                const uploaded = await confirmProjectUploadAsync(projectId, { storageKey });
                //A tárolóba a megerősítés VÁLASZÁBÓL vesszük fel, nem az eseményre várva:
                //egy szétesett kapcsolat mellett se veszítse el a felhasználó a saját
                //feltöltését. A rá érkező eseményt a tároló gárdája nem duplikálja.
                handleAttachmentUploaded(uploaded);
                notify.success(`Fájl feltöltve: ${file.name}`);

            } catch (e: any) {
                notify.error(e.response?.data ?? e.message ?? 'Hiba történt a feltöltéskor!');
            }
        }

        isUploading = false;
        uploadProgress = 0;
        input.value = '';
    }

</script>

<div class="team-resources-container">
    <div class="resources-toolbar">
        <div class="toolbar-row wrap-480">
            <h2>Projekt dokumentumok ({attachments.length})</h2>
            <div class="toolbar-actions">
                <label class="upload-btn btn-icon-text" class:loading={isUploading}>
                    {#if isUploading}
                        Feltöltés... {uploadProgress > 0 ? uploadProgress + '%' : ''}
                    {:else}
                        +<span class="btn-text"> Feltöltés</span>
                    {/if}
                    <input type="file" style="display: none" multiple on:change={handleFileUpload} disabled={isUploading} />
                </label>

                <input 
                    class="search-input-sm" 
                    placeholder="Keresés..." 
                    bind:value={searchQuery} 
                />
            </div>
        </div>

        {#if isUploading && uploadProgress > 0}
            <div class="progress-bar">
                <div class="progress-fill" style="width: {uploadProgress}%"></div>
            </div>
        {/if}
    </div>
    <div class="resources-content">
        <!-- Projekt szintű attachmentek -->
        <div class="section">
            <h3>Projekt dokumentumok</h3>
            {#if attachments.length === 0}
                <p class="empty">Nincsenek projekt szintű dokumentumok</p>
            {:else if filteredAttachments.length === 0}
                <p class="empty">Nincs találat a keresési feltételekre</p>
            {:else}
                <div class="attachments-list">
                    {#each filteredAttachments as attachment (attachment.id)}
                        <AttachmentCard
                            {attachment}
                            {projectId}
                            taskId={null}
                            onDelete={handleAttachmentDeletedLocally}
                        />
                    {/each}
                </div>
            {/if}
        </div>

        <!-- Task szintű attachmentek -->
        {#if taskAttachments.length > 0}
            <div class="section">
                <h3>Task csatolmányok</h3>
                {#if filteredTaskAttachments.length === 0}
                    <p class="empty">Nincs találat a keresési feltételekre</p>
                {:else}
                    {#each filteredTaskAttachments as group (group.taskId)}
                        <div class="task-group">
                            <h4 class="task-key">{group.taskKey} {group.taskTitle}</h4>
                            <div class="attachments-list">
                                {#each group.attachments as attachment (attachment.id)}
                                    <AttachmentCard
                                        {attachment}
                                        {projectId}
                                        taskId={group.taskId}
                                        onDelete={handleAttachmentDeletedLocally}
                                    />
                                {/each}
                            </div>
                        </div>
                    {/each}
                {/if}
            </div>
        {/if}
    </div>
</div>

<style>
    .team-resources-container {
        display: flex;
        flex-direction: column;
        height: 100%;
        overflow: hidden;
    }

    .resources-toolbar {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        padding: var(--toolbar-padding);
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .toolbar-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.75rem;
    }

    .toolbar-actions {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .progress-bar {
        width: 100%;
        height: 6px;
        background: var(--bg-input);
        border-radius: var(--border-radius);
        overflow: hidden;
    }
    
    .progress-fill {
        height: 100%;
        background: var(--accent-green);
        transition: width 0.2s ease;
    }

    .resources-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: var(--text-secondary);
    }

    .resources-content {
        padding: var(--content-padding);
        overflow-y: auto;
        flex: 1;
    }

    .section {
        margin-bottom: 1.5rem;
    }

    .section h3 {
        font-size: 0.85rem;
        color: var(--text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.05em;
        margin: 0 0 0.75rem;
        border-bottom: 1px solid var(--border);
        padding-bottom: 0.5rem;
    }

    .attachments-list {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
    }

    .upload-btn {
        background: var(--accent-green-bg);
        border: 1px solid var(--accent-green);
        color: var(--accent-green);
        padding: 0.4rem 0.8rem;
        border-radius: 6px;
        cursor: pointer;
        font-size: 0.9rem;
        transition: background 0.15s;
    }

    .upload-btn:hover { background: var(--accent-green); color: #fff; }
    .upload-btn.loading { opacity: 0.5; cursor: not-allowed; }

    .search-input-sm {
        font-size: 0.9rem;
        padding: 0.35rem 0.75rem;
        border-radius: var(--border-radius);
        border: 1px solid var(--border);
        background: var(--bg-input);
        color: var(--text-primary);
        width: 200px;
    }

    .search-input-sm:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .task-group {
        margin-bottom: 1rem;
    }

    .task-key {
        font-size: 0.85rem;
        color: var(--text-muted);
        margin: 0 0 0.5rem;
    }

    .loading {
        text-align: center;
        padding: 1rem;
        color: var(--text-muted);
    }

    .empty {
        color: var(--text-muted);
        font-size: var(--font-size-sm);
        padding: 1rem 0;
        text-align: center;
    }
</style>