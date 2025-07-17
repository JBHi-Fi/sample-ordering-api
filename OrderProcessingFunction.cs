using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Jbh.SampleOrderingApi.Models;
using Jbh.SampleOrderingApi.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Jbh.SampleOrderingApi;

public class OrderProcessingFunction
{
    private readonly ILogger<OrderProcessingFunction> _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, DateTime> _orderCache;
    private readonly object _cacheLock = new object();
    private readonly MockExternalServices _mockServices;

    public OrderProcessingFunction(
        ILogger<OrderProcessingFunction> logger,
        HttpClient httpClient,
        MockExternalServices mockServices)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _orderCache = new Dictionary<string, DateTime>();
        _mockServices = mockServices;
    }

    [Function("ProcessOrder")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Processing order request started");

            // Read request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var orderRequest = JsonSerializer.Deserialize<OrderRequest>(requestBody);

            if (orderRequest == null || string.IsNullOrEmpty(orderRequest.OrderId))
            {
                return await CreateErrorResponse(req, "Invalid request format", HttpStatusCode.BadRequest);
            }

            // Check for duplicate orders
            bool isDuplicate = false;
            lock (_cacheLock)
            {
                if (_orderCache.ContainsKey(orderRequest.OrderId))
                {
                    var lastProcessed = _orderCache[orderRequest.OrderId];
                    if (DateTime.UtcNow - lastProcessed < TimeSpan.FromMinutes(5))
                    {
                        isDuplicate = true;
                    }
                }
            }
            if (isDuplicate)
            {
                _logger.LogWarning($"Duplicate order detected: {orderRequest.OrderId}");
                return await CreateErrorResponse(req, "Duplicate order", HttpStatusCode.Conflict);
            }

            // Validate inventory
            var inventoryValid = await ValidateInventory(orderRequest.Items);
            if (!inventoryValid)
            {
                return await CreateErrorResponse(req, "Insufficient inventory", HttpStatusCode.BadRequest);
            }

            // Process payment
            var paymentResult = await ProcessPayment(orderRequest.PaymentInfo);
            if (!paymentResult.Success)
            {
                return await CreateErrorResponse(req, paymentResult.ErrorMessage, HttpStatusCode.PaymentRequired);
            }

            // Update inventory
            await UpdateInventory(orderRequest.Items);

            // Send confirmation email
            var emailSent = await SendConfirmationEmail(orderRequest.CustomerEmail, orderRequest.OrderId);

            // Cache the order
            lock (_cacheLock)
            {
                _orderCache[orderRequest.OrderId] = DateTime.UtcNow;
            } 

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.OK);
            var successResult = new OrderResult
            {
                OrderId = orderRequest.OrderId,
                Status = "Processed",
                ProcessedAt = DateTime.UtcNow,
                PaymentId = paymentResult.PaymentId,
                EmailSent = emailSent
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(successResult));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order");
            return await CreateErrorResponse(req, "Internal server error", HttpStatusCode.InternalServerError);
        }
    }

    private async Task<bool> ValidateInventory(List<OrderItem> items)
    {
        var tasks = items.Select(async item =>
        {
            try
            {
                // Use mock service for local testing
                if (_mockServices.IsLocalTesting())
                {
                    return await _mockServices.CheckInventory(item.ProductId, item.Quantity);
                }

                var response = await _httpClient.GetAsync($"https://inventory-service.com/api/check/{item.ProductId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var inventoryData = JsonSerializer.Deserialize<InventoryResponse>(content);
                    return inventoryData?.Available >= item.Quantity;
                }
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Failed to validate inventory for product {item.ProductId}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, $"Timeout validating inventory for product {item.ProductId}");
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }

    private async Task<PaymentResult> ProcessPayment(PaymentInfo paymentInfo)
    {
        try
        {
            // Use mock service for local testing
            if (_mockServices.IsLocalTesting())
            {
                return await _mockServices.ProcessPayment(paymentInfo);
            }

            var paymentData = new
            {
                Amount = paymentInfo.Amount,
                Currency = paymentInfo.Currency,
                CardNumber = paymentInfo.CardNumber,
                ExpiryDate = paymentInfo.ExpiryDate,
                CVV = paymentInfo.CVV
            };

            var json = JsonSerializer.Serialize(paymentData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://payment-gateway.com/api/process", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<PaymentApiResponse>(responseContent);

                return new PaymentResult
                {
                    Success = result.Status == "approved",
                    PaymentId = result.TransactionId,
                    ErrorMessage = result.Status != "approved" ? result.Message : null
                };
            }

            return new PaymentResult
            {
                Success = false,
                ErrorMessage = $"Payment service returned {response.StatusCode}"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Payment processing failed");
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Payment service unavailable"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse payment response");
            return new PaymentResult
            {
                Success = false,
                ErrorMessage = "Invalid payment response"
            };
        }
    }

    private async Task UpdateInventory(List<OrderItem> items)
    {
        var tasks = items.Select(async item =>
        {
            try
            {
                // Use mock service for local testing
                if (_mockServices.IsLocalTesting())
                {
                    return await _mockServices.UpdateInventory(item.ProductId, item.Quantity);
                }

                var updateData = new { ProductId = item.ProductId, Quantity = -item.Quantity };
                var json = JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://inventory-service.com/api/update", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to update inventory for product {item.ProductId}. Status: {response.StatusCode}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating inventory for product {item.ProductId}");
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);

        if (results.Any(r => !r))
        {
            _logger.LogWarning("Some inventory updates failed");
        }
    }

    private async Task<bool> SendConfirmationEmail(string customerEmail, string orderId)
    {
        try
        {
            // Use mock service for local testing
            if (_mockServices.IsLocalTesting())
            {
                return await _mockServices.SendEmail(customerEmail, orderId);
            }

            if (string.IsNullOrEmpty(customerEmail))
            {
                _logger.LogWarning($"No email provided for order {orderId}");
                return false;
            }

            var emailData = new
            {
                To = customerEmail,
                Subject = $"Order Confirmation - {orderId}",
                Body = $"Your order {orderId} has been processed successfully."
            };

            var json = JsonSerializer.Serialize(emailData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://email-service.com/api/send", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to send confirmation email for order {orderId}. Status: {response.StatusCode}");
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception sending confirmation email for order {orderId}");
            return false;
        }
    }
    
    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, string message, HttpStatusCode statusCode)
    {
        var response = req.CreateResponse(statusCode);
        var errorResult = new { Error = message, Timestamp = DateTime.UtcNow };
        await response.WriteStringAsync(JsonSerializer.Serialize(errorResult));
        return response;
    }
}