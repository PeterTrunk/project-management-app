namespace ProjectManager.API.Common.Exceptions
{
    /// <summary>
    /// Minden olyan kivétel őse, amelynek az üzenete szándékosan a felhasználónak szól,
    /// és biztonságosan kimehet a válaszban. Ami NEM ebből származik (EF Core, Npgsql,
    /// MinIO SDK, NullReference stb.), annak az üzenete sosem hagyja el a szervert - a részletek csak a naplóba kerülnek.
    /// </summary>
    public abstract class AppException : Exception
    {
        public abstract int StatusCode { get; }

        protected AppException(string message) : base(message) { }
    }
}
