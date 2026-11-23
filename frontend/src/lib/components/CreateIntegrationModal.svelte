<script lang="ts">
    import { onMount } from 'svelte';
    import { createIntegrationAsync } from '../api/integrationApi';

    import { GitBranch, Plus, TriangleAlert, X } from 'lucide-svelte';

    import { notify } from '../stores/notificationStore';

    export let isOpen = false;
    export let projectId: string;
    export let onClose: () => void = () => {};

    let modalRef: HTMLElement;
    onMount(() => modalRef?.focus());

    let provider = 'GitHub';
    let repoFullName = '';
    let webhookSecret = '';
    //Szándékosan alapból kipipálatlan: az előre bejelölt jelölőnégyzet nem érvényes nyilatkozat
    let authorityConfirmed = false;
    let error = '';
    let loading = false;

    //A pótláshoz tartozó, OPCIONÁLIS beállítások. A webhook fogadásához egyikre sincs szükség,
    //ezért külön, összecsukott szakaszban állnak: a gyakori eset ne legyen hosszabb tőlük.
    let showBackfillOptions = false;
    let instanceUrl = '';
    let accessToken = '';
    let autoBackfillEnabled = false;

    function closeModal() {
        isOpen = false;
        reset();
        onClose();
    }

    function reset() {
        provider = 'GitHub';
        repoFullName = '';
        webhookSecret = '';
        authorityConfirmed = false;
        error = '';
        loading = false;
        showBackfillOptions = false;
        instanceUrl = '';
        accessToken = '';
        autoBackfillEnabled = false;
    }

    async function handleCreate() {
        error = '';
        loading = true;
        try {
            const integration = await createIntegrationAsync(projectId, {
                provider,
                repoFullName,
                webhookSecret,
                authorityConfirmed,
                //Az üres érték itt NEM üres szöveg, hanem "nincs": a szerver a kettőt
                //egységesíti, de a kérésben se vigyünk ki értelmetlen üres titkot
                instanceUrl: instanceUrl.trim() === '' ? null : instanceUrl.trim(),
                accessToken: accessToken.trim() === '' ? null : accessToken.trim(),
                autoBackfillEnabled
            });
            notify.success('Integráció létrehozva!');
            closeModal();
        } catch (e: any) {
            const message = e.response?.data ?? e.message ?? 'Hiba történt az integráció létrehozásakor!';
            error = message;
            notify.error(message);
        } finally {
            loading = false;
        }
    }
</script>

<div class="modal-overlay"
    on:click|self={closeModal}
    bind:this={modalRef}
    on:keydown={(e) => e.key === 'Escape' && closeModal()}
    role="dialog"
    aria-modal="true"
    tabindex="-1"
