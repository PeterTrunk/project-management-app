namespace ProjectManager.API.DTOs.Columns
{
    public class CreateColumnDto
    {
        //A hatokort a ROUTE adja (/api/projects/{projectId}/...), ezert ez a mezo
        //szandekosan NINCS a DTO-ban: amit a kereles torzse nem tud megadni, azt nem is
        //lehet elteriteni. Korabban itt egy BoardId allt, es a letrehozas AZT hasznalta a
        //route erteke helyett - a jogosultsagi kapu viszont a route-ra ellenorzott.
        /// <summary>
        /// Oszlop neve, max 80 karakter
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Oszlopban található taskok státusza
        /// </summary>
        public string MapsToStatus {  get; set; } = string.Empty;
        /// <summary>
        /// Oszlop WIP limitje, null esetén nincsen limit
        /// </summary>
        public int? WipLimit { get; set; }
        /// <summary>
        /// Oszlop táblán belüli poziciója, Positon 1 és 99 között ajánlott
        /// </summary>
        public int Position { get; set; }
    }
}
