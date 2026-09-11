using Newtonsoft.Json;

namespace AI.Erp.Plugins.Next.Model
{
	internal class PluginSettings
	{
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
	}
}
