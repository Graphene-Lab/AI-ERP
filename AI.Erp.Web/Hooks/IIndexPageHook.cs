using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Index page hooks")]
	public interface IHomePageHook
	{
		IActionResult OnGet(HomePageModel pageModel);
		IActionResult OnPost(HomePageModel pageModel);
	}
}
