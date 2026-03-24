<script lang="ts">
    import { onMount } from 'svelte';
    import { getCommentsAsync, createCommentAsync, deleteCommentAsync, type CommentResponse } from '../api/commentApi';
    import { validateCommentBody } from '../validators';

    export let projectId: string;
    export let taskId: string;
    export let currentUserId: string;

    let comments: CommentResponse[] = [];
    let newComment = '';

    onMount(async () => {
        await loadComments();
    });

    async function loadComments() {
        try {
            comments = await getCommentsAsync(projectId, taskId);
        } catch (e) {
            console.error('Hiba a kommentek lekérésekor!');
        }
    }

    let error ='';
    async function handleAddComment() {
        error = '';
        if (newComment.trim() === '') return;
        const bodyError = validateCommentBody(newComment);
        if(bodyError){
            error = bodyError;
            return;
        }
        try {
            const comment = await createCommentAsync(projectId, taskId, { body: newComment });
            comments = [...comments, comment];
            newComment = '';
        } catch (e) {
            console.error('Hiba a komment hozzáadásakor!');
        }
    }

    async function handleDeleteComment(commentId: string) {
        try {
            await deleteCommentAsync(projectId, taskId, commentId);
            comments = comments.filter(c => c.id !== commentId);
        } catch (e) {
            console.error('Hiba a komment törlésekor!');
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
                    <span class="comment-author">{comment.userName}</span>
                    <span class="comment-date">{formatDate(comment.createdAt)}</span>
                    {#if comment.userId === currentUserId}
                        <button class="delete-btn" on:click={() => handleDeleteComment(comment.id)}>🗑</button>
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
        color: #ccc;
    }

    .comment {
        text-align: left;
        background: #2a2a2a;
        border-radius: 6px;
        padding: 0.75rem;
        margin-bottom: 0.5rem;
        border: 1px solid #333;
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
    }

    .comment-author {
        font-weight: bold;
        font-size: 0.9rem;
    }

    .comment-date {
        font-size: 0.75rem;
        color: #888;
    }

    .delete-btn {
        margin-left: auto;
        background: transparent;
        border: none;
        cursor: pointer;
        color: #aaa;
    }

    .delete-btn:hover {
        color: #ff5555;
    }

    .comment-input {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        margin-top: 1rem;
    }

    .comment-input textarea {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 0.9rem;
        resize: vertical;
        min-height: 80px;
        width: 100%;
    }

    .comment-input button {
        align-self: flex-end;
        padding: 0.4rem 1rem;
        border-radius: 6px;
        cursor: pointer;
    }

    .empty {
        color: #555;
        font-size: 0.85rem;
    }

    #failed {
        color: red;
        white-space: pre-line;
        font-size: 0.85rem;
    }
</style>