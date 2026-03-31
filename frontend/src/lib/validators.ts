export function validateDescription(desc: string): string | null {
    if(desc.length > 1000) return 'Leírás maximum 1000 karakter hosszú lehet!\n';
    return null;
}

export function validateProjName(name: string): string | null {
    if(name === '' || name === null) return 'Név szükséges!\n';
    if(name.length > 120) return 'A projekt neve nem lehet hosszabb mint 120 karakter!\n';
    return null;
}

export function validateDisplayName(name: string): string | null {
    if(name.length < 3) return 'A felhasználónév nem lehet rövidebb 3 karakternél!\n';
    if(name.length > 120) return 'A felhasználónév nem lehet hosszabb mint 120 karakter!\n';
    return null;
}

export function validatePassword(pwd: string): string | null {
    let aggregateError = '';
    if(pwd === '' || pwd === null) aggregateError += 'Jelszó nincs megadva!\n'
    if(pwd.length < 8) aggregateError += 'A jelszó minimum 8 karakter!\n';
    if(!/[A-Z]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon nagybetűt!\n';
    if(!/[0-9]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon számot!\n';
    if(!/[!@#$%^&*]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon speciális karaktert (!@#$%^&*)!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateEmail(email: string): string | null {
    let aggregateError = '';
    if(email === '' || email === null ) aggregateError += 'Email nincs megadva!\n';
    if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) aggregateError += 'Hibás email szignatúra!\n';
    if(email.length > 255) aggregateError += 'Email nem lehet hosszabb mint 255 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateBoardName(name: string){
    if(name === '' || name === null) return 'Board név kötelező!'
    if(name.length > 120) return 'Board név maximum 120 karakter hosszú lehet';
    if(name.length < 3) return 'Board név minimum 3 karakter hosszú lehet';
    return null;
}

export function validateBoardDescription(desc: string){
    if(desc !=null && desc.length > 500) return 'Board leírás maximum 500 karakter hosszú lehet';
    return null;
}

export function validateColumnName(name: string): string | null {
    let aggregateError = '';
    if(name === '' || name === null) aggregateError += 'Név szükséges!\n';
    if(name.length > 80) aggregateError += 'Az oszlop név nem lehet hosszabb mint 80 karakter!\n';
    if(name.length < 3) aggregateError += 'Az oszlop név nem lehet rövidebb mint 3 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateColumnStatus(status: string): string | null {
    let aggregateError = '';
    if(status ==='' || status === null) aggregateError += 'Státusz szükséges!\n';
    if(status.length > 32) aggregateError += 'Az oszlop státusza nem lehet hosszabb mint 32 karakter!\n';
    if(status.length < 3) aggregateError += 'Az oszlop státusza nem lehet rövidebb mint 3 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateTaskTitle(title: string): string | null {
    let aggregateError = '';
    if(title ==='' || title === null) aggregateError += 'Cím szükséges!\n';
    if(title.length > 200) aggregateError += 'Task címe nem lehet hosszabb mint 200 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateTaskDescription(desc: string): string | null {
    let aggregateError = '';
    if(desc.length > 250) aggregateError += 'Task leírás nem lehet hosszabb mint 250 karakter!\n';
    return aggregateError === '' ? null : aggregateError;
}

export function validateTaskDueDate(date: Date): string | null {
    if(!date) return null;
    if(new Date(date) < new Date()) return 'Határidő nem lehet múltbeli!';
    return null;
}

export function validateCommentBody(body: string): string | null {
    if(body.length > 2000) return 'Maximum 2000 karakter hosszú komment megengedett!';
    return null
}

export function validateSprintName(name: string): string | null {
    let aggregateError = '';
    if(name ==='' || name === null ) aggregateError += 'Sprint neve nem lehet üres!\n';
    if(name.length < 3) aggregateError += 'Sprint neve nem lehet rövidebb 3 karakternél!\n';
    if(name.length > 500) aggregateError += 'Sprint neve nem lehet hosszabb 500 karakternél!\n';
    return aggregateError;
}

export function validateSprintGoal(goal: string): string | null {
    let aggregateError = '';
    if(goal.length < 3) aggregateError += 'Sprint célja nem lehet rövidebb 3 karakternél!\n';
    if(goal.length > 500) aggregateError += 'Sprint célja nem lehet hosszabb 500 karakternél!\n';
    return aggregateError;
}

export function validateSprintDates(startDate: string, endDate: string): string | null {
    if (!startDate || !endDate) return null;
    const start = new Date(startDate).getTime();
    const end = new Date(endDate).getTime();
    if (start >= end)
        return 'A befejezési dátum nem lehet korábban mint a kezdés!\n';
    return null;
}
