namespace ProjectManager.API.Common.Constants
{
    public class SprintStates
    {
        public const string Planning = "Planning";
        public const string Active = "Active";
        public const string Completed = "Completed";

        //Listát itt deklaráljuk, ezzel esetleges új state bevezetésekor kevéské kell a validátorokhoz nyulni.
        public static readonly string[] ValidStates = { Planning, Active, Completed };
    }
}
