using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Commons.Model.ServiceCustomHttpClient;

namespace Service.UI.Services;

public class CallServiceRegistry : ICallServiceRegistry
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public CallServiceRegistry(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResultAPI> Delete(ApiRequestModel req)
    {
        ApplyAuth(req);
        var response = await _httpClient.DeleteAsync(BuildUrl(req));
        return await ToResult(response);
    }

    public async Task<ResultAPI> Put(ApiRequestModel req, object data)
    {
        ApplyAuth(req);
        var response = data is null
            ? await _httpClient.PutAsync(BuildUrl(req), null)
            : await _httpClient.PutAsJsonAsync(BuildUrl(req), data);
        return await ToResult(response);
    }

    public async Task<ResultAPI<T>> Get<T>(ApiRequestModel req)
    {
        ApplyAuth(req);
        var response = await _httpClient.GetAsync(BuildUrl(req));
        return await ToResult<T>(response);
    }

    public async Task<ResultAPI<T>> Post<T>(ApiRequestModel req, object data)
    {
        ApplyAuth(req);
        var response = await _httpClient.PostAsJsonAsync(BuildUrl(req), data);
        return await ToResult<T>(response);
    }

    public async Task<ResultAPI<T>> Post<T>(ApiRequestModel req, HttpContent data)
    {
        ApplyAuth(req);
        var response = await _httpClient.PostAsync(BuildUrl(req), data);
        return await ToResult<T>(response);
    }

    public async Task<ResultAPI<byte[]>> Post(ApiRequestModel req, object data)
    {
        ApplyAuth(req);
        var response = await _httpClient.PostAsJsonAsync(BuildUrl(req), data);
        var result = new ResultAPI<byte[]>(StatusCode.InternalServerError);
        if (response.IsSuccessStatusCode)
        {
            result.Status = StatusCode.OK;
            result.Data = await response.Content.ReadAsByteArrayAsync();
        }
        else
        {
            result.Message = await response.Content.ReadAsStringAsync();
        }
        return result;
    }

    private static string BuildUrl(ApiRequestModel req)
    {
        var baseUrl = (req.ApiServiceCustom ?? string.Empty).TrimEnd('/');
        var path = req.Endpoint ?? string.Empty;
        if (!path.StartsWith('/')) path = "/" + path;
        var url = baseUrl + path;

        if (req.QueryParams is { Count: > 0 })
        {
            var qs = string.Join("&", req.QueryParams.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            url = url.Contains('?') ? $"{url}&{qs}" : $"{url}?{qs}";
        }
        return url;
    }

    private void ApplyAuth(ApiRequestModel req)
    {
        if (req.HasAuthorization && !string.IsNullOrEmpty(req.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", req.Token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static async Task<ResultAPI> ToResult(HttpResponseMessage response)
    {
        var result = new ResultAPI(StatusCode.InternalServerError);
        if (response.IsSuccessStatusCode)
        {
            result.Status = StatusCode.OK;
            result.Message = "OK";
        }
        else
        {
            result.Message = await response.Content.ReadAsStringAsync();
        }
        return result;
    }

    private static async Task<ResultAPI<T>> ToResult<T>(HttpResponseMessage response)
    {
        var result = new ResultAPI<T>(StatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            result.Status = StatusCode.OK;
            result.Message = "OK";
            if (!string.IsNullOrWhiteSpace(content))
            {
                result.Data = JsonSerializer.Deserialize<T>(content, JsonOptions);
            }
        }
        else
        {
            result.Message = content;
        }
        return result;
    }
}
