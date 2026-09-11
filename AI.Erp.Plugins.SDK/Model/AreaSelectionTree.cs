using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using AI.Erp.Api.Models;

namespace AI.Erp.Plugins.SDK.Model
{
	public class AreaSelectionTree
	{
		[JsonProperty(PropertyName = "area_id")]
		public Guid AreaId { get; set; } = Guid.Empty;

		[JsonProperty(PropertyName = "all_nodes")]
		public List<SelectOption> AllNodes { get; set; } = new List<SelectOption>();
	}
}
