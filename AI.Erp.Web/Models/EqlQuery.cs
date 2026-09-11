using Newtonsoft.Json;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Eql;

namespace AI.Erp.Web.Models
{
	public class EqlQuery
	{
		[JsonProperty(PropertyName = "eql")]
		public string Eql { get; set; }

		[JsonProperty(PropertyName = "parameters")]
		public List<EqlParameter> Parameters { get; set; } = new();
	}

	public class EqlDataSourceQuery
	{
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		[JsonProperty(PropertyName = "parameters")]
		public List<EqlParameter> Parameters { get; set; } = new();
	}
}
