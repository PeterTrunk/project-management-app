<script lang="ts">
  import Router from 'svelte-spa-router';
  import Login from './routes/Login.svelte';
  import Register from './routes/Register.svelte';
  import AppLayout from './routes/AppLayout.svelte';
  import InvitePage from './routes/InvitePage.svelte';
  import { themeStore } from './lib/stores/themeStore';
  
  const routes = {
    '/': Login,
    '/register': Register,
    '/app': AppLayout,
    '/invite/:token': InvitePage,
    '*': Login  // ismeretlen route: login
  };

  // HTML tag class frissítése témaváltáskor
  themeStore.subscribe(theme => {
    document.documentElement.classList.remove('dark', 'light');
    document.documentElement.classList.add(theme);
  });
</script>

<Router {routes} />