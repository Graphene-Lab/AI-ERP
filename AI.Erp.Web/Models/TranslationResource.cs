using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Erp.Web.Models
{
	public class TranslationResource
	{
		[JsonProperty("locale")]
		public string Locale { get; set; } = "";

		[JsonProperty("key")]
		public string Key { get; set; } = "";

		[JsonProperty("value")]
		public string Value { get; set; } = "";
	}
}
