export function validateDescription(desc: string): string | null {
    if (desc.length > 1000) return 'Leírás maximum 1000 karakter hosszú lehet!\n';
    return null;
}

export function validateProjName(name: string): string | null {
    if(name === '' || name === null) return 'Név szükséges!\n';
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
    if(pwd === '' || pwd === null) aggregateError += 'Jelszó nincs megadva!\n'
    if (pwd.length < 8) aggregateError += 'A jelszó minimum 8 karakter!\n';
    if (!/[A-Z]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon nagybetűt!\n';
    if (!/[0-9]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon számot!\n';
    if (!/[!@#$%^&*]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon speciális karaktert (!@#$%^&*)!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateEmail(email: string): string | null{
    let aggregateError = '';
    if(email === '' || email === null ) aggregateError += 'Email nincs megadva!\n';
    if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) aggregateError += 'Hibás email szignatúra!\n';
    if(email.length > 255) aggregateError += 'Email nem lehet hosszabb mint 255 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateColumnName(name: string): string | null{
    let aggregateError = '';
    if(name === '' || name === null) aggregateError += 'Név szükséges!\n';
    if(name.length > 80) aggregateError += 'Az oszlop név nem lehet hosszabb mint 80 karakter!\n';
    if(name.length < 3) aggregateError += 'Az oszlop név nem lehet rövidebb mint 3 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateColumnStatus(status: string): string | null{
    let aggregateError = '';
    if(status ==='' || status === null) aggregateError += 'Státusz szükséges!\n';
    if(status.length > 32) aggregateError += 'Az oszlop státusza nem lehet hosszabb mint 32 karakter!\n';
    if(status.length < 3) aggregateError += 'Az oszlop státusza nem lehet rövidebb mint 3 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}