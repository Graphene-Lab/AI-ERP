using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record related record manage page hooks")]
	public interface IRecordRelatedRecordManagePageHook
	{
		IActionResult OnPreManageRecord(EntityRecord record, Entity entity, RecordRelatedRecordManagePageModel pageModel, List<ValidationError> validationErrors );
		IActionResult OnPostManageRecord(EntityRecord record, Entity entity, RecordRelatedRecordManagePageModel pageModel);
	}
}
