using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record related record list page hooks")]
	public interface IRecordRelatedRecordsListPageHook
	{
		IActionResult OnGet(RecordRelatedRecordsListPageModel pageModel);
		IActionResult OnPost(RecordRelatedRecordsListPageModel pageModel);
	}
}
