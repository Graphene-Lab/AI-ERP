using System.Text.Json.Serialization;

namespace AI.Erp.WebAssembly.Models;

public class WvUser
{
	[JsonPropertyName("id")]
	public Guid Id { get; set; }
    [JsonPropertyName("email")]
    public string Email { get; set; }
}
