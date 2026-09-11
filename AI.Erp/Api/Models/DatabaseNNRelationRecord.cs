using Newtonsoft.Json;
using System;

namespace AI.Erp.Api.Models
{
	public class DatabaseNNRelationRecord
	{
		[JsonProperty(PropertyName = "origin_id")]
		public Guid OriginId { get; set; }

		[JsonProperty(PropertyName = "target_id")]
		public Guid TargetId { get; set; }

	}
}
