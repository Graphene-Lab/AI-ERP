using Newtonsoft.Json;
using System.Collections.Generic;

namespace AI.Erp.Api.Models
{
	public class EntityRecordList : List<EntityRecord>
	{
		[JsonProperty(PropertyName = "total_count")]
		public int TotalCount { get; set; } = 0;
	}
}
