import * as signalR from '@microsoft/signalr';
import { tokenStore } from '../stores/tokenStore';
import { notify } from '../stores/notificationStore';

const HUB_URL = import.meta.env.VITE_API_URL 
    ? `${import.meta.env.VITE_API_URL}/hubs/project`
    : 'http://localhost:5178/hubs/project';

const KEEPALIVE_ENABLED = import.meta.env.VITE_SIGNALR_KEEPALIVE_ENABLED === 'true';

// A szerver ClientTimeoutInterval-ja 60 másodperc: ha a kliens ennél ritkábban
// pingel, a szerver bontja a kapcsolatot. Ezért a beállított értéket 5 és 30 mp
// közé szorítjuk - így egy elgépelt környezeti változó nem tud folyamatos
// újracsatlakozási hurkot okozni.
const MIN_KEEPALIVE_SECONDS = 5;
const MAX_KEEPALIVE_SECONDS = 30;

const configuredKeepalive = parseInt(import.meta.env.VITE_SIGNALR_KEEPALIVE_SECONDS ?? '15');
const keepaliveSeconds = Number.isFinite(configuredKeepalive)
    ? Math.min(Math.max(configuredKeepalive, MIN_KEEPALIVE_SECONDS), MAX_KEEPALIVE_SECONDS)
    : 15;

if (KEEPALIVE_ENABLED && configuredKeepalive !== keepaliveSeconds) {
    console.warn(
        `SignalR keepalive ${configuredKeepalive}s helyett ${keepaliveSeconds}s ` +
        `(a szerver 60 másodperc után bontja a néma kapcsolatot).`
    );
}

const KEEPALIVE_MS = keepaliveSeconds * 1000;

class SignalRService {
    private connection: signalR.HubConnection | null = null;

    //Az újracsatlakozás új connectionId-t ad, amivel a szerveroldali csoport-tagság elveszne.
    //A hívó (AppLayout) ezeken a hookokon keresztül tud visszalépni a projekt csoportjába és újraszinkronizálni az állapotot.
    private reconnectedCallback: (() => void) | null = null;
    private closedCallback: (() => void) | null = null;

    //A szándékos bontásra (kijelentkezés, kézi újracsatlakozás) nem kell kapcsolatvesztés-jelzést adni
    private intentionalDisconnect = false;

    async connect(token: string) {
        this.intentionalDisconnect = false;

        const builder = new signalR.HubConnectionBuilder()
            .withUrl(HUB_URL, {
                //skipNegotiation: true,
                //transport: signalR.HttpTransportType.WebSockets,
                accessTokenFactory: () => tokenStore.get() ?? token
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000]);

        if (KEEPALIVE_ENABLED) {
            builder.withKeepAliveInterval(KEEPALIVE_MS);
            //console.log(`SignalR keepalive: ${KEEPALIVE_MS / 1000}s`);
        }

        this.connection = builder.build();

        this.connection.onreconnecting(() => notify.warning('Kapcsolat megszakadt, újracsatlakozás...'));

        this.connection.onreconnected(() => {
            //A sikerjelzés a resync után jön, különben azt sugallná, hogy már minden friss
            if (this.reconnectedCallback) {
                this.reconnectedCallback();
            } else {
                notify.success('Kapcsolat helyreállt!');
            }
        });

        this.connection.onclose(() => {
            if (this.intentionalDisconnect) return;

            if (this.closedCallback) {
                this.closedCallback();
            } else {
                notify.error('A kapcsolat megszakadt!');
            }
        });

        try {
            await this.connection.start();
            console.log('SignalR connected!');
        } catch (e: any) {
            //A könyvtár technikai szövege ("Failed to complete negotiation with the server:
            //TypeError: Failed to fetch") a konzolban marad, a felhasználó olvasható üzenetet kap
            console.error('SignalR kapcsolódási hiba:', e);
            notify.error('Nem sikerült csatlakozni a szerverhez! Ellenőrizd az internetkapcsolatod, vagy próbáld újra pár másodperc múlva.');
        }
    }

    onReconnected(callback: () => void) {
        this.reconnectedCallback = callback;
    }

    onClosed(callback: () => void) {
        this.closedCallback = callback;
    }

    isConnected() {
        return this.connection?.state === signalR.HubConnectionState.Connected;
    }

    async joinProject(projectId: string) {
        await this.connection?.invoke('JoinProject', projectId);
    }
    
    async leaveProject(projectId: string) {
        await this.connection?.invoke('LeaveProject', projectId);
    }
    
    on(event: string, callback: (...args: any[]) => void) {
        this.connection?.on(event, callback);
    }

    off(event: string) {
        this.connection?.off(event);
    }

    async disconnect() {
        this.intentionalDisconnect = true;
        await this.connection?.stop();
        this.connection = null;
    }
}

export const signalRService = new SignalRService();
