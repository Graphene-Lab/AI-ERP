using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Project.Services;

namespace AI.Erp.Plugins.Project.Hooks.Api
{
	[HookAttachment("comment")]
	public class Comment : IErpPreCreateRecordHook, IErpPostCreateRecordHook
	{

		public void OnPreCreateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			new CommentService().PreCreateApiHookLogic(entityName, record, errors);
		}

		public void OnPostCreateRecord(string entityName, EntityRecord record)
		{
			new CommentService().PostCreateApiHookLogic(entityName, record);
		}

	}
}
