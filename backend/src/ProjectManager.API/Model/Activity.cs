namespace ProjectManager.API.Model
{
    public class Activity
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? ActorId { get; set; }

        /// <summary>
        /// Az a felhasználó, akivel a művelet TÖRTÉNT - például akit eltávolítottak a projektből,
        /// Vagy akit hozzárendeltek egy taskhoz. Null, ha a művelet senkire sem irányult.
        /// </summary>
        public Guid? TargetUserId { get; set; }

        public string EntityType { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// A leírás SABLONJA, nem kész szöveg. A személyneveket a <c>{actor}</c> és <c>{target}</c> jelölők helyettesítik,
        /// amelyeket az ActivityService olvasáskor cserél ki a hivatkozott felhasználók AKTUÁLIS nevére.
        ///
        /// Miért: a DisplayName bármikor átírható. 
        /// Ha a nevet ide égetnénk, az átnevezés után a régi név maradna a szövegben, 
        /// a fióktörlés pedig nem tudná mindet eltávolítani csak azt az egyet, amit a törlés pillanatában ismer.
        ///
        /// A board és tasknevek szándékosan beégetve maradnak: azok nem személyes adatok, 
        /// és egy naplóban helyes rögzíteni, minek hívták a dolgot az esemény idején.
        ///
        /// A jelölők bevezetése előtt keletkezett sorok kész szöveget tartalmaznak. 
        /// Azokban nincs mit cserélni, ezért változatlanul jelennek meg.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public string? Payload { get; set; }
        public DateTime CreatedAt { get; set; }

        public Project Project { get; set; } = null!;
        public User? Actor { get; set; }
        public User? TargetUser { get; set; }
    }
}
