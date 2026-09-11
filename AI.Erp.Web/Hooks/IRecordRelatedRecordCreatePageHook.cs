using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Exceptions;
using AI.Erp.Hooks;
using AI.Erp.Web.Pages.Application;

namespace AI.Erp.Web.Hooks
{
	[Hook("Record related record create page hooks")]
	public interface IRecordRelatedRecordCreatePageHook
	{
		IActionResult OnPreCreateRecord(EntityRecord record, Entity entity, RecordRelatedRecordCreatePageModel pageModel, List<ValidationError> validationErrors );
		IActionResult OnPostCreateRecord(EntityRecord record, Entity entity, RecordRelatedRecordCreatePageModel pageModel);
	}
}
