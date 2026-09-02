import { required, maxLength, minLength, emailFormat, passwordStrength, dateNotPast, dateOrder } from './validationHelpers';

export function validateDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc, 1000, 'Leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateProjName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Projekt név');
    if (req) errors.push(req);
    const max = maxLength(name, 120, 'Projekt név');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateDisplayName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Megjelenítési név');
    if (req) errors.push(req);
    const min = minLength(name, 3, 'Megjelenítési név');
    if (min) errors.push(min);
    const max = maxLength(name, 120, 'Megjelenítési név');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validatePassword(pwd: string): string | null {
    const errors: string[] = [];
    const req = required(pwd, 'Jelszó');
    if (req) errors.push(req);
    const min = minLength(pwd, 8, 'Jelszó');
    if (min) errors.push(min);
    const strength = passwordStrength(pwd);
    errors.push(...strength);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateEmail(email: string): string | null {
    const errors: string[] = [];
    const req = required(email, 'Email');
    if (req) errors.push(req);
    const format = emailFormat(email);
    if (format) errors.push(format);
    const max = maxLength(email, 254, 'Email');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateBoardName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Board név');
    if (req) errors.push(req);
    const min = minLength(name, 3, 'Board név');
    if (min) errors.push(min);
    const max = maxLength(name, 120, 'Board név');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateBoardDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc, 500, 'Board leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateColumnName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Oszlop név');
    if (req) errors.push(req);
    const min = minLength(name, 3, 'Oszlop név');
    if (min) errors.push(min);
    const max = maxLength(name, 80, 'Oszlop név');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateColumnStatus(status: string): string | null {
    const errors: string[] = [];
    const req = required(status, 'Státusz');
    if (req) errors.push(req);
    const min = minLength(status, 3, 'Státusz');
    if (min) errors.push(min);
    const max = maxLength(status, 32, 'Státusz');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskTitle(title: string): string | null {
    const errors: string[] = [];
    const req = required(title, 'Cím');
    if (req) errors.push(req);
    const max = maxLength(title, 200, 'Task cím');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskDescription(desc: string): string | null {
    const errors: string[] = [];
    const max = maxLength(desc, 250, 'Task leírás');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateTaskDueDate(date: Date): string | null {
    if (!date) return null;
    if (new Date(date) < new Date()) return 'Határidő nem lehet múltbeli!';
    return null;
}

export function validateCommentBody(body: string): string | null {
    const errors: string[] = [];
    const max = maxLength(body, 2000, 'Komment');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateSprintName(name: string): string | null {
    const errors: string[] = [];
    const req = required(name, 'Sprint név');
    if (req) errors.push(req);
    const min = minLength(name, 3, 'Sprint név');
    if (min) errors.push(min);
    const max = maxLength(name, 80, 'Sprint név');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateSprintGoal(goal: string): string | null {
    const errors: string[] = [];
    const min = minLength(goal, 3, 'Sprint cél');
    if (min) errors.push(min);
    const max = maxLength(goal, 500, 'Sprint cél');
    if (max) errors.push(max);
    return errors.length > 0 ? errors.join('\n') : null;
}

export function validateSprintDates(startDate: string, endDate: string): string | null {
    return dateOrder(startDate, endDate);
}