>
    <div class="modal-content">
        <button class="close-btn" type="button" on:click={closeModal}>
            <X size={16} />
        </button>
        <h1><GitBranch size={18} /> Git Integráció Hozzáadása</h1>
        <form on:submit|preventDefault={handleCreate}>
            <div class="form-group">
                <label for="provider">Provider</label>
                <select id="provider" bind:value={provider}>
                    <option value="GitHub">GitHub</option>
                    <option value="GitLab">GitLab</option>
                </select>
            </div>

            <div class="form-group">
                <label for="repoFullName">Repository neve</label>
                <input
                    id="repoFullName"
                    type="text"
                    placeholder={provider === 'GitLab' ? 'csoport/projekt-utvonal' : 'owner/repo'}
                    bind:value={repoFullName}
                />
                {#if provider === 'GitLab'}
                    <span class="hint">
                        A projekt <strong>útvonala</strong>, ahogy a repó URL-jében szerepel
                        (Settings / General / Project URL) nem a megjelenített
                        neve. A kettő eltérhet: a név tartalmazhat szóközt és ékezetet, az
                        útvonal nem.
                    </span>
                {:else}
                    <span class="hint">Formátum: tulajdonos/repository-neve</span>
                {/if}
            </div>

            <div class="form-group">
                <label for="webhookSecret">Webhook Secret</label>
                <input
                    id="webhookSecret"
                    type="password"
                    placeholder="Minimum 16 karakter"
                    bind:value={webhookSecret}
                />
                <span class="hint warning">
                    <TriangleAlert size={18} />
                    Ezt a secretet add meg a {provider} webhook beállításánál is!
                    Tárold biztonságos helyen — később nem lesz megjeleníthető!
                </span>
            </div>

            <!-- A pótlás beállításai: a webhook fogadásához nem kellenek, csak a kimaradt
                 commitok utólagos behúzásához -->
            <button type="button" class="options-toggle" on:click={() => showBackfillOptions = !showBackfillOptions}>
                {showBackfillOptions ? '− ' : '+ '} Commit pótlás beállításai (nem kötelező)
            </button>

            {#if showBackfillOptions}
                <div class="form-group">
                    <label for="instanceUrl">Kiszolgáló címe (saját telepítés)</label>
                    <input
                        id="instanceUrl"
                        type="text"
                        placeholder={provider === 'GitLab' ? 'https://git.ceg.hu' : 'https://ghe.ceg.hu'}
                        bind:value={instanceUrl}
                    />
                    <span class="hint">
                        Csak saját telepítésű {provider} esetén kell. Üresen hagyva a
                        szolgáltató felhős változatát használjuk. A címnek <strong>https</strong>
                        protokollúnak kell lennie, mert a hozzáférési token ezen a címen megy ki.
                    </span>
                </div>

                <div class="form-group">
                    <label for="accessToken">Hozzáférési token</label>
                    <input
                        id="accessToken"
                        type="password"
                        placeholder="Nem kötelező"
                        bind:value={accessToken}
                    />
                    <span class="hint">
                        A szolgáltatók nem küldik el minden push minden commitját (a GitLab 20,
                        a GitHub 2048 commitnál vágja le a listát). A kimaradt commitok csak az
                        API-ból kérhetők le &mdash; privát repónál ehhez token kell, publikusnál
                        elhagyható. Titkosítva tároljuk, és soha nem jelenítjük meg újra.
                    </span>
                </div>

                <label class="authority-check">
                    <input type="checkbox" bind:checked={autoBackfillEnabled} />
                    <span>
                        Hiány észlelésekor a rendszer <strong>egyszer</strong> magától próbálja
                        meg a pótlást. Hiba esetén nem ismétli: a felületről újraindítható.
                    </span>
                </label>
            {/if}

            <label class="authority-check">
                <input type="checkbox" bind:checked={authorityConfirmed} />
                <span>
                    Kijelentem, hogy jogosult vagyok a repository csatlakoztatására, és tudomásul
                    veszem, hogy a rendszer a beérkező commitok szerzőjének nevét és e-mail címét
                    tárolja.
                </span>
            </label>

            {#if error}
                <p class="error">{error}</p>
            {/if}

            <div class="buttons">
                <!-- A gomb letiltása kényelmi jelzés: a nyilatkozatot a szerver kényszeríti ki,
                     mert az API közvetlenül is hívható. -->
                <button
                    type="submit"
                    class="create-btn"
                    disabled={loading || !repoFullName || !webhookSecret || !authorityConfirmed}>
                    {#if loading}
                        Létrehozás...
                    {:else}
                        <Plus size={15} /> Hozzáadás
                    {/if}
                </button>
            </div>
        </form>
    </div>
</div>

<style>
    .options-toggle {
        align-self: flex-start;
        padding: 0.3rem 0;
        background: transparent;
        border: none;
        color: var(--accent-blue);
        font-size: 0.82rem;
        cursor: pointer;
    }

    .modal-overlay {
        position: fixed;
        top: 0; left: 0;
        width: 100%; height: 100%;
        background: var(--shadow);
        display: flex;
        justify-content: center;
        align-items: center;
        z-index: 1000;
    }

    .modal-content {
        background: var(--bg-card);
        border: 1px solid var(--border);
        padding: 2rem;
        border-radius: 8px;
        width: 480px;
        max-width: 95vw;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
        position: relative;
    }

    @media (max-width: 480px) {
        .modal-content {
            padding: var(--card-padding);
        }
    }

    h1 {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        font-size: 1.3rem;
        margin: 0;
        color: var(--text-primary);
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    .form-group {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
    }

    label {
        font-size: 0.9rem;
        color: var(--text-secondary);
    }

    select, input {
        background: var(--bg-input);
        border: 1px solid var(--border-hover);
        border-radius: 6px;
        color: var(--text-primary);
        padding: 0.5rem;
        font-size: 0.95rem;
        width: 100%;
    }

    select:focus, input:focus {
        outline: none;
        border-color: var(--accent-blue);
    }

    .hint {
        font-size: 0.75rem;
        color: var(--text-muted);
    }

    .hint.warning {
        display: flex;
        align-items: flex-start;
        gap: 0.35rem;
        color: var(--accent-yellow);
    }

    .buttons {
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
        margin-top: 0.5rem;
        flex-wrap: wrap;
    }

    button {
        display: flex;
        align-items: center;
        gap: 0.35rem;
        padding: 0.5rem 1rem;
        border-radius: 6px;
        cursor: pointer;
        border: 1px solid var(--border-hover);
        background: var(--bg-hover);
        color: var(--text-secondary);
        font-size: 0.9rem;
        transition: background 0.15s, color 0.15s;
    }

    .close-btn {
        position: absolute;
        top: 0.75rem;
        right: 0.75rem;
        display: flex;
        align-items: center;
        background: transparent;
        border: none;
        color: var(--text-secondary);
        cursor: pointer;
        padding: 0.25rem;
        border-radius: 4px;
    }

    .close-btn:hover {
        color: var(--text-primary);
        background: var(--bg-hover);
    }

    button:hover { background: var(--border-hover); color: var(--text-primary); }

    .create-btn {
        background: var(--accent-green-bg);
        border-color: var(--accent-green);
        color: var(--accent-green);
    }

    .create-btn:hover { background: var(--accent-green); color: #fff; }
    .create-btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .error { color: var(--accent-red); font-size: 0.85rem; }

    .authority-check {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        font-size: var(--font-size-xs);
        color: var(--text-secondary);
        line-height: 1.45;
        cursor: pointer;
    }

    .authority-check input[type="checkbox"] {
        cursor: pointer;
        accent-color: var(--accent-blue);
        margin: 0;
        width: 14px;
        height: 14px;
        flex-shrink: 0;
        margin-top: 0.15rem;
    }

    .create-btn:disabled {
        opacity: 0.5;
        cursor: not-allowed;
    }
</style>