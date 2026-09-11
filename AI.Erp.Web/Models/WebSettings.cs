using Newtonsoft.Json;
using System;

namespace AI.Erp.Web.Models
{
	public class WebSettings
	{
		[JsonProperty("theme_id")]
		public Guid ThemeId { get; set; } = Guid.Empty; 
	}
}
