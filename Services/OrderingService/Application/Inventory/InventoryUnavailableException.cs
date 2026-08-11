namespace OrderingService.Application.Inventory;

public sealed class InventoryUnavailableException : Exception
{
    public InventoryUnavailableException(Exception innerException)
        : base("Catalog inventory is unavailable. Please try again later.", innerException) { }
}
