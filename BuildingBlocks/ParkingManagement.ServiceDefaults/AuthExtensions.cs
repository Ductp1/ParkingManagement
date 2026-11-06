using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ParkingManagement.ServiceDefaults;

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuth(this IServiceCollection services, string publicKeyPath = "Keys/public.key")
    {
        // Thiếu public key là lỗi cấu hình; không tạo khóa ngẫu nhiên thay thế.
        if (!File.Exists(publicKeyPath))
            throw new InvalidOperationException($"Thiếu public key JWT: {Path.GetFullPath(publicKeyPath)}.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
        if (rsa.KeySize < 2048) throw new InvalidOperationException("Khóa RSA phải có ít nhất 2048 bit.");
        var publicKey = new RsaSecurityKey(rsa.ExportParameters(false));

        // 2. Cấu hình Authentication JwtBearer
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices.GetService<ITokenPrincipalValidator>();
                        if (validator is not null && !await validator.ValidateAsync(context.Principal!, context.HttpContext.RequestAborted))
                            context.Fail("Phiên đăng nhập không còn hiệu lực.");
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "ParkingManagement",
                    ValidateAudience = true,
                    ValidAudience = "ParkingManagement.Clients",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = publicKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.Zero
                };
            });

        // 3. Phân quyền RBAC (Role-Based Access Control)
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
            options.AddPolicy("RequireOwner", policy => policy.RequireAuthenticatedUser().RequireRole("LotOwner"));
            options.AddPolicy("RequireDriver", policy => policy.RequireAuthenticatedUser().RequireRole("Driver"));
            options.AddPolicy("RequireGateKeeper", policy => policy.RequireAuthenticatedUser().RequireRole("Staff"));
        });

        // 4. Đăng ký IHttpContextAccessor và ICurrentUser để lấy thông tin Token
        services.AddHttpContextAccessor();
        services.AddScoped<ParkingManagement.SharedKernel.Contracts.ICurrentUser, CurrentUser>();

        return services;
    }
}
