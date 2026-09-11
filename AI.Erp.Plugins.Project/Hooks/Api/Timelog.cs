using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api;
using AI.Erp.Api.Models;
using AI.Erp.Eql;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Project.Services;

namespace AI.Erp.Plugins.Project.Hooks.Api
{
	[HookAttachment("timelog")]
	public class Timelog : IErpPreCreateRecordHook, IErpPreDeleteRecordHook
	{

		public void OnPreCreateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			new TimeLogService().PreCreateApiHookLogic(entityName, record, errors);
		}

		public void OnPreDeleteRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			new TimeLogService().PreDeleteApiHookLogic(entityName, record, errors);
		}

	}
}
