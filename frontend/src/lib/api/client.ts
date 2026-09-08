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

//A hívók a `e.response?.data ?? e.message` mintát használják. Ha a kérés el sem jutott a szerverig,
//az axios nyers technikai szövege ("Network Error", "timeout of 0ms exceeded") kerülne a felhasználó elé.
//Ezért itt cseréljük felhasználóbarátabb üzenetre.
function friendlyMessageFor(error: any): string | null {
    //A megszakított kérés nem hiba, nem is kell róla üzenet
    if (error.code === 'ERR_CANCELED') {
        return null;
    }

    if (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT') {
        return 'A szerver nem válaszolt időben, próbáld újra!';
    }

    if (!error.response) {
        return 'Nem sikerült elérni a szervert! Ellenőrizd az internetkapcsolatod, vagy próbáld újra pár másodperc múlva.';
    }

    const status = error.response.status;
    if (status === 502 || status === 503 || status === 504) {
        return 'A szerver éppen nem elérhető, próbáld újra pár másodperc múlva!';
    }

    return null;
}

// Response interceptor - 401 Unauth. esetén kijelentkeztetés
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;

        //A backend hibaválasza {"error": "..."} alakú JSON. A hívók e.response.data-t olvasnak szövegként,
        //Itt egy helyen csomagoljuk ki.
        const data = error.response?.data;
        if (data && typeof data === 'object' && typeof data.error === 'string') {
            error.response.data = data.error;
        }

        const friendly = friendlyMessageFor(error);
        if (friendly) {
            //A technikai részlet a konzolban marad, a felhasználó az olvasható üzenetet kapja
            console.error('API hiba:', error.code ?? error.response?.status, error.message);
            error.message = friendly;
            if (error.response) {
                error.response.data = friendly;
            }
        } else if (typeof error.response?.data === 'string' && error.response.data.trimStart().startsWith('<')) {
            //Nem a mi API-nktól jött (pl. reverse proxy HTML hibaoldala)
            console.error('API hiba (nem JSON válasz):', error.response.status, error.response.data);
            error.response.data = 'Váratlan szerverhiba történt, próbáld újra!';
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