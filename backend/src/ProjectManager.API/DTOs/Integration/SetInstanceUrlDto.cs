namespace ProjectManager.API.DTOs.Integration
{
    public class SetInstanceUrlDto
    {
        /// <summary>
        /// A saját telepítésű szolgáltató kiszolgálójának címe (pl. https://git.ceg.hu).
        /// Üresen hagyva a szolgáltató felhős változatára áll vissza.
        /// </summary>
        public string? InstanceUrl { get; set; }
    }
}
