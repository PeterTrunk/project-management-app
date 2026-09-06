namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>
    /// Ütközés a jelenlegi állapottal: duplikátum, vagy időközben módosult entitás
    /// (optimistic concurrency).
    /// </summary>
    public class ConflictException : AppException
    {
        public override int StatusCode => StatusCodes.Status409Conflict;

        public ConflictException(string message) : base(message) { }
    }
}
