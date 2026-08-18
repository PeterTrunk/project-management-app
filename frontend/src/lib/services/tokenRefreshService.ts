import { tokenStore } from '../stores/tokenStore';
import apiClient from '../api/client';

let refreshTimer: ReturnType<typeof setTimeout> | null = null;

export function scheduleTokenRefresh() {
    if (refreshTimer) clearTimeout(refreshTimer);
    
    //1 perccel lejárat előtt újítjuk meg
    const expiryMinutes = parseInt(import.meta.env.VITE_JWT_ACCESS_TOKEN_LIFETIME ?? '120');
    
    const expiryMs = expiryMinutes * 60 * 1000;

    const refreshInMs = expiryMs - 60 * 1000;
    
    refreshTimer = setTimeout(async () => {
        console.log('scheduleTokenRefresh hívva!');
        try {
            const response = await apiClient.post('/auth/refresh');
            tokenStore.set(response.data.token);
            scheduleTokenRefresh();
        } catch {
            tokenStore.clear();
            window.location.href = '/#/';
        }
    }, refreshInMs);
}

export function cancelTokenRefresh() {
    if (refreshTimer) {
        clearTimeout(refreshTimer);
        refreshTimer = null;
    }
}