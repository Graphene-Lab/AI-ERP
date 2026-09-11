using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;
using AI.Erp.Web.Models;
using AI.Erp.Web.Utils;

namespace AI.Erp.Web.Hooks.TestHooks
{
	[HookAttachment()]
	public class TestRecordCreatePageHook : IRecordCreatePageHook
	{
		public IActionResult OnPostCreateRecord(EntityRecord record, Entity entity, RecordCreatePageModel pageModel)
		{
			pageModel.TempData.Put("ScreenMessage", new ScreenMessage() { Message = "Record is created successfully" });
			return null;
		}

		public IActionResult OnPreCreateRecord(EntityRecord record, Entity entity, RecordCreatePageModel pageModel, List<ValidationError> validationErrors)
		{
			if( record.Properties.ContainsKey("text"))
			{
				if (record["text"] as string == "123")
					validationErrors.Add(new ValidationError("text", "123 value is not permitted"));
			}
			return null;
		}
	}
}
