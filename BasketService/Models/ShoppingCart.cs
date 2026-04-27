namespace BasketService.Models;

public class ShoppingCart
{
    public string UserId { get; set; } = string.Empty;
    public List<BasketItem> Items { get; set; } = new();
    
    public decimal TotalPrice => Items.Sum(item => item.Price * item.Quantity);
}