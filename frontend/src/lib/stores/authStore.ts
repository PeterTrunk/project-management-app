import { writable } from 'svelte/store';
import { tokenStore } from './tokenStore';

// Ki van bejelentkezve? Milyen adatokat tárolunk?
interface User {
    userId: string;
    email: string;
    displayName: string;
    isTotpEnabled: boolean;
    isEmailVerified: boolean;
}

interface AuthState {
    token: string | null;
    user: User | null;
    isAuthenticated: boolean;
}

//Kezdeti állapot
const initialState: AuthState = {
    token: null,
    user: null,
    isAuthenticated: false
};

//initialState példányosítása
export const authStore = writable<AuthState>(initialState);

export function login(token: string, user: User) {
    tokenStore.set(token);
    authStore.set({ token, user, isAuthenticated: true });
}

export function logout() {
    tokenStore.clear();
    authStore.set({ token: null, user: null, isAuthenticated: false });
}