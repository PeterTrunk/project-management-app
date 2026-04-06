import * as signalR from '@microsoft/signalr';

class SignalRService {
    private connection: signalR.HubConnection | null = null;

    async connect(token: string) {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:5178/hubs/project', {
                accessTokenFactory: () => token
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        this.connection.onreconnecting(() => console.log('SignalR reconnecting...'));
        this.connection.onreconnected(() => console.log('SignalR reconnected!'));
        this.connection.onclose(() => console.log('SignalR connection closed!'));

        try {
            await this.connection.start();
            console.log('SignalR connected!');
        } catch (e) {
            console.error('SignalR connection error:', e);
        }
    }

    async joinProject(projectId: string) {
        await this.connection?.invoke('JoinProject', projectId);
    }

    async leaveProject(projectId: string) {
        await this.connection?.invoke('LeaveProject', projectId);
    }

    async joinBoard(boardId: string) {
        await this.connection?.invoke('JoinBoard', boardId);
    }

    async leaveBoard(boardId: string) {
        await this.connection?.invoke('LeaveBoard', boardId);
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