using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment]
	class TestApplicationNodePageHook : IApplicationNodePageHook
	{
		public IActionResult OnGet(ApplicationNodePageModel pageModel)
		{
			return null;
		}

		public IActionResult OnPost(ApplicationNodePageModel pageModel)
		{
			return null;
		}
	}
}
