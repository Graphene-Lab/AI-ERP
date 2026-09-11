using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using AI.Erp.Web.Models;

namespace AI.Erp.Plugins.SDK.Model
{
	public class PluginSettings
	{
		[JsonProperty(PropertyName = "version")]
		public int Version { get; set; }
	}
}
