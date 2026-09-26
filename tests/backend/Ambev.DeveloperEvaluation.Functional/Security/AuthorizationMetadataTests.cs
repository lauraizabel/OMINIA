using System.Reflection;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth;
using Ambev.DeveloperEvaluation.WebApi.Features.Operations;
using Ambev.DeveloperEvaluation.WebApi.Features.Sales;
using Ambev.DeveloperEvaluation.WebApi.Features.Users;
using Ambev.DeveloperEvaluation.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class AuthorizationMetadataTests
{
    [Fact]
    public void Authentication_anonymous_access_is_declared_only_on_the_required_actions()
    {
        Assert.Empty(typeof(AuthController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));

        var anonymousActions = typeof(AuthController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            [
                nameof(AuthController.AuthenticateUser),
                nameof(AuthController.Logout),
                nameof(AuthController.Refresh)
            ],
            anonymousActions);
    }

    [Fact]
    public void Sales_administration_and_operations_controllers_keep_least_privilege_policies()
    {
        AssertPolicy<SalesController>(ApiPolicies.SalesOperators);
        AssertPolicy<UsersController>(ApiPolicies.Administrators);
        AssertPolicy<OutboxController>(ApiPolicies.Administrators);
    }

    private static void AssertPolicy<TController>(string expectedPolicy)
    {
        var attribute = Assert.Single(
            typeof(TController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        Assert.Equal(expectedPolicy, attribute.Policy);
        Assert.Empty(typeof(TController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
    }
}
