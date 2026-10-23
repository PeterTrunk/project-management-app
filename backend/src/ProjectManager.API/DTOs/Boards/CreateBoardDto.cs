namespace ProjectManager.API.DTOs.Boards
{
    public class CreateBoardDto
    {
        //A hatokort a ROUTE adja (/api/projects/{projectId}/...), ezert ez a mezo
        //szandekosan NINCS a DTO-ban: amit a kereles torzse nem tud megadni, azt nem is
        //lehet elteriteni. Korabban itt egy ProjectId allt, es a letrehozas AZT hasznalta a
        //route erteke helyett - a jogosultsagi kapu viszont a route-ra ellenorzott.
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
