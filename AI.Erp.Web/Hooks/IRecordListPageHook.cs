using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record list page hooks")]
	public interface IRecordListPageHook
	{
		IActionResult OnGet(RecordListPageModel pageModel);
		IActionResult OnPost(RecordListPageModel pageModel);
	}
}
