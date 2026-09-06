import apiClient from './client';
import { validateTotpToken, validateChangePassword, validateLogin, validateRegister, validatePassword, validateDisplayName, validateEmail } from '../utils/validators';

interface LoginRequest {
    email: string;
    password: string;
    rememberMe: boolean;
}

interface LoginWithTotpRequest {
    email: string;
    password: string;
    totpToken: string;
    rememberMe: boolean;
}

interface RegisterRequest {
    email: string;
    displayName: string;
    password: string;
}

interface ChangePasswordRequest {
    currentPassword: string;
    newPassword: string;
}

interface UpdateProfileRequest {
    displayName: string;
}

interface UserProfileResponse {
    userId: string;
    email: string;
    displayName: string;
}

interface AuthResponse {
    token: string;
    userId: string;
    email: string;
    displayName: string;
    requiresTotp?: boolean;
    isTotpEnabled?: boolean;
    isEmailVerified: boolean;
}

interface TotpSetupResponse {
    secretKey: string;
    otpAuthUri: string;
}

export async function loginAsync(data: LoginRequest): Promise<AuthResponse> {
    const error = validateLogin(data.email, data.password);
    if (error) throw new Error(error);

    const response = await apiClient.post('/auth/login', data);
    return response.data;
}

export async function registerAsync(data: RegisterRequest): Promise<AuthResponse> {
    const error = validateRegister(data.email, data.displayName, data.password);
    if (error) throw new Error(error);

    const response = await apiClient.post('/auth/register', data);
    return response.data;
}

export async function refreshAsync(): Promise<AuthResponse> {
    const response = await apiClient.post('/auth/refresh');
    return response.data;
}

export async function logoutAsync(): Promise<void> {
    await apiClient.post('/auth/logout');
}

export async function meAsync(): Promise<AuthResponse> {
    const response = await apiClient.get('/auth/me');
    return response.data;
}

export async function changePasswordAsync(data: ChangePasswordRequest): Promise<void> {
    const error = validateChangePassword(data.currentPassword, data.newPassword);
    if (error) throw new Error(error);

    await apiClient.post('/auth/changepassword', data);
}

export async function updateProfileAsync(data: UpdateProfileRequest): Promise<UserProfileResponse> {
    const error = validateDisplayName(data.displayName);
    if (error) throw new Error(error);

    const response = await apiClient.patch('/auth/profile', data);
    return response.data;
}

//TOTP
export async function setupTotpAsync(): Promise<TotpSetupResponse> {
    const response = await apiClient.post('/auth/totp/setup');
    return response.data;
}

export async function verifyTotpAsync(token: string): Promise<void> {
    const error = validateTotpToken(token);
    if (error) throw new Error(error);
    
    await apiClient.post('/auth/totp/verify', { token });
}

export async function disableTotpAsync(currentPassword: string): Promise<void> {
    if (!currentPassword) throw new Error('A jelenlegi jelszó megadása kötelező!');

    await apiClient.post('/auth/totp/disable', { currentPassword });
}

export async function loginWithTotpAsync(data: LoginWithTotpRequest): Promise<AuthResponse> {
    const errors: string[] = [];
    const loginError = validateLogin(data.email, data.password);
    if (loginError) errors.push(loginError);
    const totpError = validateTotpToken(data.totpToken);
    if (totpError) errors.push(totpError);
    if (errors.length > 0) throw new Error(errors.join('\n'));
    
    const response = await apiClient.post('/auth/totp/login', data);
    return response.data;
}

export async function resendVerificationAsync(email: string): Promise<void> {
    await apiClient.post('/auth/resend-verification', { email });
}

export async function forgotPasswordAsync(email: string): Promise<void> {
    const error = validateEmail(email);
    if (error) throw new Error(error);

    await apiClient.post('/auth/forgot-password', { email });
}

export async function resetPasswordAsync(token: string, newPassword: string): Promise<void> {
    const error = validatePassword(newPassword);
    if (error) throw new Error(error);

    await apiClient.post('/auth/reset-password', { token, newPassword });
}