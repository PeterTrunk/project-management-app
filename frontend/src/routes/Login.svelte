<script lang="ts">
    import { loginAsync } from '../lib/api/authApi';
    import { login } from '../lib/stores/authStore';
    import { push } from 'svelte-spa-router';

    let email = '';
    let password = '';
    let error = '';

    async function handleLogin() {
        try {
            const response = await loginAsync({ email, password });
            login(response.token, response.refreshToken, {
                userId: response.userId,
                email: response.email,
                displayName: response.displayName
            });
            push('/app');
        } catch (e) {
            error = "Hibás email vagy jelszó!";
        }
    }

    async function goToRegister() {
        push('/register');
    }

</script>

<div>
  <h1>Login</h1>
  <form on:submit|preventDefault={handleLogin}>
    <input type="email" placeholder="Email" bind:value={email}/>
    <input type="password" placeholder="Jelszó" bind:value={password}/>
    {#if error}
        <p>{error}</p>
    {/if}
    <button type="submit">Bejelentkezés</button>
  </form>
  <form on:submit={goToRegister}>
    <button type="submit">Regisztráció</button>
  </form>
</div>

<style>
  
</style>