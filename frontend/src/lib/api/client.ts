import axios from 'axios';
import { tokenStore } from '../stores/tokenStore';

const apiClient = axios.create({
    baseURL: `${import.meta.env.VITE_API_URL || 'http://localhost:5178'}/api`,
    headers: {
        'Content-Type': 'application/json'
    }
    //withCredentials: true
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
        
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;
            try {
                console.log('Token lejárt, refresh indítása...');
                const response = await axios.post(
                    `${import.meta.env.VITE_API_URL || 'http://localhost:5178'}/api/auth/refresh`,
                    {},
                    { withCredentials: true }
                );

                const newToken = response.data.token;
                tokenStore.set(newToken);
                
                originalRequest.headers.Authorization = `Bearer ${newToken}`;
                return apiClient(originalRequest);
            } catch (refreshError) {
                console.log('Refresh sikertelen, kijelentkeztetés');
                tokenStore.clear();
                window.location.href = '/#/';
                return Promise.reject(refreshError);
            }
        }
        return Promise.reject(error);
    }
);

export default apiClient;