namespace LotusCode.Application.Exceptions
{
    public sealed class BusinessRuleException : Exception
    {
        /// <summary>
        /// Represents exception thrown when a business rule is violated.
        /// Used for domain-specific validation such as duplicate constraints.
        /// Typically mapped to HTTP 422 status code.
        /// </summary>
        public BusinessRuleException(string message)
            : base(message)
        {
        }
    }
}
