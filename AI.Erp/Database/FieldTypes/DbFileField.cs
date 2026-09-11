using Newtonsoft.Json;

namespace AI.Erp.Database
{
	public class DbFileField : DbBaseField
    {
		[JsonProperty(PropertyName = "default_value")]
		public string DefaultValue { get; set; }
    }
}