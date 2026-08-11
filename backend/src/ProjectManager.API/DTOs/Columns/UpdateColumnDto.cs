namespace ProjectManager.API.DTOs.Columns
{
    public class UpdateColumnDto
    {
        /// <summary>
        /// Oszlop neve, max 80 karakter
        /// </summary>
        public string? Name { get; set; }
        /// <summary>
        /// Oszlopban található taskok státusza
        /// </summary>
        public string? MapsToStatus { get; set; }
        /// <summary>
        /// Oszlop WIP limitje, null esetén nincsen limit
        /// </summary>
        public int? WipLimit { get; set; }
        public byte[] RowVersion { get; set; } = null!;
    }
}
