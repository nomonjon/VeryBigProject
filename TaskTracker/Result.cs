using Microsoft.AspNetCore.Mvc;

namespace TaskTracker;

public class Error
{
    public string Code { get; }
    public string Message { get; }
    public int StatusCode { get; }

    public Error(string code, string message, int statusCode)
    {
        Code = code; Message = message; StatusCode = statusCode;
    }

    public static readonly Error None = new(string.Empty, string.Empty, 200);

    public static readonly Error NotModified = new("NotModified", "Resource not modified", 304);

    public static readonly Error NotFound = new("NotFound", "User not found", 404);
    public static readonly Error BadRequest = new("BadRequest", "Bad request", 400);
    public static readonly Error EmailTaken = new("EmailTaken", "Email already in use", 409);
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "Invalid email or password", 401);
    public static readonly Error Forbidden = new("Auth.Forbidden", "Forbidden", 403);
}

public class Result<T>
{
    public T? Value { get; }
    public Error Error { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = Error.None;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
        Value = default;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public IActionResult ToResponse() => IsSuccess
        ? new OkObjectResult(Value)
        : Error.StatusCode switch
        {
            304 => new StatusCodeResult(Error.StatusCode),
            400 => new BadRequestObjectResult(new { Error.Code, Error.Message }),
            401 => new UnauthorizedObjectResult(new { Error.Code, Error.Message }),
            403 => new ObjectResult(new { Error.Code, Error.Message }) { StatusCode = 403 },
            404 => new NotFoundObjectResult(new { Error.Code, Error.Message }),
            409 => new ConflictObjectResult(new { Error.Code, Error.Message }),
            _ => new ObjectResult(Error.Message) { StatusCode = 500 }
        };
}