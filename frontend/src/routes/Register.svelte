<script lang="ts">
    import { registerAsync } from '../lib/api/authApi';
    import { push } from 'svelte-spa-router';


    let email = '';
    let password = '';
    let passwordconfirm = '';
    let displayName = '';

    let success = '';
    let error = '';

    async function handleRegister() {
        try {

            if(password == passwordconfirm)
            {
                const response = await registerAsync({ email, displayName, password });
                success = 'Sikeres regisztráció! Átirányítás...';
                setTimeout(() => push('/'), 2000);
            }
            else{
                error = "A két jelszó nem egyezik!";
            }
            
        } catch (e) {
            error = "Hibás email vagy jelszó!";
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
  }
</style>