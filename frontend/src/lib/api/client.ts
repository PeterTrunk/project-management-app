import axios from 'axios';
import { tokenStore } from '../stores/tokenStore';
import { refreshTokenOnce } from './tokenRefresh';

const apiClient = axios.create({
    baseURL: `${import.meta.env.VITE_API_URL || 'http://localhost:5178'}/api`,
    headers: {
        'Content-Type': 'application/json'
    },
    withCredentials: true
});

// Request interceptor - minden kéréshez hozzáadja a JWT tokent
apiClient.interceptors.request.use((config) => {
    const token = tokenStore.get();
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

// Response interceptor - 401 Unauth. esetén kijelentkeztetés
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;
        
        //A backend hibaválasza {"error": "..."} alakú JSON. A hívók e.response.data-t olvasnak szövegként,
        //ezért itt, egy helyen csomagoljuk ki.
        const data = error.response?.data;
        if (data && typeof data === 'object' && typeof data.error === 'string') {
            error.response.data = data.error;
        }
        
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;
            try {
                const newToken = await refreshTokenOnce();
                originalRequest.headers.Authorization = `Bearer ${newToken}`;
                return apiClient(originalRequest);
            } catch (refreshError) {
                tokenStore.clear();
                window.location.href = '/#/';
                return Promise.reject(refreshError);
            }
        }
        return Promise.reject(error);
    }
);

export default apiClient;