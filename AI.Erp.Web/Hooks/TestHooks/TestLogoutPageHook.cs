using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment]
	class TestLogoutPageHook : ILogoutPageHook
	{
		public IActionResult OnGet(LogoutModel pageModel)
		{
			return null;
		}
		public IActionResult OnPost(LogoutModel pageModel)
		{
			return null;
		}

	}
}
