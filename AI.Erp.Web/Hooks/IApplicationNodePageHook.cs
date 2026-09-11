using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Application node page hooks")]
	public interface IApplicationNodePageHook
	{
		IActionResult OnPost(ApplicationNodePageModel pageModel);
		IActionResult OnGet(ApplicationNodePageModel pageModel);
	}
}
