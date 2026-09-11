using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace AI.Erp.Web.Models
{
	public class KeyStringList
	{
		[JsonProperty(PropertyName = "key")]
		public string Key { get; set; } = "";

		[JsonProperty(PropertyName = "values")]
		public List<string> Values { get; set; } = new List<string>();
	}
}
