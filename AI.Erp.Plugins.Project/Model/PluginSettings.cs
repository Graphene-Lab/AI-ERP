using Newtonsoft.Json;

namespace AI.Erp.Plugins.Project.Model
{
	internal class PluginSettings
	{
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
	}
}
