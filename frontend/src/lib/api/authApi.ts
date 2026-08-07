import apiClient from './client';

interface LoginRequest {
    email: string;
    password: string;
}

interface RegisterRequest {
    email: string;
    displayName: string;
    password: string;
}

interface RefreshTokenRequest {
    refreshToken: string;
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
    refreshToken: string;
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

interface LoginWithTotpRequest {
    email: string;
    password: string;
    totpToken: string;
}

export async function loginAsync(data: LoginRequest): Promise<AuthResponse> {
    const response = await apiClient.post('/auth/login', data);
    return response.data;
}

export async function registerAsync(data: RegisterRequest): Promise<AuthResponse> {
    const response = await apiClient.post('/auth/register', data);
    return response.data;
}

export async function refreshAsync(data: RefreshTokenRequest): Promise<AuthResponse> {
    const response = await apiClient.post('/auth/refresh', data);
    return response.data;
}

export async function logoutAsync(refreshToken: string): Promise<void> {
    await apiClient.post('/auth/logout', { refreshToken});
}

export async function meAsync(): Promise<AuthResponse> {
    const response = await apiClient.get('/auth/me');
    return response.data;
}

export async function changePasswordAsync(data: ChangePasswordRequest): Promise<void> {
    await apiClient.post('/auth/changepassword', data);
}

export async function updateProfileAsync(data: UpdateProfileRequest): Promise<UserProfileResponse> {
    const response = await apiClient.patch('/auth/profile', data);
    return response.data;
}

//TOTP
export async function setupTotpAsync(): Promise<TotpSetupResponse> {
    const response = await apiClient.post('/auth/totp/setup');
    return response.data;
}

export async function verifyTotpAsync(token: string): Promise<void> {
    await apiClient.post('/auth/totp/verify', { token });
}

export async function disableTotpAsync(): Promise<void> {
    await apiClient.post('/auth/totp/disable');
}

export async function loginWithTotpAsync(data: LoginWithTotpRequest): Promise<AuthResponse> {
    const response = await apiClient.post('/auth/totp/login', data);
    return response.data;
}

export async function resendVerificationAsync(email: string): Promise<void> {
    await apiClient.post('/auth/resend-verification', { email });
}

export async function forgotPasswordAsync(email: string): Promise<void> {
    await apiClient.post('/auth/forgot-password', { email });
}

export async function resetPasswordAsync(token: string, newPassword: string): Promise<void> {
    await apiClient.post('/auth/reset-password', { token, newPassword });
}