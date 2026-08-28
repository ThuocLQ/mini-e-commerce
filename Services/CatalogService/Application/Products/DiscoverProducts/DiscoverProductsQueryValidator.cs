using FluentValidation;

namespace CatalogService.Application.Products.DiscoverProducts;

public sealed class DiscoverProductsQueryValidator : AbstractValidator<DiscoverProductsQuery>
{
    public DiscoverProductsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(120)
            .WithMessage("Product search keyword must not exceed 120 characters.");

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Product category must not exceed 100 characters.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 48)
            .WithMessage("Product discovery page size must be between 1 and 48.");

        RuleFor(x => x.Cursor)
            .Must((query, cursor) => string.IsNullOrWhiteSpace(cursor) || ProductDiscoveryCursor.IsValidFor(cursor, query.Sort))
            .WithMessage("Product discovery cursor is invalid for the requested sort.");
    }
}