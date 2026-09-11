using Microsoft.AspNetCore.Mvc;
using AI.Erp.Api.Models;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment]
	class TestLoginPageHook : ILoginPageHook
	{
		public IActionResult OnPostAfterLogin(ErpUser user, LoginModel pageModel)
		{
			return null;
		}

		public IActionResult OnPostPreLogin(LoginModel pageModel)
		{
			return null;
		}
	}
}
