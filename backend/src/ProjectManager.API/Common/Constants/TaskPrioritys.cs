namespace ProjectManager.API.Common.Constants
{
    public class TaskPrioritys
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
        public const string Critical = "critical";

        //Listát itt deklaráljuk, ezzel esetleges új prió bevezetésekor kevéské kell a validátorokhoz nyulni.
        public static readonly string[] ValidPriorities = { Low, Medium, High, Critical };
    }
}
