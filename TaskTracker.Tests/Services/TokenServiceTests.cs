using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskTracker.Models;
using TaskTracker.MyOptions;
using TaskTracker.Services;
using TaskTracker.Tests.TestKit;

namespace TaskTracker.Tests.Services;

/// <summary>
/// Token generation is security code, so these tests do not stop at "a string came
/// back". They decode the token and check the claims, and they verify the signature
/// with the right key and reject it with a wrong one.
/// </summary>
public class TokenServiceTests
{
    private const string SecretKey = "this-is-a-test-signing-key-at-least-32-bytes-long";
    private const string Issuer = "TaskTracker";
    private const string Audience = "TaskTrackerClients";

    private readonly TokenService _sut = new(Options.Create(new JwtSettings
    {
        SecretKey = SecretKey,
        Issuer = Issuer,
        Audience = Audience,
        ExpiresInMinutes = 60
    }));

    [Fact]
    public void GenerateToken_ProducesAReadableJwt()
    {
        var token = _sut.GenerateToken(Make.User());

        Assert.NotNull(new JwtSecurityTokenHandler().ReadJwtToken(token));
    }

    [Fact]
    public void GenerateToken_PutsTheUserIdInTheSubjectClaim()
    {
        var user = Make.User();

        var claim = Read(_sut.GenerateToken(user)).Claims.Single(c => c.Type == ClaimTypes.NameIdentifier);

        // ProjectService and WorkTaskService read exactly this claim to decide access.
        Assert.Equal(user.Id.ToString(), claim.Value);
    }

    [Fact]
    public void GenerateToken_PutsTheRoleInTheRoleClaim()
    {
        var claim = Read(_sut.GenerateToken(Make.User(role: Roles.Admin)))
            .Claims.Single(c => c.Type == ClaimTypes.Role);

        Assert.Equal(Roles.Admin, claim.Value);
    }

    [Fact]
    public void GenerateToken_IncludesEmailAndPosition()
    {
        var token = Read(_sut.GenerateToken(Make.User(email: "ada@example.com", position: "Engineer")));

        Assert.Equal("ada@example.com", token.Claims.Single(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Engineer", token.Claims.Single(c => c.Type == CustomClaims.Position).Value);
    }

    [Fact]
    public void GenerateToken_NeverIncludesThePasswordHash()
    {
        var token = Read(_sut.GenerateToken(Make.User(passwordHash: "$2a$11$secretsecretsecret")));

        Assert.DoesNotContain(token.Claims, c => c.Value.Contains("$2a$11$"));
    }

    [Fact]
    public void GenerateToken_StampsTheConfiguredIssuerAndAudience()
    {
        var token = Read(_sut.GenerateToken(Make.User()));

        Assert.Equal(Issuer, token.Issuer);
        Assert.Contains(Audience, token.Audiences);
    }

    [Fact]
    public void GenerateToken_ExpiresAfterTheConfiguredLifetime()
    {
        var before = DateTime.UtcNow;

        var token = Read(_sut.GenerateToken(Make.User()));

        // One minute of slack absorbs the clock ticking mid-test without making the
        // assertion meaningless — 60 minutes is still clearly distinguishable from 5.
        Assert.InRange(token.ValidTo, before.AddMinutes(59), before.AddMinutes(61));
    }

    [Fact]
    public void GenerateToken_SignsWithHmacSha256()
        => Assert.Equal(SecurityAlgorithms.HmacSha256, Read(_sut.GenerateToken(Make.User())).SignatureAlgorithm);

    [Fact]
    public void GenerateToken_ProducesATokenThatValidatesAgainstTheConfiguredKey()
    {
        var token = _sut.GenerateToken(Make.User());

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, ValidationParameters(SecretKey), out _);

        Assert.NotNull(principal);
    }

    [Fact]
    public void GenerateToken_ProducesATokenThatFailsValidationUnderADifferentKey()
    {
        var token = _sut.GenerateToken(Make.User());

        // This is the real point of signing: a token minted elsewhere must not pass.
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(
            () => new JwtSecurityTokenHandler().ValidateToken(
                token, ValidationParameters("a-completely-different-key-also-32-bytes"), out _));
    }

    [Fact]
    public void GenerateToken_ProducesDifferentTokens_ForDifferentUsers()
    {
        var first = _sut.GenerateToken(Make.User(email: "ada@example.com"));
        var second = _sut.GenerateToken(Make.User(email: "grace@example.com"));

        Assert.NotEqual(first, second);
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = false
    };
}
