<script lang="ts">
  import Router from 'svelte-spa-router';
  import Login from './routes/Login.svelte';
  import Register from './routes/Register.svelte';
  import Dashboard from './routes/Dashboard.svelte';

  const routes = {
    '/': Login,
    '/register': Register,
    '/dashboard': Dashboard,
    '*': Login  // ismeretlen route: login
  };
</script>

<Router {routes} />