using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Models;

namespace AI.Erp.Web.Hooks
{
	[Hook("Global page hooks")]
	public interface IPageHook
	{
		IActionResult OnGet(BaseErpPageModel pageModel);
		IActionResult OnPost(BaseErpPageModel pageModel);
	}
}
