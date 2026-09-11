using Newtonsoft.Json;


namespace AI.Erp.Database
{
    public class DbCheckboxField : DbBaseField
    {
		[JsonProperty(PropertyName = "default_value")]
		public bool DefaultValue { get; set; }
    }
}