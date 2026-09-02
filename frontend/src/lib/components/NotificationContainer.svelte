<script lang="ts">
    import { notify, type Notification } from '../stores/notificationStore';
    import { X, CircleCheckBig, CircleAlert, TriangleAlert, Info } from 'lucide-svelte';

    function getIcon(type: Notification['type']) {
        switch (type) {
            case 'success': 
            return CircleCheckBig;
            case 'error': return CircleAlert;
            case 'warning': return TriangleAlert;
            case 'info': return Info;
        }
    }
</script>

{#if $notify.length > 0}
    <div class="notification-container">
        {#each $notify as notification (notification.id)}
            <div class="notification notification-{notification.type}">
                <span class="notification-icon">
                    <svelte:component this={getIcon(notification.type)} size={18} />
                </span>
                <span class="notification-message">{notification.message}</span>
                <button
                    class="notification-close"
                    on:click={() => notify.remove(notification.id)}
                >
                    <X size={14} />
                </button>
            </div>
        {/each}
    </div>
{/if}

<style>
    .notification-container {
        position: fixed;
        bottom: 1.5rem;
        right: 1.5rem;
        display: flex;
        flex-direction: column;
        align-items: flex-end;
        gap: 0.5rem;
        z-index: 9999;
        max-width: 380px;
        width: calc(100vw - 3rem);
        word-break: break-word;
        overflow-wrap: break-word;
    }

    .notification {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.75rem 1rem;
        border-radius: var(--border-radius);
        border: 1px solid;
        font-size: var(--font-size-sm);
        animation: slideIn 0.2s ease;
        box-shadow: 0 4px 12px var(--shadow);
    }

    .notification-success {
        background: var(--accent-green-bg);
        color: var(--accent-green);
        border-color: var(--accent-green);
    }

    .notification-error {
        background: var(--accent-red-bg);
        color: var(--accent-red);
        border-color: var(--accent-red);
    }

    .notification-warning {
        background: var(--accent-yellow-bg);
        color: var(--accent-yellow);
        border-color: var(--accent-yellow);
    }

    .notification-info {
        background: var(--accent-blue-bg);
        color: var(--accent-blue);
        border-color: var(--accent-blue);
    }

    .notification-icon {
        flex-shrink: 0;
        display: flex;
        align-items: center;
        padding-top: 0.1rem;
    }

    .notification-message {
        flex: 1;
        line-height: 1.4;
        min-width: 0;
        word-break: break-word;
    }

    .notification-close {
        flex-shrink: 0;
        align-self: flex-start;
        background: transparent;
        border: none;
        color: inherit;
        cursor: pointer;
        padding: 0.1rem;
        display: flex;
        align-items: center;
        opacity: 0.7;
        border-radius: 4px;
    }

    .notification-close:hover {
        opacity: 1;
        background: transparent;
    }

    @keyframes slideIn {
        from {
            transform: translateY(100%);
            opacity: 0;
        }
        to {
            transform: translateY(0);
            opacity: 1;
        }
    }

    @media (max-width: 768px) {
        .notification-container {
            bottom: auto;
            top: 1rem;
            left: 50%;
            right: auto;
            transform: translateX(-50%);
            width: calc(100vw - 2rem);
            max-width: 400px;
        }

        @keyframes slideIn {
            from {
                transform: translateX(-50%) translateY(-100%);
                opacity: 0;
            }
            to {
                transform: translateX(-50%) translateY(0);
                opacity: 1;
            }
        }
    }
</style>