using Newtonsoft.Json;

namespace AI.Erp.Database
{
	public class DbSystemSettings : DbDocumentBase
    {
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
    }
}
