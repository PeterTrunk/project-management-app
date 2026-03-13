export function validateDescription(desc: string): string | null {
    if (desc.length > 1000) return 'Leírás maximum 1000 karakter hosszú lehet!\n';
    return null;
}

export function validateProjName(name: string): string | null {
    if (name.length > 120) return 'A projekt neve nem lehet hosszabb mint 120 karakter!\n';
    return null;
}