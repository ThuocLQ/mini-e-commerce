using FluentValidation;

namespace CatalogService.Application.Products.SearchProducts;

public sealed class SearchProductsQueryValidator : AbstractValidator<SearchProductsQuery>
{
    public SearchProductsQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .MaximumLength(200)
            .WithMessage("Search keyword must not exceed 200 characters.");
    }
}
