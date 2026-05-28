using OrderQueryService.Application.ReadModels;

namespace OrderQueryService.Infrastructure.ReadModels.MongoDb;

internal static class OrderSummaryDocumentMapper
{
    public static OrderSummaryDocument ToDocument(
        OrderSummaryReadModel model,
        DateTime? createdAtUtc = null)
    {
        return new OrderSummaryDocument
        {
            Id = model.OrderId.ToString("D"),
            OrderId = model.OrderId.ToString("D"),
            CustomerId = model.CustomerId.ToString("D"),
            CustomerName = model.CustomerName,
            Status = model.Status,
            TotalAmount = model.TotalAmount,
            Currency = model.Currency,
            ItemCount = model.ItemCount,
            Items = model.Items.Select(item => new OrderSummaryItemDocument
            {
                ProductId = item.ProductId.ToString("D"),
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            CreatedAtUtc = createdAtUtc ?? model.CreatedAtUtc,
            LastUpdatedAtUtc = model.LastUpdatedAtUtc
        };
    }

    public static OrderSummaryReadModel ToReadModel(OrderSummaryDocument document)
    {
        return new OrderSummaryReadModel
        {
            Id = Guid.Parse(document.Id),
            OrderId = Guid.Parse(document.OrderId),
            CustomerId = Guid.Parse(document.CustomerId),
            CustomerName = document.CustomerName,
            Status = document.Status,
            TotalAmount = document.TotalAmount,
            Currency = document.Currency,
            ItemCount = document.ItemCount,
            Items = document.Items.Select(item => new OrderSummaryItemReadModel
            {
                ProductId = Guid.Parse(item.ProductId),
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            CreatedAtUtc = document.CreatedAtUtc,
            LastUpdatedAtUtc = document.LastUpdatedAtUtc
        };
    }
}
