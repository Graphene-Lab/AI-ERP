using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using AI.Erp.Web.Models;

namespace AI.Erp.Plugins.SDK.Model
{
	public class SitemapNodeSubmit : SitemapNode
	{
		[JsonProperty("pages")]
		public List<Guid> Pages { get; set; } = new List<Guid>();

    }
}
