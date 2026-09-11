using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Hooks;
using AI.Erp.Plugins.Next;
using AI.Erp.Plugins.Next.Services;

namespace AI.Erp.Plugins.Next.Hooks.Api
{
	[HookAttachment("account",int.MinValue)]
	public class AccountHook : IErpPostCreateRecordHook, IErpPostUpdateRecordHook
	{
		public void OnPostCreateRecord(string entityName, EntityRecord record)
		{
			new SearchService().RegenSearchField(entityName,record, Configuration.AccountSearchIndexFields);
		}

		public void OnPostUpdateRecord(string entityName, EntityRecord record)
		{
			new SearchService().RegenSearchField(entityName,record, Configuration.AccountSearchIndexFields);
		}
	}
}
