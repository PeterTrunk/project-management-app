export function validateDescription(desc: string): string | null {
    if (desc.length > 1000) return 'Leírás maximum 1000 karakter hosszú lehet!\n';
    return null;
}

export function validateProjName(name: string): string | null {
    if (name.length > 120) return 'A projekt neve nem lehet hosszabb mint 120 karakter!\n';
    return null;
}

export function validateDisplayName(name: string): string | null{
    if (name.length < 3) return 'A felhasználónév nem lehet rövidebb 3 karakternél!\n';
    if (name.length > 120) return 'A felhasználónév nem lehet hosszabb mint 120 karakter!\n';
    return null;
}

export function validatePassword(pwd: string): string | null {
    let aggregateError = '';
    if (pwd.length < 8) aggregateError += 'A jelszó minimum 8 karakter!\n';
    if (!/[A-Z]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon nagybetűt!\n';
    if (!/[0-9]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon számot!\n';
    if (!/[!@#$%^&*]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon speciális karaktert (!@#$%^&*)!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateEmail(email: string): string | null{
    let aggregateError = '';
    if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) aggregateError += 'Hibás email szignatúra!\n';
    if(email.length > 255) aggregateError += 'Email nem lehet hosszabb mint 255 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}