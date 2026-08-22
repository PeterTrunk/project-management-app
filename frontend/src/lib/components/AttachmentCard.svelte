<script lang="ts">
    import type { AttachmentResponse } from '../api/attachmentApi';
    import { downloadAttachmentAsync, deleteAttachmentAsync  } from '../api/attachmentApi';

    import { Image, FileText, Sheet, FilePen, Paperclip, Download, Trash2 } from 'lucide-svelte';

    export let attachment: AttachmentResponse;
    export let projectId: string;
    export let taskId: string | null = null;
    export let onDelete: (attachmentId: string) => void = () => {};
    
    function getAttachmentIcon(attachmentType: string): any {
        switch (attachmentType) {
            case 'image':       return Image;
            case 'pdf':         return FileText;
            case 'spreadsheet': return Sheet;
            case 'document':    return FilePen;
            default:            return Paperclip;
        }
    }

    function formatFileSize(bytes: number): string {
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    async function handleDownload() {
        try {
            await downloadAttachmentAsync(projectId, attachment.id, attachment.fileName);
        } catch (e) {
            console.error('Hiba a letöltéskor!');
        }
    }

    async function handleDelete() {
        try {
            await deleteAttachmentAsync(projectId, attachment.id);
            onDelete(attachment.id);
        } catch (e) {
            console.error('Hiba a törléskor!');
        }
    }
</script>

<div class="attachment-card">
    <span class="attachment-icon">
        <svelte:component this={getAttachmentIcon(attachment.attachmentType)} size={18} />
    </span>
    <div class="attachment-info">
        <span class="attachment-name">{attachment.fileName}</span>
        <span class="attachment-meta">
            {formatFileSize(attachment.sizeBytes)} · {attachment.uploadedByName}
        </span>
    </div>
    <div class="attachment-actions">
        <button class="download-btn" on:click={handleDownload} title="Letöltés">
            <Download size={15} />
        </button>
        <button class="delete-btn" on:click={handleDelete} title="Törlés">
            <Trash2 size={15} />
        </button>
    </div>
</div>

<style>
    .attachment-card {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.5rem 0.75rem;
        background: var(--bg-hover);
        border-radius: 6px;
        border: 1px solid var(--border-subtle);
        transition: border-color 0.15s;
    }

    .attachment-card:hover {
        border-color: var(--border-hover);
    }

    .attachment-icon {
        display: flex;
        align-items: center;
        color: var(--text-muted);
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
        color: var(--text-primary);
    }

    .attachment-meta {
        font-size: 0.75rem;
        color: var(--text-muted);
    }

    .attachment-actions {
        display: flex;
        gap: 0.25rem;
    }

    .download-btn, .delete-btn {
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        cursor: pointer;
        padding: 0.25rem;
        border-radius: 4px;
        color: var(--text-secondary);
        transition: background 0.15s, color 0.15s;
    }

    .download-btn:hover { background: var(--accent-green-bg); color: var(--accent-green); }
    .delete-btn:hover   { background: var(--accent-red-bg);   color: var(--accent-red); }
</style>