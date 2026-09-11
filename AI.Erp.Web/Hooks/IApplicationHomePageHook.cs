using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Application home page hooks")]
	public interface IApplicationHomePageHook
	{
		IActionResult OnGet(ApplicationHomePageModel pageModel);
		IActionResult OnPost(ApplicationHomePageModel pageModel);
	}
}
