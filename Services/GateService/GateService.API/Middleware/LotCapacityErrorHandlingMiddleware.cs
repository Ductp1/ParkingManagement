using GateService.Application.Features.LotCapacity;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.API.Middleware;

/// <summary>
/// Middleware xử lý lỗi riêng của GateService cho US-066, chạy SAU ExceptionHandlingMiddleware dùng chung:
/// - CapacityValidationException → 400 ProblemDetails kèm extensions["errors"] = { field: [{code, message}] }.
/// - StaleCapacityConfigVersionException → 409 kèm errorCode CONFIG_VERSION_CONFLICT.
/// Exception còn lại (NotFound/Validation/Conflict của SharedKernel) để middleware dùng chung xử lý.
/// </summary>
public sealed class LotCapacityErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (CapacityValidationException ex)
        {
            if (context.Response.HasStarted) return;
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Cấu hình sức chứa không hợp lệ",
                Detail = ex.Summary?.Message ?? ex.Message,
                Instance = context.Request.Path,
            };
            problem.Extensions["errors"] = ex.Errors.ToDictionary(
                pair => pair.Key, pair => pair.Value.Select(e => new { code = e.Code, message = e.Message }));
            if (ex.Summary is not null)
                problem.Extensions["summary"] = new { code = ex.Summary.Code, message = ex.Summary.Message };
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
        catch (StaleCapacityConfigVersionException ex)
        {
            if (context.Response.HasStarted) return;
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Version cấu hình đã cũ",
                Detail = ex.Message,
                Instance = context.Request.Path,
            };
            problem.Extensions["errorCode"] = "CONFIG_VERSION_CONFLICT";
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}