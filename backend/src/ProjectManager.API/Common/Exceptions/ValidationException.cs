namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>Üzleti szabály sérült - a kérés formailag rendben volt, tartalmilag nem.</summary>
    public class ValidationException : AppException
    {
        public override int StatusCode => StatusCodes.Status400BadRequest;

        public ValidationException(string message) : base(message) { }
    }
}
