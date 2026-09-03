import * as signalR from '@microsoft/signalr';
import { tokenStore } from '../stores/tokenStore';
import { notify } from '../stores/notificationStore';

const HUB_URL = import.meta.env.VITE_API_URL 
    ? `${import.meta.env.VITE_API_URL}/hubs/project`
    : 'http://localhost:5178/hubs/project';

const KEEPALIVE_ENABLED = import.meta.env.VITE_SIGNALR_KEEPALIVE_ENABLED === 'true';
const KEEPALIVE_MS = parseInt(import.meta.env.VITE_SIGNALR_KEEPALIVE_SECONDS ?? '15') * 1000;

class SignalRService {
    private connection: signalR.HubConnection | null = null;

    async connect(token: string) {
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
        this.connection.onreconnected(() => notify.success('Kapcsolat helyreállt!'));
        this.connection.onclose(() => notify.error('A kapcsolat megszakadt!'));

        try {
            await this.connection.start();
            console.log('SignalR connected!');
        } catch (e: any) {
            notify.error(e.message ?? 'Nem sikerült csatlakozni a szerverhez!');
        }
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
        await this.connection?.stop();
        this.connection = null;
    }
}

export const signalRService = new SignalRService();