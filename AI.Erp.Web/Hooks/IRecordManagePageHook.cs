using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record manage page hooks")]
	public interface IRecordManagePageHook
	{
		IActionResult OnPreManageRecord(EntityRecord record, Entity entity, RecordManagePageModel pageModel, List<ValidationError> validationErrors );
		IActionResult OnPostManageRecord(EntityRecord record, Entity entity, RecordManagePageModel pageModel);
	}
}
