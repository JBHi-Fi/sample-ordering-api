namespace Jbh.SampleOrderingApi.Models;

public class PaymentInfo
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string CardNumber { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string CVV { get; set; } = string.Empty;
}