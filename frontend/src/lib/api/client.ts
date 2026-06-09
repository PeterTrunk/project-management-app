import axios from 'axios';

const apiClient = axios.create({
    baseURL: `${import.meta.env.VITE_API_URL || 'http://localhost:5178'}/api`,
    headers: {
        'Content-Type': 'application/json'
    }
});

// Request interceptor — minden kéréshez hozzáadja a JWT tokent
apiClient.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

// Response interceptor — 401 Unauth. esetén kijelentkeztetés
apiClient.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            localStorage.removeItem('token');
            localStorage.removeItem('refreshToken');
            window.location.href = '/#/';
        }
        return Promise.reject(error);
    }
);

export default apiClient;