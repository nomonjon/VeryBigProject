using Microsoft.AspNetCore.Mvc;

namespace TaskTracker.Tests.Common;

/// <summary>
/// <see cref="Result{T}"/> is the contract between every service and every controller
/// in TaskTracker: services return it, controllers call <c>ToResponse()</c> on it. The
/// status-code table below is therefore the API's error contract in one place —
/// changing it changes every endpoint at once, which is exactly why it is pinned here.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_CarriesTheValue()
    {
        var result = Result<string>.Success("value");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void Success_HasNoError()
        => Assert.Same(Error.None, Result<string>.Success("value").Error);

    [Fact]
    public void Failure_CarriesTheError_AndNoValue()
    {
        var result = Result<string>.Failure(Error.NotFound);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(Error.NotFound, result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Failure_OfAValueType_LeavesTheDefault()
        => Assert.Equal(default, Result<bool>.Failure(Error.NotFound).Value);

    [Fact]
    public void Success_CanCarryFalse_WithoutLookingLikeAFailure()
    {
        // DeleteUserAsync returns Success(false) for "nothing was deleted".
        // IsSuccess must not be inferred from the value.
        var result = Result<bool>.Success(false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    // ---------- ToResponse ----------

    [Fact]
    public void ToResponse_Returns200_WithTheValue_OnSuccess()
    {
        var ok = Assert.IsType<OkObjectResult>(Result<string>.Success("value").ToResponse());

        Assert.Equal("value", ok.Value);
    }

    [Fact]
    public void ToResponse_Returns304_WithNoBody_ForNotModified()
    {
        var result = Assert.IsType<StatusCodeResult>(Result<string>.Failure(Error.NotModified).ToResponse());

        Assert.Equal(304, result.StatusCode);
    }

    [Fact]
    public void ToResponse_Returns400_ForBadRequest()
        => Assert.IsType<BadRequestObjectResult>(Result<string>.Failure(Error.BadRequest).ToResponse());

    [Fact]
    public void ToResponse_Returns401_ForInvalidCredentials()
        => Assert.IsType<UnauthorizedObjectResult>(Result<string>.Failure(Error.InvalidCredentials).ToResponse());

    [Fact]
    public void ToResponse_Returns401_ForUnauthorized()
        => Assert.IsType<UnauthorizedObjectResult>(Result<string>.Failure(Error.Unauthorized).ToResponse());

    [Fact]
    public void ToResponse_Returns403_ForForbidden()
    {
        var result = Assert.IsType<ObjectResult>(Result<string>.Failure(Error.Forbidden).ToResponse());

        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void ToResponse_Returns404_ForNotFound()
        => Assert.IsType<NotFoundObjectResult>(Result<string>.Failure(Error.NotFound).ToResponse());

    [Fact]
    public void ToResponse_Returns409_ForEmailTaken()
        => Assert.IsType<ConflictObjectResult>(Result<string>.Failure(Error.EmailTaken).ToResponse());

    [Fact]
    public void ToResponse_Returns500_ForAnUnmappedStatusCode()
    {
        var result = Assert.IsType<ObjectResult>(
            Result<string>.Failure(new Error("Teapot", "I'm a teapot", 418)).ToResponse());

        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void ToResponse_IncludesTheCodeAndMessage_InTheErrorBody()
    {
        var response = Assert.IsType<NotFoundObjectResult>(Result<string>.Failure(Error.NotFound).ToResponse());

        // The body is an anonymous type; reflection is the only way in, and it is worth
        // it — clients parse these fields.
        var body = response.Value!;
        Assert.Equal(Error.NotFound.Code, body.GetType().GetProperty("Code")!.GetValue(body));
        Assert.Equal(Error.NotFound.Message, body.GetType().GetProperty("Message")!.GetValue(body));
    }

    // ---------- the Error catalog ----------

    [Theory]
    [InlineData("NotModified", 304)]
    [InlineData("NotFound", 404)]
    [InlineData("BadRequest", 400)]
    [InlineData("EmailTaken", 409)]
    [InlineData("Auth.InvalidCredentials", 401)]
    [InlineData("Auth.Unauthorized", 401)]
    [InlineData("Auth.Forbidden", 403)]
    public void ErrorCatalog_PinsEachCodeToItsHttpStatus(string code, int statusCode)
    {
        var error = new[]
        {
            Error.NotModified, Error.NotFound, Error.BadRequest,
            Error.EmailTaken, Error.InvalidCredentials, Error.Unauthorized, Error.Forbidden
        }.Single(e => e.Code == code);

        Assert.Equal(statusCode, error.StatusCode);
    }

    [Fact]
    public void ErrorNone_IsAnEmptySuccessMarker()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(200, Error.None.StatusCode);
    }
}
