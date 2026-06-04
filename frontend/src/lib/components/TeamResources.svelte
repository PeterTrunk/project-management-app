<script lang="ts">
    import { onMount } from 'svelte';
    import { getProjectAttachmentsAsync, uploadProjectAttachmentAsync, getTaskAttachmentsAsync } from '../api/attachmentApi';
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

        const file = input.files[0];
        isUploading = true;
        uploadError = '';

        try {
            const uploaded = await uploadProjectAttachmentAsync(projectId, file);
            attachments = [uploaded, ...attachments];
        } catch (e: any) {
            uploadError = 'Hiba történt a feltöltéskor!';
        } finally {
            isUploading = false;
            input.value = '';
        }
    }
</script>

<div class="team-resources-container">
    <div class="resources-toolbar">
        <h2>Projekt dokumentumok ({attachments.length})</h2>
        <label class="upload-btn" class:loading={isUploading}>
            {isUploading ? 'Feltöltés...' : '+ Feltöltés'}
            <input
                type="file"
                style="display: none"
                on:change={handleFileUpload}
                disabled={isUploading}
            />
        </label>
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
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 1rem;
        background: var(--bg-secondary);
        border-bottom: 1px solid var(--border);
        flex-shrink: 0;
    }

    .resources-toolbar h2 {
        font-size: 1rem;
        margin: 0;
        color: var(--text-secondary);
    }

    .resources-content {
        padding: 1rem;
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