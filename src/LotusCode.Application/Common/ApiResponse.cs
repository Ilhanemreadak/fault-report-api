namespace LotusCode.Application.Common
{
    public sealed class ApiResponse<T>
    {
        public bool Success { get; init; }

        public T? Data { get; init; }

        public string Message { get; init; } = string.Empty;

        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        public static ApiResponse<T> SuccessResponse(T data, string message = "")
            => new()
            {
                Success = true,
                Data = data,
                Message = message
            };

        public static ApiResponse<T> FailureResponse(
            string message,
            IReadOnlyList<string>? errors = null)
            => new()
            {
                Success = false,
                Message = message,
                Errors = errors ?? Array.Empty<string>()
            };
    }
}
