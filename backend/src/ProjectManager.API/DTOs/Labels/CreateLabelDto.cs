using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Labels
{
    public class CreateLabelDto
    {
        /// <summary>
        /// Címke neve, max 40 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Címke színe, HEX-ben
        /// </summary>
        public string Color { get; set; } = string.Empty;
    }
}
