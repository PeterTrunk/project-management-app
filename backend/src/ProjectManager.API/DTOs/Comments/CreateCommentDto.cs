using System.ComponentModel.DataAnnotations;

namespace ProjectManager.API.DTOs.Comments
{
    public class CreateCommentDto
    {
        [Required]
        public string Body { get; set; } = string.Empty;
    }
}
