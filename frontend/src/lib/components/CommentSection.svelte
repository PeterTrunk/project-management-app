<script lang="ts">
    import { getCommentsAsync, createCommentAsync, deleteCommentAsync } from '../api/commentApi';
    import {
        commentStore, setComments, commentsForTask,
        handleCommentAdded, handleCommentDeleted
    } from '../stores/commentStore';

    import { notify } from '../stores/notificationStore';

    import { Trash2 } from 'lucide-svelte';

    export let projectId: string;
    export let taskId: string;
    export let currentUserId: string;

    let newComment = '';
    let error ='';

    //A kommentek a KÖZPONTI tárolóból jönnek, a feladat azonosítója szerint. A két esemény
    //kezelője szintén központi (signalRClientService) - korábban ez a komponens iratkozott fel
    //rájuk, és minden idegen komment érkezésekor újratöltötte az egész listát.
    $: comments = commentsForTask($commentStore, taskId) ?? [];

    //A betöltés a feladat VÁLTOZÁSÁRA fut, és csak ha még nincs betöltve. A tároló
    //gyorsítótár: egy visszanyitott feladat kommentjei már bent vannak, és közben az
    //események tartották frissen - akkor is, amíg a részletnézet zárva volt.
    $: if (taskId && commentsForTask($commentStore, taskId) === undefined) {
        loadCommentsAsync(taskId);
    }

    async function loadCommentsAsync(id: string) {
        try {
            setComments(id, await getCommentsAsync(projectId, id));
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba a kommentek lekérésekor!';
            error = message;
            notify.error(message);
        }
    }

    async function handleAddComment() {
        error = '';
        try {
            const comment = await createCommentAsync(projectId, taskId, { body: newComment });
            //A saját kommentünket a POST válaszából vesszük fel, nem az eseményre várva: egy
            //szétesett kapcsolat mellett se veszítse el a felhasználó a saját műveletét.
            //A központi kezelő gárdája miatt a RÁ ÉRKEZŐ esemény nem duplikálja.
            handleCommentAdded(comment);
            newComment = '';
            notify.success('Komment hozzáadva!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba a komment hozzáadásakor!';
            error = message;
            notify.error(message);
        }
    }

    async function handleDeleteComment(commentId: string) {
        error = '';
        try {
            await deleteCommentAsync(projectId, taskId, commentId);
            handleCommentDeleted({ taskId, commentId });
            notify.success('Komment törölve!');
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba a komment törlésekor!';
            error = message;
            notify.error(message);
        }
    }

    function formatDate(date: Date): string {
        return new Date(date).toLocaleString('hu-HU', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    }
</script>

<div class="section">
    <h3>Kommentek</h3>
    {#if comments.length > 0}
        {#each comments as comment}
            <div class="comment">
                <div class="comment-header">
                    <span class="comment-author truncate">{comment.userName}</span>
                    <span class="comment-date">{formatDate(comment.createdAt)}</span>
                    {#if comment.userId === currentUserId}
                        <button class="delete-btn" on:click={() => handleDeleteComment(comment.id)}>
                            <Trash2 size={14} />
                        </button>
                    {/if}
                </div>
                <p>{comment.body}</p>
            </div>
        {/each}
    {:else}
        <p class="empty">Nincs komment</p>
    {/if}

    <div class="comment-input">
        <textarea bind:value={newComment} placeholder="Írj egy kommentet..."></textarea>
        {#if error}
            <p id="failed">{error}</p>
        {/if}
        <button on:click={handleAddComment}>Küldés</button>
    </div>
</div>

<style>
    .section h3 {
        font-size: 1rem;
        margin-bottom: 0.5rem;
        color: var(--text-secondary);
    }

    .comment {
        text-align: left;
        background: var(--bg-hover);
        border-radius: 6px;
        padding: 0.75rem;
        margin-bottom: 0.5rem;
        border: 1px solid var(--border-subtle);
    }

    .comment p {
        white-space: pre-wrap;
        word-break: break-word;
    }

    .comment-header {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.25rem;
        flex-wrap: wrap;
    }

    .comment-author {
        font-weight: bold;
        font-size: 0.9rem;
        color: var(--text-primary);
        min-width: 0;
        flex: 1;
    }

    .comment-date {
        font-size: 0.75rem;
        color: var(--text-muted);
        flex-shrink: 0;
    }

    .delete-btn {
        margin-left: auto;
        background: transparent;
        border: none;
        cursor: pointer;
        color: var(--text-secondary);
        display: flex;
        align-items: center;
        padding: 0.15rem;
        border-radius: 3px;
        flex-shrink: 0;
    }

    .delete-btn:hover {
        color: var(--accent-red);
        background: var(--accent-red-bg);
    }

    .comment-input {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        margin-top: 1rem;
    }

    .comment-input textarea {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 0.9rem;
        resize: vertical;
        min-height: 80px;
        width: 100%;
    }

    .comment-input textarea:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .comment-input button {
        align-self: flex-end;
        padding: 0.4rem 1rem;
        border-radius: 6px;
        cursor: pointer;
    }

    .empty {
        color: var(--text-muted);
        font-size: 0.85rem;
    }

    #failed {
        color: var(--accent-red);
        white-space: pre-line;
        font-size: 0.85rem;
        word-break: break-word;
    }
</style>