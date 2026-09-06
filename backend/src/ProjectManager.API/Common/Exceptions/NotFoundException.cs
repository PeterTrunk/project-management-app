namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>A hivatkozott entitás nem létezik, vagy nem tartozik a hívó projektjéhez.</summary>
    public class NotFoundException : AppException
    {
        public override int StatusCode => StatusCodes.Status404NotFound;

        public NotFoundException(string message) : base(message) { }
    }
}
