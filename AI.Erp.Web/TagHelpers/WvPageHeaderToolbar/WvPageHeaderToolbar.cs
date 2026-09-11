using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Services;
using AI.Erp.Web.Utils;
using AI.Erp.Api.Models;
using System.Linq;
using System.Globalization;

namespace AI.Erp.Web.TagHelpers
{
	[HtmlTargetElement("wv-page-header-toolbar", ParentTag = "wv-page-header")]
	public class WvPageHeaderToolbar : TagHelper
	{
		public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
		{
			return Task.CompletedTask;
		}

	}
}
