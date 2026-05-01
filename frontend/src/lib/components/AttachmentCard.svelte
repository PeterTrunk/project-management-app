<script lang="ts">
    import type { AttachmentResponse } from '../api/attachmentApi';
    import { downloadAttachmentAsync, deleteTaskAttachmentAsync, downloadProjectAttachmentAsync, deleteProjectAttachmentAsync } from '../api/attachmentApi';

    export let attachment: AttachmentResponse;
    export let projectId: string;
    export let taskId: string | null = null;
    export let onDelete: (attachmentId: string) => void = () => {};

    function getAttachmentIcon(attachmentType: string): string {
        switch (attachmentType) {
            case 'image': return '🖼';
            case 'pdf': return '📄';
            case 'spreadsheet': return '📊';
            case 'document': return '📝';
            default: return '📎';
        }
    }

    function formatFileSize(bytes: number): string {
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    async function handleDownload() {
        try {
            if (taskId) {
                await downloadAttachmentAsync(projectId, taskId, attachment.id, attachment.fileName);
            } else {
                await downloadProjectAttachmentAsync(projectId, attachment.id, attachment.fileName);
            }
        } catch (e) {
            console.error('Hiba a letöltéskor!');
        }
    }

    async function handleDelete() {
        try {
            if (taskId) {
                await deleteTaskAttachmentAsync(projectId, taskId, attachment.id);
            } else {
                await deleteProjectAttachmentAsync(projectId, attachment.id);
            }
            onDelete(attachment.id);
        } catch (e) {
            console.error('Hiba a törléskor!');
        }
    }
</script>

<div class="attachment-card">
    <span class="attachment-icon">
        {getAttachmentIcon(attachment.attachmentType)}
    </span>
    <div class="attachment-info">
        <span class="attachment-name">{attachment.fileName}</span>
        <span class="attachment-meta">
            {formatFileSize(attachment.sizeBytes)} · {attachment.uploadedByName}
        </span>
    </div>
    <div class="attachment-actions">
        <button class="download-btn" on:click={handleDownload} title="Letöltés">
            ⬇
        </button>
        <button class="delete-btn" on:click={handleDelete} title="Törlés">
            🗑
        </button>
    </div>
</div>

<style>
    .attachment-card {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.5rem 0.75rem;
        background: #2a2a2a;
        border-radius: 6px;
        border: 1px solid #333;
    }

    .attachment-card:hover {
        border-color: #555;
    }

    .attachment-icon {
        font-size: 1.2rem;
        flex-shrink: 0;
    }

    .attachment-info {
        display: flex;
        flex-direction: column;
        gap: 0.1rem;
        flex: 1;
    }

    .attachment-name {
        font-size: 0.9rem;
        color: #ddd;
    }

    .attachment-meta {
        font-size: 0.75rem;
        color: #666;
    }

    .attachment-actions {
        display: flex;
        gap: 0.25rem;
    }

    .download-btn, .delete-btn {
        background: transparent;
        border: none;
        cursor: pointer;
        font-size: 1rem;
        padding: 0.25rem;
        border-radius: 4px;
    }

    .download-btn:hover { background: #1a3a1a; }
    .delete-btn:hover { background: #3a1a1a; }
</style>