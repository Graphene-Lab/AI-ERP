using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record details page hooks")]
	public interface IRecordDetailsPageHook
	{
		IActionResult OnPost(RecordDetailsPageModel pageModel);
	}
}
