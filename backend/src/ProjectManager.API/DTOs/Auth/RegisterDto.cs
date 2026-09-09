namespace ProjectManager.API.DTOs.Auth
{
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// A felhasználási feltételek és az adatkezelési tájékoztató elfogadása.
        /// A kliens oldali tiltás önmagában nem elég: az API nyilvános, egy közvetlen kérés megkerülné a jelölőnégyzetet.
        /// </summary>
        public bool AcceptedTerms { get; set; }

        /// <summary>
        /// A kliens által MEGJELENÍTETT dokumentumverzió. A szerver csak akkor fogadja el, ha ez a hatályos verzió
        /// így egy régi, gyorsítótárazott felület nem tud olyan szövegre hivatkozó elfogadást rögzíteni, amit a felhasználó valójában nem látott.
        /// </summary>
        public string AcceptedTermsVersion { get; set; } = string.Empty;
    }
}
