using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TokenProvider.Infrastructure.Models;
using TokenProvider.Infrastructure.Services;

namespace TokenProvider.Functions;

public class GenerateTokens(ILogger<GenerateTokens> logger, IRefreshTokenService refreshTokenService, ITokenGenerator tokenGenerator)
{
    private readonly ILogger<GenerateTokens> _logger = logger;
    private readonly IRefreshTokenService _refreshTokenService = refreshTokenService;
    private readonly ITokenGenerator _tokenGenerator = tokenGenerator;

    [Function("GenerateTokens")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "token/generate")] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var tokenRequest = JsonConvert.DeserializeObject<TokenRequest>(body);

        if (tokenRequest == null || tokenRequest.UserId == null || tokenRequest.Email == null)
        {
            return new BadRequestObjectResult(new { Error = "Please provide a valid UserId and Email" });
        }

        try
        {
            RefreshTokenResult refreshTokenResult = null!;
            AccessTokenResult accessTokenResult = null!;

            using var ctsTimeOut = new CancellationTokenSource(TimeSpan.FromSeconds(120 * 2));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeOut.Token, req.HttpContext.RequestAborted);

            req.HttpContext.Request.Cookies.TryGetValue("refreshToken", out var refreshToken);
            if (string.IsNullOrEmpty(refreshToken))
                refreshTokenResult = await _tokenGenerator.GenerateRefreshTokenAsync(tokenRequest.UserId, cts.Token);
            accessTokenResult = _tokenGenerator.GenerateAccessToken(tokenRequest, refreshTokenResult.Token!);

            if (refreshTokenResult.CookieOptions != null && refreshTokenResult.Token != null)
                req.HttpContext.Response.Cookies.Append("refreshToken", refreshTokenResult.Token, refreshTokenResult.CookieOptions);

            if (accessTokenResult != null && accessTokenResult.Token != null && refreshTokenResult.Token != null)
                return new OkObjectResult(new { AccessToken = accessTokenResult.Token, RefreshToken = refreshTokenResult.Token });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tokens");
            return new BadRequestObjectResult(new { Error = "Error generating tokens" });
        }
        return new BadRequestObjectResult(new { Error = "Error generating tokens" });
    }
}
