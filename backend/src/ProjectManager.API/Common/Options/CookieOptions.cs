namespace ProjectManager.API.Common.Options
{

    //A refresh token süti beállításai. A Domain korábban hardcode-olva volt a controllerben, 
    //ami minden környezetben ugyanazt a domaint erőltette.
    public class CookieOptions
    {
        //Süti domain (pl. ".trunkpeter.com"). Üresen hagyva a süti host-only lesz - fejlesztői környezetben ez a helyes.
        public string? Domain { get; set; }

        //A süti útvonala. Csak a refresh végpontokra megy ki.
        public string Path { get; set; } = "/api/auth";
    }
}
