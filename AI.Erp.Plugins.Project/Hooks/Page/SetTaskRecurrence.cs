using Microsoft.AspNetCore.Mvc;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Project.Services;
using AI.Erp.Web.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Plugins.Project.Hooks.Page
{
	[HookAttachment(key: "SetTaskRecurrence")]
	public class SetTaskRecurrence : IRecordDetailsPageHook
	{
		public IActionResult OnPost(RecordDetailsPageModel pageModel)
		{
			return new TaskService().SetTaskRecurrenceOnPost(pageModel);
		}
	}

}
