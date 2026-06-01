import { writable } from 'svelte/store';

type Theme = 'dark' | 'light';

const savedTheme = (localStorage.getItem('theme') as Theme) ?? 'dark';

export const themeStore = writable<Theme>(savedTheme);

export function toggleTheme() {
    themeStore.update(t => {
        const newTheme = t === 'dark' ? 'light' : 'dark';
        localStorage.setItem('theme', newTheme);
        return newTheme;
    });
}

export function setTheme(theme: Theme) {
    localStorage.setItem('theme', theme);
    themeStore.set(theme);
}