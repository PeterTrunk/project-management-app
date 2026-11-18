namespace ProjectManager.API.DTOs.Integration
{
    public class SetAccessTokenDto
    {
        /// <summary>
        /// Az új hozzáférési token. Üresen hagyva a meglévő token <b>törlődik</b> - ez a
        /// visszavonás útja, és azért ugyanez a végpont, mert a kettő ugyanaz a döntés:
        /// a felhasználó megmondja, mi legyen a tárolt hitelesítő adat.
        /// </summary>
        public string? AccessToken { get; set; }
    }
}
