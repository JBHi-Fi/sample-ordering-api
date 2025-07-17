using Jbh.SampleOrderingApi.Models;
using Microsoft.Extensions.Logging;

namespace Jbh.SampleOrderingApi.Services;

public class MockExternalServices
{
    private readonly ILogger<MockExternalServices> _logger;
    private readonly Dictionary<string, int> _inventory = new()
    {
        { "PRODUCT-001", 100 },
        { "PRODUCT-002", 50 },
        { "PRODUCT-003", 25 }
    };

    public MockExternalServices(ILogger<MockExternalServices> logger)
    {
        _logger = logger;
    }

    public bool IsLocalTesting() => true; // Always use mock for local testing

    public async Task<bool> CheckInventory(string productId, int quantity)
    {
        await Task.Delay(100); // Simulate network delay
        
        if (_inventory.ContainsKey(productId))
        {
            var available = _inventory[productId];
            _logger.LogInformation($"Inventory check: Product {productId} has {available} units, requesting {quantity}");
            return available >= quantity;
        }
        
        _logger.LogWarning($"Product {productId} not found in inventory");
        return false;
    }

    public async Task<PaymentResult> ProcessPayment(PaymentInfo paymentInfo)
    {
        await Task.Delay(200); // Simulate payment processing delay
        
        // Simulate payment success/failure based on card number
        if (paymentInfo.CardNumber.EndsWith("0000"))
        {
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Invalid card number"
            };
        }
        
        var paymentId = $"PAY-{Guid.NewGuid().ToString()[..8]}";
        _logger.LogInformation($"Payment processed successfully: {paymentId}");
        
        return new PaymentResult
        {
            Success = true,
            PaymentId = paymentId
        };
    }

    public async Task<bool> UpdateInventory(string productId, int quantity)
    {
        await Task.Delay(50); // Simulate network delay
        
        if (_inventory.ContainsKey(productId))
        {
            _inventory[productId] -= quantity;
            _logger.LogInformation($"Inventory updated: Product {productId} reduced by {quantity}");
            return true;
        }
        
        _logger.LogError($"Failed to update inventory for product {productId}");
        return false;
    }

    public async Task<bool> SendEmail(string customerEmail, string orderId)
    {
        await Task.Delay(150); // Simulate email sending delay
        
        // Simulate email failure for certain email addresses
        if (customerEmail.Contains("invalid"))
        {
            _logger.LogError($"Failed to send email to {customerEmail}");
            return false;
        }
        
        _logger.LogInformation($"Confirmation email sent to {customerEmail} for order {orderId}");
        return true;
    }
}