namespace LotusCode.Application.Exceptions
{
    /// <summary>
    /// Represents exception thrown when a requested resource is not found.
    /// Typically mapped to HTTP 404 status code.
    /// </summary>
    public sealed class NotFoundException : Exception
    {
        public NotFoundException(string message)
            : base(message)
        {
        }
    }
}
