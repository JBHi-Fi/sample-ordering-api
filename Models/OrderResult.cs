namespace Jbh.SampleOrderingApi.Models;

public class OrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public bool EmailSent { get; set; }
}

public class PaymentResult
{
    public bool Success { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}