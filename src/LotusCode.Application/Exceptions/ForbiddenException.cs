namespace LotusCode.Application.Exceptions
{
    public sealed class ForbiddenException : Exception
    {
        /// <summary>
        /// Represents exception thrown when a user tries to access a resource
        /// without sufficient permissions.
        /// Typically mapped to HTTP 403 status code.
        /// </summary>
        public ForbiddenException(string message)
            : base(message)
        {
        }
    }
}
