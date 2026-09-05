export function required(value: string, fieldName: string): string | null {
    if (!value || value.trim() === '') return `${fieldName} megadása kötelező!`;
    return null;
}

export function maxLength(value: string, max: number, fieldName: string): string | null {
    if (value && value.length > max) return `${fieldName} maximum ${max} karakter lehet!`;
    return null;
}

export function minLength(value: string, min: number, fieldName: string): string | null {
    if (value && value.length < min) return `${fieldName} minimum ${min} karakter lehet!`;
    return null;
}

export function emailFormat(value: string): string | null {
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value))
        return 'Érvénytelen email formátum! (pl. email@example.com)';
    return null;
}

export function passwordStrength(pwd: string): string[] {
    const errors: string[] = [];
    if (!/[A-Z]/.test(pwd)) errors.push('A jelszó tartalmazzon nagybetűt!');
    if (!/[0-9]/.test(pwd)) errors.push('A jelszó tartalmazzon számot!');
    if (!/[!@#$%^&*]/.test(pwd)) errors.push('A jelszó tartalmazzon speciális karaktert (!@#$%^&*)!');
    return errors;
}

export function dateNotPast(value: string, fieldName: string): string | null {
    if (!value) return null;
    if (new Date(value) < new Date()) return `${fieldName} nem lehet múltbeli dátum!`;
    return null;
}

export function dateOrder(start: string, end: string): string | null {
    if (!start || !end) return null;
    if (new Date(start) >= new Date(end))
        return 'A befejezési dátum nem lehet korábbi mint a kezdés!';
    return null;
}