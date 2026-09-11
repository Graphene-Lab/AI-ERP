using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages;

namespace AI.Erp.Web.Hooks
{
	[Hook("Logout page hooks")]
	public interface ILogoutPageHook
	{
		IActionResult OnGet(LogoutModel pageModel);
		IActionResult OnPost(LogoutModel pageModel);
	}
}
