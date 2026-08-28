namespace ProjectManager.API.Common.Constants
{
    public static class ProjectRoles
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string Member = "Member";
        public const string Viewer = "Viewer";
        
        //Listát itt deklaráljuk, ezzel esetleges új role bevezetésekor kevéské kell a validátorokhoz nyulni.
        public static readonly string[] ValidRoles = { Admin, Member, Viewer };
    }
}
