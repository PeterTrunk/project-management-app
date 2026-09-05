import { tokenStore } from '../stores/tokenStore';
import axios from 'axios';

let refreshPromise: Promise<string> | null = null;

export async function refreshTokenOnce(): Promise<string> {
    //Ha már fut egyszer egy refresh akkor ugyanazt a Promise-t adjuk vissza.
    if (refreshPromise) return refreshPromise;
    
    refreshPromise = axios.post(
        `${import.meta.env.VITE_API_URL || 'http://localhost:5178'}/api/auth/refresh`,
        {},
        { withCredentials: true }
    )
    .then(response => {
        const newToken = response.data.token;
        tokenStore.set(newToken);
        return newToken;
    })
    .finally(() => {
        //Refresh után töröljük a promise-t.
        refreshPromise = null;
    });

    return refreshPromise;
}