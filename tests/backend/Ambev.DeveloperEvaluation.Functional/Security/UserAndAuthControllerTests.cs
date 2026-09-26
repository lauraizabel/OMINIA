using System.Diagnostics;
using System.Security.Claims;
using Ambev.DeveloperEvaluation.Application.Auth.AuthenticateUser;
using Ambev.DeveloperEvaluation.Application.Users.CreateUser;
using Ambev.DeveloperEvaluation.Application.Users.DeleteUser;
using Ambev.DeveloperEvaluation.Application.Users.GetUser;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth;
using Ambev.DeveloperEvaluation.WebApi.Features.Auth.AuthenticateUserFeature;
using Ambev.DeveloperEvaluation.WebApi.Features.Users;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.CreateUser;
using Ambev.DeveloperEvaluation.WebApi.Features.Users.GetUser;
using Ambev.DeveloperEvaluation.IoC.Observability;
using Microsoft.AspNetCore.Http;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using Ambev.DeveloperEvaluation.Application.Auth.RefreshSessions;
using Ambev.DeveloperEvaluation.WebApi.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class UserAndAuthControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IRefreshSessionService _refreshSessions = Substitute.For<IRefreshSessionService>();

    [Fact]
    public async Task Authentication_controller_maps_request_and_result()
    {
        _mediator.Send(Arg.Any<AuthenticateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticateUserResult
            {
                Token = "token",
                RefreshToken = "opaque-refresh-token",
                RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                Email = "manager@example.com",
                Name = "Manager",
                Role = nameof(UserRole.Manager)
            });
        var controller = Controller();

        var action = await controller.AuthenticateUser(
            new AuthenticateUserRequest { Email = "manager@example.com", Password = "password" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action);
        var envelope = Assert.IsType<ApiResponseWithData<AuthenticateUserResponse>>(ok.Value);
        Assert.True(envelope.Success);
        Assert.Equal("token", envelope.Data!.Token);
        Assert.Contains("test-refresh=opaque-refresh-token", controller.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain("opaque-refresh-token", System.Text.Json.JsonSerializer.Serialize(envelope));
        await _mediator.Received(1).Send(
            Arg.Is<AuthenticateUserCommand>(command =>
                command.Email == "manager@example.com" && command.Password == "password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authentication_controller_rotates_and_revokes_cookie_sessions()
    {
        _mediator.Send(Arg.Any<RefreshUserSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshUserSessionResult(
                "new-access",
                "new-refresh",
                DateTimeOffset.UtcNow.AddDays(7),
                "manager@example.com",
                "Manager",
                nameof(UserRole.Manager)));
        var controller = Controller("current-refresh", "http://localhost:4200");

        var refreshed = Assert.IsType<OkObjectResult>(await controller.Refresh(CancellationToken.None));
        var envelope = Assert.IsType<ApiResponseWithData<AuthenticateUserResponse>>(refreshed.Value);
        Assert.Equal("new-access", envelope.Data!.Token);
        Assert.Contains("test-refresh=new-refresh", controller.Response.Headers.SetCookie.ToString());

        controller = Controller("new-refresh", "http://localhost:4200");
        Assert.IsType<NoContentResult>(await controller.Logout(CancellationToken.None));
        await _refreshSessions.Received(1).RevokeAsync("new-refresh", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cookie_session_endpoints_reject_untrusted_browser_origins()
    {
        var controller = Controller("refresh-token", "https://attacker.example");

        Assert.Equal(
            403,
            Assert.IsType<StatusCodeResult>(await controller.AuthenticateUser(
                new AuthenticateUserRequest
                {
                    Email = "manager@example.com",
                    Password = ValidTestPassword()
                },
                CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.Refresh(CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await controller.Logout(CancellationToken.None)).StatusCode);
        await _mediator.DidNotReceiveWithAnyArgs().Send(default(RefreshUserSessionCommand)!, default);
    }

    [Fact]
    public async Task Cookie_session_endpoints_accept_the_api_origin_and_reject_malformed_origins()
    {
        _mediator.Send(Arg.Any<RefreshUserSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new RefreshUserSessionResult(
                "access",
                "replacement",
                DateTimeOffset.UtcNow.AddDays(7),
                "manager@example.com",
                "Manager",
                nameof(UserRole.Manager)));
        var sameOrigin = Controller("refresh-token", "http://localhost:5119");

        Assert.IsType<OkObjectResult>(await sameOrigin.Refresh(CancellationToken.None));

        var malformed = Controller("refresh-token", "not an origin");
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(
            await malformed.Refresh(CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task Missing_refresh_cookie_is_unauthorized_and_logout_remains_idempotent()
    {
        var controller = Controller(origin: "http://localhost:4200");

        Assert.IsType<UnauthorizedResult>(await controller.Refresh(CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.Logout(CancellationToken.None));
        await _refreshSessions.DidNotReceiveWithAnyArgs().RevokeAsync(default!, default);
    }

    [Fact]
    public void Production_refresh_cookie_has_host_prefix_and_strict_security_attributes()
    {
        var options = Options.Create(new RefreshSessionOptions());
        var configuration = new ConfigurationBuilder().Build();
        var manager = new RefreshTokenCookieManager(options, configuration);
        var context = new DefaultHttpContext();

        manager.Write(context.Response, "opaque-token", DateTimeOffset.UtcNow.AddDays(7));

        var header = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("__Host-developerstore-refresh=opaque-token", header);
        Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_controller_maps_create_get_and_delete_contracts()
    {
        var id = Guid.NewGuid();
        _mediator.Send(Arg.Any<CreateUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserResult { Id = id });
        _mediator.Send(Arg.Any<GetUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new GetUserResult
            {
                Id = id,
                Name = "Admin User",
                Email = "admin@example.com",
                Phone = "+5511999999999",
                Role = UserRole.Admin,
                Status = UserStatus.Active
            });
        _mediator.Send(Arg.Any<DeleteUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteUserResponse { Success = true });
        var controller = new UsersController(_mediator);
        var request = new CreateUserRequest
        {
            Username = "Admin User",
            Email = "admin@example.com",
            Phone = "+5511999999999",
            Password = ValidTestPassword(),
            Role = UserRole.Admin,
            Status = UserStatus.Active
        };

        var created = Assert.IsType<CreatedResult>(
            await controller.CreateUser(request, CancellationToken.None));
        var createdEnvelope = Assert.IsType<ApiResponseWithData<CreateUserResponse>>(created.Value);
        Assert.Equal(id, createdEnvelope.Data!.Id);

        var read = Assert.IsType<OkObjectResult>(
            await controller.GetUser(id, CancellationToken.None));
        var readEnvelope = Assert.IsType<ApiResponseWithData<GetUserResponse>>(read.Value);
        Assert.Equal("Admin User", readEnvelope.Data!.Name);
        Assert.Equal(UserRole.Admin, readEnvelope.Data.Role);

        var deleted = Assert.IsType<OkObjectResult>(
            await controller.DeleteUser(id, CancellationToken.None));
        var deletedEnvelope = Assert.IsType<ApiResponse>(deleted.Value);
        Assert.True(deletedEnvelope.Success);
    }

    [Fact]
    public void Public_request_validators_accept_valid_and_reject_invalid_inputs()
    {
        var authentication = new AuthenticateUserRequestValidator();
        Assert.True(authentication.Validate(
            new AuthenticateUserRequest { Email = "user@example.com", Password = "password" }).IsValid);
        Assert.False(authentication.Validate(new AuthenticateUserRequest()).IsValid);

        var users = new CreateUserRequestValidator();
        Assert.True(users.Validate(new CreateUserRequest
        {
            Username = "Valid User",
            Email = "user@example.com",
            Phone = "+5511999999999",
            Password = ValidTestPassword(),
            Role = UserRole.Manager,
            Status = UserStatus.Active
        }).IsValid);
        Assert.False(users.Validate(new CreateUserRequest()).IsValid);
    }

    private static string ValidTestPassword() => string.Concat("Strong", 1, '!');

    private AuthController Controller(string? refreshToken = null, string? origin = null)
    {
        var options = Options.Create(new RefreshSessionOptions
        {
            CookieName = "test-refresh",
            SecureCookie = false
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
            })
            .Build();
        var controller = new AuthController(
            _mediator,
            _refreshSessions,
            new RefreshTokenCookieManager(options, configuration));
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost:5119");
        if (origin is not null)
            context.Request.Headers.Origin = origin;
        if (refreshToken is not null)
            context.Request.Headers.Cookie = $"test-refresh={refreshToken}";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    [Fact]
    public void Base_controller_exposes_consistent_envelopes_and_identity_claims()
    {
        var controller = new TestController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "42"),
                    new Claim(ClaimTypes.Email, "user@example.com")
                ]))
            }
        };

        Assert.Equal(42, controller.UserId());
        Assert.Equal("user@example.com", controller.Email());
        Assert.IsType<CreatedAtRouteResult>(controller.CreatedEnvelope("route", new { id = 42 }, "data"));
        Assert.False(Assert.IsType<ApiResponse>(Assert.IsType<BadRequestObjectResult>(
            controller.BadRequestEnvelope("bad")).Value).Success);
        Assert.Equal("Resource not found", Assert.IsType<ApiResponse>(Assert.IsType<NotFoundObjectResult>(
            controller.NotFoundEnvelope()).Value).Message);

        var page = new PaginatedList<int>([1, 2], 5, 2, 2);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal(3, page.TotalPages);
        var response = Assert.IsType<PaginatedResponse<int>>(
            Assert.IsType<OkObjectResult>(controller.Page(page)).Value);
        Assert.Equal(5, response.TotalCount);
    }

    [Fact]
    public void Base_controller_rejects_missing_identity_claims_and_page_boundaries_are_correct()
    {
        var controller = new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Assert.Throws<NullReferenceException>(() => controller.UserId());
        Assert.Throws<NullReferenceException>(() => controller.Email());

        var first = new PaginatedList<int>([1], 1, 1, 10);
        Assert.False(first.HasPrevious);
        Assert.False(first.HasNext);
    }

    [Fact]
    public void Correlation_provider_prefers_http_then_activity_and_keeps_a_stable_fallback()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "http-correlation" };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var provider = new HttpCorrelationIdProvider(accessor);
        Assert.Equal("http-correlation", provider.CorrelationId);

        accessor.HttpContext = null;
        using (var activity = new Activity("coverage").Start())
        {
            Assert.Equal(activity.TraceId.ToString(), provider.CorrelationId);
        }

        var fallback = provider.CorrelationId;
        Assert.NotEmpty(fallback);
        Assert.Equal(fallback, provider.CorrelationId);
    }

    private sealed class TestController : BaseController
    {
        public int UserId() => GetCurrentUserId();
        public string Email() => GetCurrentUserEmail();
        public IActionResult CreatedEnvelope<T>(string route, object values, T data) =>
            Created(route, values, data);
        public IActionResult BadRequestEnvelope(string message) => BadRequest(message);
        public IActionResult NotFoundEnvelope() => NotFound();
        public IActionResult Page<T>(PaginatedList<T> page) => OkPaginated(page);
    }
}
