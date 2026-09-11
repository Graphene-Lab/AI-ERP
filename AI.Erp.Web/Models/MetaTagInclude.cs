using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Erp.Web.Models
{
	public class MetaTagInclude
	{
		[JsonProperty("content")]
		public string Content { get; set; } = "";

		[JsonProperty("name")]
		public string Name { get; set; } = "";

		[JsonProperty("property")]
		public string Property { get; set; } = "";

		[JsonProperty("charset")]
		public string Charset { get; set; } = "";
	}

}
