using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using AI.Erp.Api.Models;

namespace AI.Erp.Web.Models
{
	public enum SitemapNodeType
	{
		[SelectOption(Label = "entity list")]
		EntityList = 1,
		[SelectOption(Label = "application page")]
		ApplicationPage = 2,
		[SelectOption(Label = "url")]
		Url = 3,
	}
}
