using LotusCode.Application.Common;
using LotusCode.Application.DTOs.Auth;
using LotusCode.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LotusCode.Api.Controllers
{
    /// <summary>
    /// Exposes authentication endpoints for user login and token generation.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService authService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="authService">The authentication service.</param>
        public AuthController(IAuthService authService)
        {
            this.authService = authService;
        }

        /// <summary>
        /// Authenticates the user and returns a JWT token.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/auth/login
        ///     {
        ///       "email": "admin@lotus.local",
        ///       "password": "Admin123!"
        ///     }
        /// </remarks>
        /// <param name="request">The login request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The authenticated user information and access token.</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var response = await this.authService.LoginAsync(request, cancellationToken);

            return this.Ok(ApiResponse<LoginResponse>.SuccessResponse(response, "Login successful."));
        }
    }
}
