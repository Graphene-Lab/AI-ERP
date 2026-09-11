using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment]
	class TestApplicationHomePageHook : IApplicationHomePageHook
	{
		public IActionResult OnGet(ApplicationHomePageModel pageModel)
		{
			return null;
		}

		public IActionResult OnPost(ApplicationHomePageModel pageModel)
		{
			return null;
		}
	}
}
