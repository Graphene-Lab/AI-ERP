using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Related record details page hooks")]
	public interface IRecordRelatedRecordDetailsPageHook
	{
		IActionResult OnPost(RecordRelatedRecordDetailsPageModel pageModel);
	}
}
