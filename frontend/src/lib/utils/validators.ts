import { dateNotPast, required, maxLength, minLength, emailFormat, passwordStrength, dateOrder } from './validationHelpers';

export function validateLogin(email: string, password: string): string | null {
    const errors: string[] = [];
    const req = required(email, 'Email');
    if (req) errors.push(req);
    else {
        const format = emailFormat(email);
        if (format) errors.push(format);
        const max = maxLength(email, 254, 'Email');
        if (max) errors.push(max);
    }
    const pwdReq = required(password, 'Jelszó');
    if (pwdReq) errors.push(pwdReq);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateRegister(email: string, displayName: string, password: string): string | null {
    const errors: string[] = [];
    const req = required(email, 'Email');
    if (req) errors.push(req);
    else {
        const format = emailFormat(email);
        if (format) errors.push(format);
        const max = maxLength(email, 254, 'Email');
        if (max) errors.push(max);
    }
    const nameReq = required(displayName, 'Megjelenítési név');
    if (nameReq) errors.push(nameReq);
    else {
        const nameMin = minLength(displayName, 3, 'Megjelenítési név');
        if (nameMin) errors.push(nameMin);
        const nameMax = maxLength(displayName, 120, 'Megjelenítési név');
        if (nameMax) errors.push(nameMax);
    }
    const pwdReq = required(password, 'Jelszó');
    if (pwdReq) errors.push(pwdReq);
    else {
        const pwdMin = minLength(password, 8, 'Jelszó');
        if (pwdMin) errors.push(pwdMin);
        errors.push(...passwordStrength(password));
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validatePassword(password: string): string | null {
    const errors: string[] = [];
    const req = required(password, 'Jelszó');
    if (req) errors.push(req);
    else {
        const min = minLength(password, 8, 'Jelszó');
        if (min) errors.push(min);
        errors.push(...passwordStrength(password));
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateDisplayName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Megjelenítési név');
    if (req) errors.push(req);
    else {
        const min = minLength(name, 3, 'Megjelenítési név');
        if (min) errors.push(min);
        const max = maxLength(name, 120, 'Megjelenítési név');
        if (max) errors.push(max);

        //Ugyanaz a korlátozás, mint szerveroldalon: a név activity-leírásokba interpolálódik,
        //ezért a markup-karaktereket kizárjuk. Ez csak kényelmi visszajelzés - a tényleges kikényszerítés a backend validátoré.
        if (/[<>&"'`]/.test(name))
            errors.push('A megjelenítési név nem tartalmazhat < > & " \' ` karaktereket!');
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateEmail(email: string): string | null {
    const errors: string[] = [];
    const req = required(email, 'Email');
    if (req) errors.push(req);
    else {
        const format = emailFormat(email);
        if (format) errors.push(format);
        const max = maxLength(email, 254, 'Email');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateProjName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Projekt név');
    if (req) errors.push(req);
    else {
        const max = maxLength(name, 120, 'Projekt név');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc, 1000, 'Leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateBoardName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Board név');
    if (req) errors.push(req);
    else {
        const min = minLength(name, 3, 'Board név');
        if (min) errors.push(min);
        const max = maxLength(name, 120, 'Board név');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateBoardDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc ?? '', 500, 'Board leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateColumnName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Oszlop név');
    if (req) errors.push(req);
    else {
        const min = minLength(name, 3, 'Oszlop név');
        if (min) errors.push(min);
        const max = maxLength(name, 80, 'Oszlop név');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateColumnStatus(status: string): string | null {
    const errors: string[] = [];
    const req = required(status, 'Státusz');
    if (req) errors.push(req);
    else {
        const min = minLength(status, 3, 'Státusz');
        if (min) errors.push(min);
        const max = maxLength(status, 32, 'Státusz');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskTitle(title: string): string | null {
    const errors: string[] = [];
    const req = required(title, 'Cím');
    if (req) errors.push(req);
    else {
        const max = maxLength(title, 200, 'Task cím');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc ?? '', 250, 'Task leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskDueDate(date: Date): string | null {
    if (!date) return null;
    return dateNotPast(date.toString(), 'Határidő');
}

export function validateSprintName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Sprint név');
    if (req) errors.push(req);
    else {
        const min = minLength(name, 3, 'Sprint név');
        if (min) errors.push(min);
        const max = maxLength(name, 80, 'Sprint név');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateSprintGoal(goal: string): string | null {
    const errors: string[] = [];
    if (goal) {
        const min = minLength(goal, 3, 'Sprint cél');
        if (min) errors.push(min);
        const max = maxLength(goal, 500, 'Sprint cél');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateSprintDates(startDate: string, endDate: string): string | null {
    return dateOrder(startDate, endDate);
}

export function validateCreateBoard(name: string, description?: string): string | null {
    const errors: string[] = [];
    const nameError = validateBoardName(name);
    if (nameError) errors.push(nameError);
    if (description) {
        const descError = validateBoardDescription(description);
        if (descError) errors.push(descError);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateUpdateBoard(name?: string | null, description?: string | null): string | null {
    const errors: string[] = [];
    if (name !== null && name !== undefined) {
        const nameError = validateBoardName(name);
        if (nameError) errors.push(nameError);
    }
    if (description !== null && description !== undefined) {
        const descError = validateBoardDescription(description);
        if (descError) errors.push(descError);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateCreateColumn(name: string, mapsToStatus: string, position: number): string | null {
    const errors: string[] = [];
    const nameError = validateColumnName(name);
    if (nameError) errors.push(nameError);
    const statusError = validateColumnStatus(mapsToStatus);
    if (statusError) errors.push(statusError);
    if (position <= 0) errors.push('A 0-ás pozíció a Backlog oszlopnak van fenntartva!');
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateColumnOrder(position: number): string | null {
    if (position <= 0) return 'A 0-ás pozíció a Backlog oszlopnak van fenntartva!';
    return null;
}

export function validateUpdateColumn(name?: string | null, mapsToStatus?: string | null): string | null {
    const errors: string[] = [];
    if (name !== null && name !== undefined) {
        const nameError = validateColumnName(name);
        if (nameError) errors.push(nameError);
    }
    if (mapsToStatus !== null && mapsToStatus !== undefined) {
        const statusError = validateColumnStatus(mapsToStatus);
        if (statusError) errors.push(statusError);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateComment(body: string): string | null {
    const errors: string[] = [];
    const req = required(body, 'Komment');
    if (req) errors.push(req);
    else {
        const max = maxLength(body, 2000, 'Komment');
        if (max) errors.push(max);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateCreateIntegration(provider: string, repoFullName: string, webhookSecret: string): string | null {
    const errors: string[] = [];
    
    const providerReq = required(provider, 'Provider');
    if (providerReq) errors.push(providerReq);
    else if (!['GitHub', 'GitLab'].includes(provider))
        errors.push('Érvénytelen provider! Lehetséges értékek: GitHub, GitLab');
    
    const repoReq = required(repoFullName, 'Repository');
    if (repoReq) errors.push(repoReq);
    else {
        const repoMax = maxLength(repoFullName, 200, 'Repository');
        if (repoMax) errors.push(repoMax);
        if (!/^[a-zA-Z0-9_.-]+\/[a-zA-Z0-9_.-]+$/.test(repoFullName))
            errors.push('Érvénytelen repository formátum! (owner/repo)');
    }
    
    const secretReq = required(webhookSecret, 'Webhook secret');
    if (secretReq) errors.push(secretReq);
    else {
        const secretMin = minLength(webhookSecret, 16, 'Webhook secret');
        if (secretMin) errors.push(secretMin);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateWebhookSecret(secret: string): string | null {
    const errors: string[] = [];
    const req = required(secret, 'Webhook secret');
    if (req) errors.push(req);
    else {
        const min = minLength(secret, 16, 'Webhook secret');
        if (min) errors.push(min);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateLabel(name: string, color: string): string | null {
    const errors: string[] = [];
    
    const nameReq = required(name, 'Címke név');
    if (nameReq) errors.push(nameReq);
    else {
        const nameMax = maxLength(name, 40, 'Címke név');
        if (nameMax) errors.push(nameMax);
    }
    
    const colorReq = required(color, 'Szín');
    if (colorReq) errors.push(colorReq);
    else if (!/^#[0-9A-Fa-f]{6}$/.test(color))
        errors.push('Érvénytelen hex szín formátum! (pl. #FF0000)');
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateCreateProject(name: string, projKey: string, description?: string | null): string | null {
    const errors: string[] = [];
    
    const nameError = validateProjName(name);
    if (nameError) errors.push(nameError);
    
    const keyReq = required(projKey, 'Projekt kulcs');
    if (keyReq) errors.push(keyReq);
    else {
        const keyMin = minLength(projKey, 2, 'Projekt kulcs');
        if (keyMin) errors.push(keyMin);
        const keyMax = maxLength(projKey, 10, 'Projekt kulcs');
        if (keyMax) errors.push(keyMax);
        if (!/^[A-Z0-9]+$/.test(projKey))
            errors.push('A projekt kulcs csak nagybetűket és számokat tartalmazhat!');
    }
    
    if (description) {
        const descError = validateDescription(description);
        if (descError) errors.push(descError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateUpdateProject(name?: string | null, description?: string | null): string | null {
    const errors: string[] = [];
    
    if (name !== null && name !== undefined) {
        const nameError = validateProjName(name);
        if (nameError) errors.push(nameError);
    }
    
    if (description !== null && description !== undefined) {
        const descError = validateDescription(description);
        if (descError) errors.push(descError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateCreateSprint(name: string, goal?: string | null, startDate?: Date | null, endDate?: Date | null): string | null {
    const errors: string[] = [];
    
    const nameError = validateSprintName(name);
    if (nameError) errors.push(nameError);
    
    if (goal) {
        const goalError = validateSprintGoal(goal);
        if (goalError) errors.push(goalError);
    }
    
    if (startDate && endDate) {
        const dateError = validateSprintDates(startDate.toString(), endDate.toString());
        if (dateError) errors.push(dateError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateUpdateSprint(name?: string | null, goal?: string | null, startDate?: Date | null, endDate?: Date | null): string | null {
    const errors: string[] = [];
    
    if (name !== null && name !== undefined) {
        const nameError = validateSprintName(name);
        if (nameError) errors.push(nameError);
    }
    
    if (goal !== null && goal !== undefined) {
        const goalError = validateSprintGoal(goal);
        if (goalError) errors.push(goalError);
    }
    
    if (startDate && endDate) {
        const dateError = validateSprintDates(startDate.toString(), endDate.toString());
        if (dateError) errors.push(dateError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateDateRange(dateFrom: string, dateTo: string): string | null {
    const errors: string[] = [];
    const req1 = required(dateFrom, 'Kezdő dátum');
    if (req1) errors.push(req1);
    const req2 = required(dateTo, 'Befejező dátum');
    if (req2) errors.push(req2);
    if (!req1 && !req2) {
        const dateError = dateOrder(dateFrom, dateTo);
        if (dateError) errors.push(dateError);
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateCreateTask(title: string, description?: string | null, dueDate?: Date | null): string | null {
    const errors: string[] = [];
    
    const titleError = validateTaskTitle(title);
    if (titleError) errors.push(titleError);
    
    if (description) {
        const descError = validateTaskDescription(description);
        if (descError) errors.push(descError);
    }
    
    if (dueDate) {
        const dateError = validateTaskDueDate(dueDate);
        if (dateError) errors.push(dateError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateUpdateTask(title?: string | null, description?: string | null, dueDate?: Date | null): string | null {
    const errors: string[] = [];
    
    if (title !== null && title !== undefined) {
        const titleError = validateTaskTitle(title);
        if (titleError) errors.push(titleError);
    }
    
    if (description !== null && description !== undefined) {
        const descError = validateTaskDescription(description);
        if (descError) errors.push(descError);
    }
    
    if (dueDate) {
        const dateError = validateTaskDueDate(dueDate);
        if (dateError) errors.push(dateError);
    }
    
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateMemberRole(projectRole: string): string | null {
    const errors: string[] = [];
    const req = required(projectRole, 'Szerepkör');
    if (req) errors.push(req);
    else if (!['Admin', 'Member', 'Viewer'].includes(projectRole))
        errors.push('Érvénytelen szerepkör! Lehetséges értékek: Admin, Member, Viewer');
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateInviteLink(maxUses?: number | null, expiresInDays?: number | null): string | null {
    const errors: string[] = [];
    if (maxUses !== null && maxUses !== undefined && maxUses < 1)
        errors.push('A maximális használatok száma legalább 1 kell legyen!');
    if (expiresInDays !== null && expiresInDays !== undefined) {
        if (expiresInDays < 1) errors.push('A lejárati idő legalább 1 nap kell legyen!');
        if (expiresInDays > 30) errors.push('A lejárati idő maximum 30 nap lehet!');
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTotpToken(token: string): string | null {
    const errors: string[] = [];
    const req = required(token, 'TOTP kód');
    if (req) errors.push(req);
    else {
        if (token.length !== 6) errors.push('A TOTP kódnak pontosan 6 karakter hosszúnak kell lennie!');
        if (!/^[0-9]+$/.test(token)) errors.push('A TOTP kód csak számokat tartalmazhat!');
    }
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateChangePassword(currentPassword: string, newPassword: string): string | null {
    const errors: string[] = [];
    const currentReq = required(currentPassword, 'Jelenlegi jelszó');
    if (currentReq) errors.push(currentReq);
    const newError = validatePassword(newPassword);
    if (newError) errors.push(newError);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateFileUpload(fileName: string, sizeBytes: number, contentType: string): string | null {
    const errors: string[] = [];
    
    const nameMax = maxLength(fileName, 255, 'Fájlnév');
    if (nameMax) errors.push(nameMax);
    
    if (sizeBytes <= 0) errors.push('A fájl mérete nem lehet nulla!');
    if (sizeBytes > 64 * 1024 * 1024) errors.push('A fájl mérete maximum 64MB lehet!');
    
    const typeReq = required(contentType, 'Fájl típus');
    if (typeReq) errors.push(typeReq);
    
    return errors.length > 0 ? errors.join('\n') : null;
}