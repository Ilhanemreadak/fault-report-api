namespace LotusCode.Application.Exceptions
{
    public sealed class StatusTransitionException : Exception
    {
        /// <summary>
        /// Represents exception thrown when an invalid status transition is attempted.
        /// Used when transition rules defined in policy are violated.
        /// Typically mapped to HTTP 422 status code.
        /// </summary>
        public StatusTransitionException(string message)
            : base(message)
        {
        }
    }
}
