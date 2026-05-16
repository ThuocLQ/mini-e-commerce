using FluentValidation;

namespace CatalogService.Application.Products.GetProductsByPrice;

public sealed class GetProductsByPriceQueryValidator : AbstractValidator<GetProductsByPriceQuery>
{
    public GetProductsByPriceQueryValidator()
    {
        RuleFor(x => x.Min)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum price cannot be negative.");

        RuleFor(x => x.Max)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Maximum price cannot be negative.")
            .GreaterThanOrEqualTo(x => x.Min)
            .WithMessage("Maximum price must be greater than or equal to minimum price.");
    }
}
