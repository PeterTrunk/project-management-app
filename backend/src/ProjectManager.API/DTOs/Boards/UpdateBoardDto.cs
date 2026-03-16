namespace ProjectManager.API.DTOs.Boards
{
    public class UpdateBoardDto
    {
        /// <summary>
        /// Board neve, maximum 120 karakter
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Opcionális Board leírás, maximum 500 karakter
        /// </summary>
        public string? Description { get; set; }
        public bool? IsDefault { get; set; }
    }
}
