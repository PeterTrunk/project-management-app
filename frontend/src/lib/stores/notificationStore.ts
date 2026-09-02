import { writable } from 'svelte/store';

export type NotificationType = 'success' | 'error' | 'warning' | 'info';

export interface Notification {
    id: string;
    type: NotificationType;
    message: string;
    autoDismiss: boolean;
    duration: number;
}

function createNotificationStore() {
    const { subscribe, update } = writable<Notification[]>([]);

    const MAX_MOBILE = 2;
    const MAX_DESKTOP = 4;

    function add(type: NotificationType, message: string) {
        const autoDismiss = type === 'success' || type === 'info';
        const duration = type === 'success' ? 5000 : type === 'info' ? 8000 : 0;

        const notification: Notification = {
            id: crypto.randomUUID(),
            type,
            message,
            autoDismiss,
            duration
        };

        update(notifications => {
            const maxCount = window.innerWidth < 768 ? MAX_MOBILE : MAX_DESKTOP;
            const updated = [...notifications, notification];
            //Ha túl lépi a maximumot, legrégebbit eltávolítjuk
            return updated.length > maxCount ? updated.slice(1) : updated;
        });

        //Auto dismiss
        if (autoDismiss && duration > 0) {
            setTimeout(() => remove(notification.id), duration);
        }
    }

    function remove(id: string) {
        update(notifications => notifications.filter(n => n.id !== id));
    }

    return {
        subscribe,
        success: (message: string) => add('success', message),
        error: (message: string) => add('error', message),
        warning: (message: string) => add('warning', message),
        info: (message: string) => add('info', message),
        remove
    };
}

export const notify = createNotificationStore();