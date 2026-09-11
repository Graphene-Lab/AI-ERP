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
	public class TestRecordRelatedRecrordManagePageHook : IRecordRelatedRecordManagePageHook
	{
		public IActionResult OnPostManageRecord(EntityRecord record, Entity entity, RecordRelatedRecordManagePageModel pageModel)
		{
			pageModel.TempData.Put("ScreenMessage", new ScreenMessage() { Message = "Record is updated successfully" });
			return null;
		}

		public IActionResult OnPreManageRecord(EntityRecord record, Entity entity, RecordRelatedRecordManagePageModel pageModel, List<ValidationError> validationErrors)
		{
			if (record.Properties.ContainsKey("name"))
			{
				if (record["name"] as string == "123")
					validationErrors.Add(new ValidationError("name", "123 as item name is not permitted"));
			}
			return null;
		}
	}
}
