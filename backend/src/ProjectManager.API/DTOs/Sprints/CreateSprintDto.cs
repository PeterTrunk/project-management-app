namespace ProjectManager.API.DTOs.Sprints
{
    public class CreateSprintDto
    {
        //A hatokort a ROUTE adja (/api/projects/{projectId}/...), ezert ez a mezo
        //szandekosan NINCS a DTO-ban: amit a kereles torzse nem tud megadni, azt nem is
        //lehet elteriteni. Korabban itt egy ProjectId allt, es a letrehozas AZT hasznalta a
        //route erteke helyett - a jogosultsagi kapu viszont a route-ra ellenorzott.
        /// <summary>
        /// Sprint neve, maximum 80 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Sprint cél leírása, maximum 500 karakter
        /// </summary>
        public string? Goal { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        /// <summary>
        /// Sprint státusza, State-ek: "Planned","Active","Completed"
        /// </summary>
        public string State { get; set; } = string.Empty;
    }
}
