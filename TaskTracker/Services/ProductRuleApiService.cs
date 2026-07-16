using System.Net;

namespace TaskTracker.Services;

// Proxies the ProductRule REST API that lives on GrpcServer (:5002). Products
// themselves flow over gRPC, but rules are REST-only there, so this is a thin
// HTTP passthrough registered as a typed HttpClient.
public class ProductRuleApiService(HttpClient httpClient)
{
    public async Task<List<ProductRuleDto>> GetAllAsync()
    {
        var rules = await httpClient.GetFromJsonAsync<List<ProductRuleDto>>("api/ProductRule");
        return rules ?? [];
    }

    public async Task<ProductRuleDto> CreateAsync(CreateUpdateProductRuleDto dto)
    {
        var response = await httpClient.PostAsJsonAsync("api/ProductRule", dto);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ProductRuleApiException(response.StatusCode, message);
        }

        return (await response.Content.ReadFromJsonAsync<ProductRuleDto>())!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await httpClient.DeleteAsync($"api/ProductRule/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}

public class ProductRuleApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public class ProductRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Expression { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUpdateProductRuleDto
{
    public string Name { get; set; } = null!;
    public string Expression { get; set; } = null!;
    public string Color { get; set; } = "orange";
    public bool IsActive { get; set; } = true;
}
