using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TaskTracker.Models;

namespace TaskTracker.Tests.TestKit;

/// <summary>
/// Builds the <see cref="IHttpContextAccessor"/> that ProjectService and WorkTaskService
/// read the caller's identity from.
///
/// Every authorization branch in those services keys off two claims — NameIdentifier
/// and Role — so "who is calling" must be a first-class, one-line arrange step. If it
/// is fiddly, tests quietly stop covering the Forbidden paths.
/// </summary>
public static class FakeHttpContext
{
    /// <summary>A caller with the given user id and role.</summary>
    public static IHttpContextAccessor For(Guid userId, string role)
        => Build(new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role));

    public static IHttpContextAccessor ForAdmin(Guid? userId = null)
        => For(userId ?? Guid.NewGuid(), Roles.Admin);

    public static IHttpContextAccessor ForUser(Guid userId)
        => For(userId, Roles.User);

    /// <summary>A caller carrying a role but no usable user id (token missing the subject claim).</summary>
    public static IHttpContextAccessor WithRoleOnly(string role)
        => Build(new Claim(ClaimTypes.Role, role));

    /// <summary>An authenticated principal with no claims at all.</summary>
    public static IHttpContextAccessor Anonymous() => Build();

    /// <summary>No HttpContext at all — e.g. a background worker calling the same service.</summary>
    public static IHttpContextAccessor NoContext()
        => new NullHttpContextAccessor();

    private static IHttpContextAccessor Build(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
