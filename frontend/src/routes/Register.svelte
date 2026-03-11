<script lang="ts">
    import { registerAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';


    let email = '';
    let password = '';
    let passwordconfirm = '';
    let displayName = '';
    
    let success = '';
    let error = '';

    function validateDisplayName(name: string): string | null{
        if (name.length < 3) return 'A felhasználónév nem lehet rövidebb 3 karakternél!\n';
        if (name.length > 120) return 'A felhasználónév nem lehet hosszabb mint 120 karakter!\n';
        return null;
    }

    function validateEmail(email: string): string | null{
        let aggregateError = '';
        if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) aggregateError += 'Hibás email szignatúra!\n';
        if(email.length > 255) aggregateError += 'Email nem lehet hosszabb mint 255 karakter!\n';
        return aggregateError === '' ? null : aggregateError;
    }

    function validatePassword(pwd: string): string | null {
        let aggregateError = '';
        if (pwd.length < 8) aggregateError += 'A jelszó minimum 8 karakter!\n';
        if (!/[A-Z]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon nagybetűt!\n';
        if (!/[0-9]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon számot!\n';
        if (!/[!@#$%^&*]/.test(pwd)) aggregateError += 'A jelszó tartalmazzon speciális karaktert (!@#$%^&*)!\n';
        return aggregateError === '' ? null : aggregateError;
    }

    async function handleRegister() {
        error = '';
        let errorOccured: boolean = false;
        const emailError = validateEmail(email);
        const displayNameError = validateDisplayName(displayName);
        const passwordError = validatePassword(password);
        if(emailError!=null){
            error = error + emailError;
            errorOccured = true;
        }
        if(displayNameError!=null){
            error = error + displayNameError;
            errorOccured = true;
        }
        if (passwordError!=null) {
            error = error + passwordError;
            errorOccured = true;
        }
        if (password !== passwordconfirm) {
            error = error + 'A két jelszó nem egyezik!\n';
            errorOccured = true;
        }
        if(errorOccured) {
            return;
        }
        try {
            const response = await registerAsync({ email, displayName, password });
            success = 'Sikeres regisztráció! Átirányítás...';
            setTimeout(() => push('/'), 2000);
        } catch (e) {
            error = 'Hiba történt a regisztráció során!';
        }
    }
</script>

<div>
  <h1>Regisztráció</h1>
  <form on:submit|preventDefault={handleRegister}>
    <input type="email" placeholder="Email" bind:value={email}/>
    <input type="text" placeholder="Felhasználónév" bind:value={displayName}/>
    <input type="password" placeholder="Jelszó" bind:value={password}/>
    <input type="password" placeholder="Jelszó megerősítése" bind:value={passwordconfirm}/>
    {#if error}
        <p id="failed">{error}</p>
    {/if}
    {#if success}
        <p id="success">{success}</p>
    {/if}
    <button type="submit">Regisztráció</button>
    <br>
  </form>

  <button on:click={() => push('/')}>Vissza a bejelentkezéshez</button>  
</div>

<style>
  #success{
    color: greenyellow;
  }
  #failed{
    color: red;
    white-space: pre-line;
  }
</style>