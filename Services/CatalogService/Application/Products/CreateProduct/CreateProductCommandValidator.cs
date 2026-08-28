using FluentValidation;

namespace CatalogService.Application.Products.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Product name is required.").MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).WithMessage("Product price cannot be negative.");
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative.");
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage("Product description must not exceed 1000 characters.");
        RuleFor(x => x.Category).MaximumLength(100).WithMessage("Product category must not exceed 100 characters.");
        RuleFor(x => x.ImageUrl).MaximumLength(2048).WithMessage("Product image URL must not exceed 2048 characters.").Must(BeAbsoluteHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.ImageUrl)).WithMessage("Product image URL must be an absolute HTTP or HTTPS URL.");
        RuleFor(x => x.Sku).NotEmpty().WithMessage("Product SKU is required.").MaximumLength(64).WithMessage("Product SKU must not exceed 64 characters.");
        RuleFor(x => x.Brand).MaximumLength(100).When(x => !string.IsNullOrWhiteSpace(x.Brand)).WithMessage("Product brand must not exceed 100 characters.");
    }

    private static bool BeAbsoluteHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}