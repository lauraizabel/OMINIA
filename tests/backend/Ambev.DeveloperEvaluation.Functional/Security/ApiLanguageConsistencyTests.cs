using System.Globalization;
using Ambev.DeveloperEvaluation.WebApi.Common;
using Ambev.DeveloperEvaluation.WebApi.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Security;

public sealed class ApiLanguageConsistencyTests
{
    [Fact]
    public void Api_configuration_forces_default_validation_messages_to_English()
    {
        var previousLanguage = ValidatorOptions.Global.LanguageManager.Culture;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

            new ServiceCollection().AddApiProtection();
            var validator = new InlineValidator<ValidationTarget>();
            validator.RuleFor(target => target.Value).NotEmpty();

            var error = Assert.Single(validator.Validate(new ValidationTarget()).Errors);

            Assert.Equal("'Value' must not be empty.", error.ErrorMessage);
        }
        finally
        {
            ValidatorOptions.Global.LanguageManager.Culture = previousLanguage;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void Invalid_model_state_does_not_expose_localized_framework_messages()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiProtection();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Quantity", "O valor fornecido é inválido.");
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        var result = Assert.IsType<BadRequestObjectResult>(
            options.InvalidModelStateResponseFactory(actionContext));
        var response = Assert.IsType<ApiErrorResponse>(result.Value);
        var errors = Assert.IsAssignableFrom<IReadOnlyCollection<ApiErrorDetail>>(response.Errors);
        var error = Assert.Single(errors);

        Assert.Equal("quantity", error.Field);
        Assert.Equal("InvalidValue", error.Code);
        Assert.Equal("The supplied value is invalid.", error.Message);
    }

    private sealed class ValidationTarget
    {
        public string Value { get; init; } = string.Empty;
    }
}
