namespace ProjectManager.API.DTOs.Project
{
    public class CreateProjectDto
    {
        /// <summary>
        /// Projekt neve, maximum 120 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Projekt kulcs, nagybetűk és számok kombinációja, a kulcs alapján kapnak egyedi azonosítót a projekten létrehozott Taskok.
        /// Létrehozás után NEM változtatható, végleges érték
        /// </summary>
        /// <example>
        /// Kulcs: "PMA", Task által kapott anonosító: "PMA-1", "PMA-2", "PMA-3"
        /// </example>
        public string ProjKey { get; set; } = string.Empty;
        /// <summary>
        /// Opcionális Projekt leírás, max 1000 karakter
        /// </summary>
        public string? Description { get; set; }
    }
}
