using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TokenProvider.Infrastructure.Models;
using TokenProvider.Infrastructure.Services;

namespace TokenProvider.Functions;

public class GenerateToken(ILogger<GenerateToken> logger, IRefreshTokenService refreshTokenService, ITokenGenerator tokenGenerator)
{
    private readonly ILogger<GenerateToken> _logger = logger;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly ITokenGenerator _tokenGenerator = tokenGenerator;

    [Function("GenerateToken")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "token/generate")] HttpRequest req, [FromBody] TokenRequest tokenRequest)
    {
        if (tokenRequest.UserId == null || tokenRequest.Email == null)
        {
            return new BadRequestObjectResult(new { Error = "Please provide a valid UserId and Email" });
        }

        try
        {
            RefreshTokenResult refreshTokenResult = null!;
            AccessTokenResult accessTokenResult = null!;

            using var ctsTimeOut = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeOut.Token, req.HttpContext.RequestAborted);

            req.HttpContext.Request.Cookies.TryGetValue("refreshToken", out var refreshToken);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                refreshTokenResult = await _refreshTokenService.GetRefreshTokenAsync(refreshToken, cts.Token);
            }
            if (refreshTokenResult == null || refreshTokenResult.ExpiryDate < DateTime.Now.AddDays(1))
            {
                refreshTokenResult = await _tokenGenerator.GenerateRefreshTokenAsync(tokenRequest.UserId, cts.Token);
            }

            accessTokenResult = _tokenGenerator.GenerateAccessToken(tokenRequest, refreshTokenResult.Token!);
            if (accessTokenResult != null && accessTokenResult.Token != null && refreshTokenResult.Token != null && refreshTokenResult.CookieOptions != null)
            {
                req.HttpContext.Response.Cookies.Append("refreshToken", refreshTokenResult.Token, refreshTokenResult.CookieOptions);
                return new OkObjectResult(new { AccessToken = accessTokenResult.Token });
               
            }
            return new ObjectResult(new { Error = "Error generating token" }) { StatusCode = 500 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating token");
            return new BadRequestObjectResult(new { Error = "Error generating token" });
        }

    }
}


//https://youtu.be/K5OQuNuGBeI?t=5819