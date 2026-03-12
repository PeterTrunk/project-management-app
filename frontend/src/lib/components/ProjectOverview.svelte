<script lang="ts">
    import type { ProjectResponse } from '../api/projectApi';

    export let project: ProjectResponse;
</script>

<div>
    <h1>{project.name}</h1>
    <p>Leírás: {project.description ?? 'Nincs leírás'}</p>
    <p>Projekt tulajdonosa: <span>{project.ownerName}</span></p>
    <p>Projekthez tartozó kulcs: <span>{project.projKey}</span></p>
    <p>Létrehozás ideje: <span>{new Date(project.createdAt).toLocaleDateString('hu-HU')}</span></p>
    <p>Alap adatok legutobbi változásának ideje: <span>{new Date(project.updatedAt).toLocaleDateString('hu-HU')}</span></p>
    {#if project.isArchived}
        <p>A Projekt Arhivált állapotú</p>
    {:else}
        <p>A Projekt <span>Aktív</span> állapotú.</p>
    {/if}
</div>

<style>
    span{
        font-weight: bold;
    }
    h1 {
        margin-bottom: 1rem;
    }
</style>