using System.Reflection;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults;
using PaymentService.API.Controllers;

namespace PaymentService.Test;

/// <summary>
/// US-078: PaymentService bật JWT (RS256, public key của UserService) để báo cáo doanh thu dùng policy RequireOwner.
/// Endpoint thanh toán / báo giá hiện có (VNPAY return/IPN, gate-settlement, pricing) phải giữ nguyên, không yêu cầu token.
/// </summary>
public class JwtAuthenticationSetupTests
{
    private static readonly string KeyPath = Path.Combine(AppContext.BaseDirectory, "Keys", "public.key");

    [Fact]
    public void PublicKey_IsCopiedWithApi_AndIsRsa2048OrMore()
    {
        Assert.True(File.Exists(KeyPath), $"Thiếu {KeyPath}");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(KeyPath));
        Assert.True(rsa.KeySize >= 2048);
    }

    [Fact]
    public async Task AddJwtAuth_WithServiceKey_RegistersBearerSchemeAndRequireOwnerPolicy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJwtAuth(KeyPath);
        await using var provider = services.BuildServiceProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, defaultScheme?.Name);

        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync("RequireOwner");
        Assert.NotNull(policy);
        var roles = Assert.Single(policy.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(["LotOwner"], roles.AllowedRoles);
    }

    [Fact]
    public void ExistingPaymentAndPricingControllers_DoNotRequireAuthentication()
    {
        var controllers = typeof(PaymentsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(OwnerRevenueReportsController)
                        && t != typeof(AdminCommissionController))
            .ToList();

        Assert.Contains(typeof(PaymentsController), controllers);
        Assert.Contains(typeof(PricingController), controllers);
        foreach (var controller in controllers)
        {
            Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>(inherit: true));
            var protectedActions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AuthorizeAttribute>(inherit: true) is not null)
                .Select(m => m.Name);
            Assert.Empty(protectedActions);
        }
    }

    [Fact]
    public void OwnerRevenueReportsController_RequiresOwnerPolicy()
    {
        var authorize = typeof(OwnerRevenueReportsController).GetCustomAttribute<AuthorizeAttribute>(inherit: true);
        Assert.Equal("RequireOwner", authorize?.Policy);
        Assert.Null(typeof(OwnerRevenueReportsController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.DoesNotContain(typeof(OwnerRevenueReportsController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    [Fact]
    public void AdminCommissionController_RequiresAdminPolicy()
    {
        var authorize = typeof(AdminCommissionController).GetCustomAttribute<AuthorizeAttribute>(inherit: true);
        Assert.Equal("RequireAdmin", authorize?.Policy);
        Assert.DoesNotContain(typeof(AdminCommissionController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }
}
