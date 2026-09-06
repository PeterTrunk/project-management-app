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

        //Növekvő sorrend: a jogosultság-ellenőrzés ebben a listában vett indexeket hasonlítja össze.
        //Új szerepkör bevezetésekor CSAK itt kell beszúrni.
        public static readonly string[] Hierarchy = { Viewer, Member, Admin, Owner };
        
        //A szerepkör helye a hierarchiában, vagy -1 ha ismeretlen.
        public static int RankOf(string? role) =>
            role == null ? -1 : Array.IndexOf(Hierarchy, role);
    }
}
