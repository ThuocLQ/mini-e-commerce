using FluentValidation;

namespace CatalogService.Application.Products.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Product name is required.")
            .MaximumLength(200)
            .WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Product price cannot be negative.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Product description must not exceed 1000 characters.");
    }
}
