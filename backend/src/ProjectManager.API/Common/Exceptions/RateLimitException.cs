namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>Túllépett kérésszám. Az üzenet tartalmazza az újrapróbálkozásig hátralévő időt.</summary>
    public class RateLimitException : AppException
    {
        public override int StatusCode => StatusCodes.Status429TooManyRequests;

        public RateLimitException(string message) : base(message) { }
    }
}
