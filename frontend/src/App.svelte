<script lang="ts">
  import Router from 'svelte-spa-router';
  import Login from './routes/Login.svelte';
  import Register from './routes/Register.svelte';
  import AppLayout from './routes/AppLayout.svelte';
  import InvitePage from './routes/InvitePage.svelte';

  const routes = {
    '/': Login,
    '/register': Register,
    '/app': AppLayout,
    '/invite/:token': InvitePage,
    '*': Login  // ismeretlen route: login
  };
</script>

<Router {routes} />