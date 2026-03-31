namespace LotusCode.Application.Exceptions
{
    /// <summary>
    /// Represents exception thrown when authentication fails
    /// or user is not authorized.
    /// Typically mapped to HTTP 401 status code.
    /// </summary>
    public sealed class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message)
            : base(message)
        {
        }
    }
}
