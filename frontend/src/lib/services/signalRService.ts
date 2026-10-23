import * as signalR from '@microsoft/signalr';
import { tokenStore } from '../stores/tokenStore';
import { notify } from '../stores/notificationStore';

const HUB_URL = import.meta.env.VITE_API_URL 
    ? `${import.meta.env.VITE_API_URL}/hubs/project`
    : 'http://localhost:5178/hubs/project';

const KEEPALIVE_ENABLED = import.meta.env.VITE_SIGNALR_KEEPALIVE_ENABLED === 'true';

// A szerver ClientTimeoutInterval-ja 60 másodperc: ha a kliens ennél ritkábban
// pingel, a szerver bontja a kapcsolatot. 
// Ezért a beállított értéket 5 és 30 mp közé szorítjuk
// - így egy elgépelt környezeti változó nem tud folyamatos újracsatlakozási loopot okozni.
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
                //Csak WebSocket, negotiate nélkül. 
                //(Elvetjük a negotiate-et, ez eddig meg volt tarva)
                //
                //ELŐZMÉNY, hogy a döntés érthető legyen: a Microsoft dokumentációja szerint
                //(learn.microsoft.com/aspnet/core/signalr/scale) a Redis backplane mellett a
                //sticky session KÖTELEZŐ, és pontosan EGY kivétel van - ha minden kliens
                //kizárólag WebSocketet használ ÉS a skipNegotiation be van kapcsolva. (Mint itt)
                //
                //Vagyis a korábbi beállítás (negotiate + Traefik affinity-cookie + backplane)
                //NEM volt hibás: szó szerint az ajánlott felállás volt. 
                //Egyetlen paramétere volt rossz, a cookie 30 másodperces maxage-e - 
                //lásd a 2. pontot. Itt tehát két dokumentált út KÖZÖTT választunk, nem egy hibát javítunk.
                //
                //Két ok a kivétel melletti döntés mellett:
                //
                //1. A negotiate elhagyásával egyetlen kérés épül fel, nem kettő. 
                //   A negotiate válaszában kapott connectionToken annak a példánynak a memóriájában él,
                //   amelyik kiadta - a rá következő WebSocket-upgrade-nek tehát UGYANODA kell érkeznie.
                //   Ezt eddig a Traefik affinity-cookie-ja biztosította; így erre nincs is szükség.
                //
                //2. A megkötés nélkül a SignalR WebSocket-hiba esetén Server-Sent Eventsre,
                //   majd long pollingra esik vissza. Azok viszont HTTP kérések SOROZATÁBÓL
                //   állnak a kapcsolat teljes élettartamán, és mindegyiknek ugyanahhoz a
                //   példányhoz kell érkeznie - az affinity-cookie maxage-e viszont 30
                //   másodperc, és a Traefik NEM újítja meg (ellenőrizve a v3.1 forrásában:
                //   valid cookie esetén korai visszatéréssel kiszolgál, Set-Cookie nélkül).
                //   A visszaesés tehát 30 másodpercenként újracsatlakozási hurokba futott
                //   volna - ami jóval nehezebben diagnosztizálható, mint egy azonnali hiba.
                //
                //Ami NEM indok: a replikák közti terheléselosztás. Két replika ugyanazon a
                //két vCPU-n osztozik, tehát nem ad CPU kapacitást - a hasznuk a leállás nélküli
                //újraindítás és a rendelkezésre állás, amit a ragasztás nem érint (a Traefik
                //csak ÉP backendre fogadja el a cookie-t). Lásd KERDESEK.md, 10. kérdés.
                //
                //Az ára viszont valódi: aki nem tud WebSocketet használni, az egyáltalán nem
                //kap valós idejű frissítést. 
                //A REST felület ettől függetlenül működik, és a hibaüzenet erre külön kitér.
                //
                //Ha ez egyszer zavaróvá válik, a visszaesés visszavehető tartalékként: 
                //elsőre skipNegotiation, és a start() hibájára újraépítés negotiate-tel. 
                //Akkor a cookie-nak session cookie-nak kell lennie (maxage nélkül).
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,

                //skipNegotiation mellett is működik: a token a WebSocket URL query
                //stringjébe kerül, és a szerver a /hubs útvonalon onnan olvassa.
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

            //A WebSocket-megkötés miatt nincs visszaesés más szállításra, ezért a hálózati
            //ok mellett a blokkolt WebSocket is valós lehetőség - erre a felhasználónak
            //esélye sincs rájönni, ha nem mondjuk meg.
            notify.error(
                'Nem sikerült élő kapcsolatot létesíteni a szerverrel! '
                + 'Ellenőrizd az internetkapcsolatod - ha céges hálózaton vagy, lehet, hogy a '
                + 'WebSocket kapcsolatok blokkolva vannak. Az alkalmazás ettől működik, '
                + 'csak a változások nem jelennek meg automatikusan.'
            );
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

    /**
     * Leiratkozás egy eseményről.
     *
     * A `callback` megadása **erősen javasolt**: nélküle a SignalR az esemény ÖSSZES
     * kezelőjét leveszi, nem csak a hívóét. Mivel 37 esemény központilag is regisztrálva
     * van (signalRClientService), egy komponens így a központi kezelőt is kilőné - és
     * kilépéskor nem állítaná vissza, tehát a munkamenet hátralévő részében az az esemény
     * kezelő nélkül maradna.
     *
     * A paraméter nélküli alak csak a központi `unregisterSignalREvents` számára való,
     * ami kijelentkezéskor szándékosan mindent letakarít.
     */
    off(event: string, callback?: (...args: any[]) => void) {
        if (callback) {
            this.connection?.off(event, callback);
        } else {
            this.connection?.off(event);
        }
    }

    async disconnect() {
        this.intentionalDisconnect = true;
        await this.connection?.stop();
        this.connection = null;
    }
}

export const signalRService = new SignalRService();
