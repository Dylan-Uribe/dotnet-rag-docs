using Rag.API.Common;

namespace Rag.API.Endpoints;

public static class ErrorResults
{
    public static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.UnsupportedType => StatusCodes.Status415UnsupportedMediaType,
            ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(detail: error.Message, statusCode: status);
    }
}
