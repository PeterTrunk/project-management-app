namespace ProjectManager.API.Common.Constants
{
    /// <summary>
    /// A támogatott git szolgáltatók azonosítói. Ez az érték kerül az Integration.Provider oszlopba,
    /// tehát már az DB-ben tárolt szöveg: átírásához migráció kellene, nem elég a konstans módosítása.
    /// </summary>
    public static class GitProviders
    {
        public const string GitHub = "GitHub";
        public const string GitLab = "GitLab";

        public static readonly string[] All = [GitHub, GitLab];
    }
}
