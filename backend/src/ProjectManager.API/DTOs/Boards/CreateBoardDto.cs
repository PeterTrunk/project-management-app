namespace ProjectManager.API.DTOs.Boards
{
    public class CreateBoardDto
    {
        public Guid ProjectId { get; set; }
        /// <summary>
        /// Board neve, maximum 120 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Opcionális Board leírás, maximum 500 karakter
        /// </summary>
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } 
    }
}
