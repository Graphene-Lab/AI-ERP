using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Models;
using AI.Erp.Web.Pages.Application;
using AI.Erp.Web.Utils;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment()]
	public class TestRecordManagePageHook : IRecordManagePageHook
	{
		public IActionResult OnPostManageRecord(EntityRecord record, Entity entity, RecordManagePageModel pageModel)
		{
			pageModel.TempData.Put("ScreenMessage", new ScreenMessage() { Message = "Record is updated successfully" });
			return null;
		}

		public IActionResult OnPreManageRecord(EntityRecord record, Entity entity, RecordManagePageModel pageModel, List<ValidationError> validationErrors)
		{
			if (record.Properties.ContainsKey("text"))
			{
				if (record["text"] as string == "123")
					validationErrors.Add(new ValidationError("text", "123 value is not permitted"));
			}
			return null;
		}
	}
}
