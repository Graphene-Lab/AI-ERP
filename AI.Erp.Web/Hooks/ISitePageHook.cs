using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Site page hooks")]
	public interface ISitePageHook
	{
		IActionResult OnGet(SitePageModel pageModel);
		IActionResult OnPost(SitePageModel pageModel);
	}
}
