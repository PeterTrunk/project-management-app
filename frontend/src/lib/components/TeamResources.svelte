<script lang="ts">
    import { onMount } from 'svelte';
    import { getProjectAttachmentsAsync, getProjectPresignedUrlAsync, uploadToMinIOAsync, confirmProjectUploadAsync } from '../api/attachmentApi';
    import { getTasksAsync, type TaskResponse } from '../api/taskApi';
    import type { AttachmentResponse } from '../api/attachmentApi';
    import AttachmentCard from './AttachmentCard.svelte';

    export let projectId: string;

    let taskAttachments: { task: TaskResponse, attachments: AttachmentResponse[] }[] = [];

    let attachments: AttachmentResponse[] = [];
    let loading = true;
    let isUploading = false;
    let uploadError = '';
    let error = '';
    let uploadProgress = 0;

    onMount(async () => {
        await loadAttachments();
    });

    async function loadAttachments() {
        loading = true;
        error = '';
        try {
            // Projekt szintű attachmentek
            attachments = await getProjectAttachmentsAsync(projectId);
            
            // Task szintű attachmentek
            const tasks = await getTasksAsync(projectId);
            const tasksWithAttachments = tasks.filter(t => t.attachments && t.attachments.length > 0);
            taskAttachments = tasksWithAttachments.map(t => ({
                task: t,
                attachments: t.attachments
            }));
        } catch (e: any) {
            error = 'Hiba történt a fájlok lekérésekor!';
        } finally {
            loading = false;
        }
    }

    async function handleFileUpload(e: Event) {
        const input = e.target as HTMLInputElement;
        if (!input.files || input.files.length === 0) return;

        const files = Array.from(input.files);
        isUploading = true;
        uploadError = '';

        for (const file of files) {
            try {
                // 1. Presigned URL kérés
                const { presignedUrl, storageKey } = await getProjectPresignedUrlAsync(
                    projectId, {
                        fileName: file.name,
                        contentType: file.type,
                        sizeBytes: file.size
                    }
                );

                // 2. Direkt feltöltés MinIO-ra
                await uploadToMinIOAsync(presignedUrl, file, (progress) => {
                    uploadProgress = progress;
                });

                // 3. Confirm
                const uploaded = await confirmProjectUploadAsync(projectId, { storageKey });
                attachments = [uploaded, ...attachments];

            } catch (e: any) {
                uploadError = e.response?.data ?? 'Hiba történt a feltöltéskor!';
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
            <label class="upload-btn btn-icon-text" class:loading={isUploading}>
                {#if isUploading}
                    Feltöltés... {uploadProgress > 0 ? uploadProgress + '%' : ''}
                {:else}
                    +<span class="btn-text"> Feltöltés</span>
                {/if}
                <input type="file" style="display: none" multiple on:change={handleFileUpload} disabled={isUploading} />
            </label>
        </div>
        
        {#if isUploading && uploadProgress > 0}
            <div class="progress-bar">
                <div class="progress-fill" style="width: {uploadProgress}%"></div>
            </div>
        {/if}
        
        {#if uploadError}
            <p class="msg error">{uploadError}</p>
        {/if}
    </div>
    <div class="resources-content">
        <!-- Projekt szintű attachmentek -->
        <div class="section">
            <h3>Projekt dokumentumok</h3>
            {#if attachments.length === 0}
                <p class="empty">Nincsenek projekt szintű dokumentumok</p>
            {:else}
                <div class="attachments-list">
                    {#each attachments as attachment (attachment.id)}
                        <AttachmentCard
                            {attachment}
                            {projectId}
                            taskId={null}
                            onDelete={(id) => attachments = attachments.filter(a => a.id !== id)}
                        />
                    {/each}
                </div>
            {/if}
        </div>

        <!-- Task szintű attachmentek -->
        {#if taskAttachments.length > 0}
            <div class="section">
                <h3>Task csatolmányok</h3>
                {#each taskAttachments as { task, attachments: taskFiles }}
                    <div class="task-group">
                        <h4 class="task-key">{task.taskKey} {task.title}</h4>
                        <div class="attachments-list">
                            {#each taskFiles as attachment (attachment.id)}
                                <AttachmentCard
                                    {attachment}
                                    {projectId}
                                    taskId={task.id}
                                    onDelete={() => {}}
                                />
                            {/each}
                        </div>
                    </div>
                {/each}
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

    .msg {
        font-size: 0.9rem;
        padding: 0.5rem 0;
    }

    .msg.error {
        color: var(--accent-red);
        white-space: pre-line;
        width: 100%;
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
</style>