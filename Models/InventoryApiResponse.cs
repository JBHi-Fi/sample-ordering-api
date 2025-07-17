namespace Jbh.SampleOrderingApi.Models;

public class InventoryResponse
{
    public string ProductId { get; set; } = string.Empty;
    public int Available { get; set; }
}