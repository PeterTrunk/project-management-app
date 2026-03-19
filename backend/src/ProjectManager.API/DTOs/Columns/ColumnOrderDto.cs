namespace ProjectManager.API.DTOs.Columns
{
    public class ColumnOrderDto
    {
        public Guid Id { get; set; }
        /// <summary>
        /// Oszlop táblán belüli poziciója, Positon 1 és 99 között ajánlott
        /// </summary>
        public int Position { get; set; }
    }
}
