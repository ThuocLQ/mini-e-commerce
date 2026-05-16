namespace BasketService.Domain.Baskets;

public sealed class ShoppingCart
{
    public string UserId { get; set; } = string.Empty;
    public List<BasketItem> Items { get; set; } = [];

    public decimal TotalPrice => Items.Sum(item => item.Price * item.Quantity);

    public void AddItem(BasketItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ProductId))
        {
            throw new ArgumentException("ProductId is required.", nameof(item));
        }

        if (item.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than 0.", nameof(item));
        }

        if (item.Price < 0)
        {
            throw new ArgumentException("Price must be greater than or equal to 0.", nameof(item));
        }

        var existingItem = Items.FirstOrDefault(x => x.ProductId == item.ProductId);
        if (existingItem is null)
        {
            Items.Add(new BasketItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                Price = item.Price
            });

            return;
        }

        existingItem.Quantity += item.Quantity;
        existingItem.ProductName = item.ProductName;
        existingItem.Price = item.Price;
    }

    public bool UpdateItemQuantity(string productId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }

        if (quantity < 0)
        {
            throw new ArgumentException("Quantity cannot be negative.", nameof(quantity));
        }

        var existingItem = Items.FirstOrDefault(x => x.ProductId == productId);
        if (existingItem is null)
        {
            return false;
        }

        if (quantity == 0)
        {
            Items.Remove(existingItem);
            return true;
        }

        existingItem.Quantity = quantity;
        return true;
    }

    public bool RemoveItem(string productId)
    {
        var existingItem = Items.FirstOrDefault(x => x.ProductId == productId);
        if (existingItem is null)
        {
            return false;
        }

        Items.Remove(existingItem);
        return true;
    }

    public void Clear()
    {
        Items.Clear();
    }
}
