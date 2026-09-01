<script lang="ts">
    import type { AttachmentResponse } from '../api/attachmentApi';
    import { downloadAttachmentAsync, deleteAttachmentAsync  } from '../api/attachmentApi';

    import { Image, FileText, Sheet, FilePen, Paperclip, Download, Trash2, Check, X } from 'lucide-svelte';

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

    let showConfirm = false;
    let deleting = false;

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
        deleting = true;
        try {
            await deleteAttachmentAsync(projectId, attachment.id);
            onDelete(attachment.id);
        } catch (e) {
            console.error('Hiba a törléskor!');
        } finally {
            deleting = false;
            showConfirm = false;
        }
    }
</script>

<div class="attachment-card card-overflow-hidden">
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
        {#if showConfirm}
            <span class="confirm-text">Törlöd?</span>
            <button class="confirm-yes-btn" on:click={handleDelete} disabled={deleting} title="Megerősítés">
                <Check size={15} />
            </button>
            <button class="confirm-cancel-btn" on:click={() => showConfirm = false} title="Mégsem">
                <X size={15} />
            </button>
        {:else}
            <button class="download-btn" on:click={handleDownload} title="Letöltés">
                <Download size={15} />
            </button>
            <button class="delete-btn" on:click={() => showConfirm = true} title="Törlés">
                <Trash2 size={15} />
            </button>
        {/if}
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
        min-width: 0;
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
        align-items: center;
        gap: 0.25rem;
        flex-shrink: 0;
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

    .confirm-text {
        font-size: 0.8rem;
        color: var(--text-secondary);
        white-space: nowrap;
    }

    .confirm-yes-btn, .confirm-cancel-btn {
        display: flex;
        align-items: center;
        border: none;
        cursor: pointer;
        padding: 0.25rem;
        border-radius: 4px;
        transition: background 0.15s, color 0.15s;
    }

    .confirm-yes-btn {
        background: var(--accent-green-bg);
        color: var(--accent-green);
    }
    .confirm-yes-btn:hover { background: var(--accent-green); color: #fff; }
    .confirm-yes-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .confirm-cancel-btn {
        background: var(--bg-hover);
        color: var(--text-secondary);
    }
    .confirm-cancel-btn:hover { background: var(--border-hover); color: var(--text-primary); }

    .download-btn:hover { background: var(--accent-green-bg); color: var(--accent-green); }
    .delete-btn:hover   { background: var(--accent-red-bg);   color: var(--accent-red); }
</style>