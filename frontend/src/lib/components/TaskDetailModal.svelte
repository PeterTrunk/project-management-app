<script lang="ts">
    import { onMount } from 'svelte';
    import { setActiveTask } from '../stores/taskStore';
    import type { TaskResponse } from '../api/taskApi';
    import { getCommentsAsync, createCommentAsync, deleteCommentAsync, type CommentResponse } from '../api/commentApi';
    import { authStore } from '../stores/authStore';

    export let task: TaskResponse;
    export let projectId: string;
    export let isTaskDetailOpen = false;
    export let onClose: () => void = () => {};

    let comments: CommentResponse[] = [];
    let newComment = '';
    let currentUserId = '';

    
    let modalRef: HTMLElement;

    authStore.subscribe(state => {
        currentUserId = state.user?.userId ?? '';
    });

    onMount(async () => {
        modalRef?.focus();
        await loadComments();
    });

    async function loadComments() {
        try {
            comments = await getCommentsAsync(projectId, task.id);
        } catch (e) {
            console.error('Hiba a kommentek lekérésekor!');
        }
    }

    async function handleAddComment() {
        if (newComment.trim() === '') return;
        try {
            const comment = await createCommentAsync(projectId, task.id, { body: newComment });
            comments = [...comments, comment];
            newComment = '';
        } catch (e) {
            console.error('Hiba a komment hozzáadásakor!');
        }
    }

    async function handleDeleteComment(commentId: string) {
        try {
            await deleteCommentAsync(projectId, task.id, commentId);
            comments = comments.filter(c => c.id !== commentId);
        } catch (e) {
            console.error('Hiba a komment törlésekor!');
        }
    }

    function closeModal() {
        isTaskDetailOpen = false;
        onClose();
    }
</script>

<div class="modal-overlay" on:click|self={closeModal}
    //Accessability configolása
    bind:this={modalRef} //Autofocus helyett.
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    >
    <div class="modal-content">
        <div class="modal-header">
            <button class="close-btn" on:click={closeModal}>✕</button>
            <h1>{task.title}</h1>
            <p>{task.taskKey} · {task.status}</p>
        </div>

        <div class="left-column">
            <p>Létrehozó: <span>{task.createdByName}</span></p>
            <p>Határidő: {task.dueDate ? new Date(task.dueDate).toLocaleDateString('hu-HU') : 'Nincs határidő'}</p>
            <p>Létrehozás ideje: <span>{new Date(task.createdAt).toLocaleDateString('hu-HU')}</span></p>
            <p>Adatok legutóbbi változásának ideje: <span>{new Date(task.updatedAt).toLocaleDateString('hu-HU')}</span></p>
            <!-- Assignee-k -->
            <div class="section">
                <h3>Hozzárendelt személyek</h3>
                {#if task.assigneeNames.length > 0}
                    {#each task.assigneeNames as name}
                        <span class="tag">{name}</span>
                    {/each}
                {:else}
                    <p class="empty">Nincs hozzárendelt személy</p>
                {/if}
            </div>
            <!-- Labelek -->
            <div class="section">
                <h3>Labelek</h3>
                {#if task.labelNames.length > 0}
                    {#each task.labelNames as label}
                        <span class="tag">{label}</span>
                    {/each}
                {:else}
                    <p class="empty">Nincs label</p>
                {/if}
            </div>
            <div id="optional-fields">
                <h2>Opcionális mezők</h2>
                <p>Leírás: {task.description ?? 'Nincs leírás'}</p>
                <p>Prioritás: {task.priority}</p>
                <p>Becsült idő: {task.estimateInMinutes ?? 'Nincs becslés'}</p>
            </div>
            <!-- Commit linkek -->
            <div class="section">
                <h3>Commit linkek</h3>
                {#if task.commitLinks.length > 0}
                    {#each task.commitLinks as link}
                        <a href={link} target="_blank">{link}</a>
                    {/each}
                {:else}
                    <p class="empty">Nincs commit link</p>
                {/if}
            </div>

            <!-- PR linkek -->
            <div class="section">
                <h3>PR linkek</h3>
                {#if task.prLinks.length > 0}
                    {#each task.prLinks as link}
                        <a href={link} target="_blank">{link}</a>
                    {/each}
                {:else}
                    <p class="empty">Nincs PR link</p>
                {/if}
            </div>
        </div>

        <div class="right-column">
            <!-- Kommentek -->
            <div class="section">
                <h3>Kommentek</h3>
                {#if comments.length > 0}
                    {#each comments as comment}
                        <div class="comment">
                            <div class="comment-header">
                                <span class="comment-author">{comment.userName}</span>
                                <span class="comment-date">{new Date(comment.createdAt).toLocaleDateString('hu-HU')}</span>
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
                
                <!-- Új komment hozzáadása -->
                <div class="comment-input">
                    <textarea bind:value={newComment} placeholder="Írj egy kommentet..."></textarea>
                    <button on:click={handleAddComment}>Küldés</button>
                </div>
            </div>
        </div>
    </div>
</div>

<style>
    .modal-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100%;
        height: 100%;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }


    .modal-content {
        background: #1e1e1e;
        border-radius: 8px;
        width: 900px;
        max-width: 95vw;
        max-height: 85vh;
        overflow-y: auto;
        position: relative;
        padding: 2rem;
        display: grid;
        grid-template-columns: 1fr 1fr;
        grid-template-rows: auto 1fr;
        gap: 2rem;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        background: transparent;
        border: none;
        color: #aaa;
        font-size: 1.2rem;
        cursor: pointer;
        z-index: 10;
    }

    .close-btn:hover {
        color: white;
    }
    
    .modal-header {
        grid-column: 1 / -1;
        border-bottom: 1px solid #333;
        padding-bottom: 1rem;
    }

    .left-column {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        overflow-y: auto;
    }

    .right-column {
        display: flex;
        flex-direction: column;
        gap: 1rem;
        border-left: 1px solid #333;
        padding-left: 2rem;
        overflow-y: auto;
    }

    .modal-content h1 {
        margin-bottom: 0.5rem;
        font-size: 1.5rem;
    }

    textarea {
        background: #2a2a2a;
        border: 1px solid #444;
        border-radius: 6px;
        color: white;
        padding: 0.5rem;
        font-size: 1rem;
        width: 100%;
    }

    textarea:focus {
        outline: none;
        border-color: #666;
    }

    button {
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        width: fit-content;
        align-self: center;
    }

    #optional-fields {
        background: #161616;
        border-radius: 8px;
        padding: 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        margin-top: 0.5rem;
        border: 1px solid #2a2a2a;
    }

    #optional-fields h2 {
        font-size: 1rem;
        color: #aaa;
        margin-bottom: 0.25rem;
    }
    span{
        font-weight: bold;
    }
</style>