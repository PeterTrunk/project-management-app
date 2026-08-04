import { writable } from 'svelte/store';

// Ki van bejelentkezve? Milyen adatokat tárolunk?
interface User {
    userId: string;
    email: string;
    displayName: string;
    isTotpEnabled: boolean;
}

interface AuthState {
    token: string | null;
    refreshToken: string | null;
    user: User | null;
    isAuthenticated: boolean;
}

//Kezdeti állapot
const initialState: AuthState = {
    token: localStorage.getItem('token'),
    refreshToken: localStorage.getItem('refreshToken'),
    user: null,
    isAuthenticated: !!localStorage.getItem('token')
};

//initialState példányosítása
export const authStore = writable<AuthState>(initialState);

export function login(token: string, refreshToken: string, user: User) {
    localStorage.setItem('token', token);
    localStorage.setItem('refreshToken', refreshToken);
    authStore.set({ token, refreshToken, user, isAuthenticated: true });
}

export function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    authStore.set({ token: null, refreshToken: null, user: null, isAuthenticated: false });
}