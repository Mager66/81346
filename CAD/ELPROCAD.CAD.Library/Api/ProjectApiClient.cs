using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ELPROCAD.CAD.Library.Api
{
	public sealed class ProjectApiClient
	{
		private readonly HttpClient _httpClient;

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true
		};

		public ProjectApiClient(HttpClient httpClient)
		{
			_httpClient = httpClient
				?? throw new ArgumentNullException(nameof(httpClient));
		}

		public async Task<ProjectResponse> CreateAsync(string name, string path, string description, CancellationToken cancellationToken = default)
		{
			var json = JsonSerializer.Serialize(new
			{
				name,
				path,
				description
			}, JsonOptions);

			using var content = new StringContent(
				json,
				Encoding.UTF8,
				"application/json");

			using var response = await _httpClient.PostAsync(
				"api/projects",
				content,
				cancellationToken).ConfigureAwait(false);

			var responseBody = await response.Content
				.ReadAsStringAsync().ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				throw new HttpRequestException(
					$"API-fel {(int)response.StatusCode}: {responseBody}");
			}

			return JsonSerializer.Deserialize<ProjectResponse>(
				responseBody,
				JsonOptions)
				?? throw new JsonException("API:t returnerade inget projekt.");
		}
	}
}
