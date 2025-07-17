namespace Jbh.SampleOrderingApi.Models;

public class OrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public PaymentInfo PaymentInfo { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}