namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>A hívó azonosított, de a művelethez nincs jogosultsága.</summary>
    public class ForbiddenException : AppException
    {
        public override int StatusCode => StatusCodes.Status403Forbidden;

        public ForbiddenException(string message) : base(message) { }
    }
}
