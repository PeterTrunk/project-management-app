import axios from 'axios';
import { tokenStore } from '../stores/tokenStore';
import { refreshTokenOnce } from './tokenRefresh';
import { friendlyMessageFor, normalizeResponseData } from './errorMessages';

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

        //A hálózati hibák emberi nyelvre cserélése elsőbbséget kap: ezeknél nincs is válasz.
        const friendly = friendlyMessageFor(error);
        if (friendly) {
            //A technikai részlet a konzolban marad, a felhasználó az olvasható üzenetet kapja
            console.error('API hiba:', error.code ?? error.response?.status, error.message);
            error.message = friendly;
            if (error.response) {
                error.response.data = friendly;
            }
        } else if (error.response) {
            //A válasz törzse MINDIG olvasható szöveggé alakul - lásd normalizeResponseData.
            const normalized = normalizeResponseData(error.response.data);
            if (normalized !== null) {
                error.response.data = normalized;
                error.message = normalized;
            } else {
                //Ismeretlen alak vagy nem JSON válasz: a nyers tartalom a konzolba megy.
                console.error('API hiba (ismeretlen válaszalak):',
                    error.response.status, error.response.data);
                error.response.data = 'Váratlan szerverhiba történt, próbáld újra!';
                error.message = error.response.data;
            }
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