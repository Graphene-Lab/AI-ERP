using Microsoft.AspNetCore.Mvc;
using AI.Erp.Api.Models;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages;

namespace AI.Erp.Web.Hooks
{
	[Hook("Login page hooks")]
	public interface ILoginPageHook
	{
		IActionResult OnPostPreLogin(LoginModel pageModel);
		IActionResult OnPostAfterLogin(ErpUser user, LoginModel pageModel);
	}
}
