namespace ProjectManager.API.DTOs.Project
{
    public class UpdateProjectDto
    {
        /// <summary>
        /// Projekt neve, maximum 120 karakter
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Opcionális Projekt leírás, max 1000 karakter
        /// </summary>
        public string? Description { get; set; }
    }
}
