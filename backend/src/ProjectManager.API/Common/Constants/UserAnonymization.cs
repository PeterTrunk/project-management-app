namespace ProjectManager.API.Common.Constants
{
    public static class UserAnonymization
    {
        /// <summary>
        /// A törölt felhasználó megjelenített neve.
        /// </summary>
        public const string DisplayName = "<Törölt felhasználó>";

        /// <summary>
        /// Helyettesítő e-mail cím. Az .invalid fenntartott felső szintű tartomány (RFC 2606), tehát a cím sosem kézbesíthető.
        /// </summary>
        public static string EmailFor(Guid userId) => $"deleted-{userId}@invalid.local";
    }
}
