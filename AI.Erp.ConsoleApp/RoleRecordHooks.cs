using System;
using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Hooks;

namespace AI.Erp.ConsoleApp
{
	[HookAttachment("role")]
	public class RoleRecordHooks : IErpPreCreateRecordHook, IErpPostCreateRecordHook, IErpPreUpdateRecordHook, IErpPostUpdateRecordHook, IErpPreDeleteRecordHook, IErpPostDeleteRecordHook
	{
		public void OnPreCreateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			Console.WriteLine($"Pre create role: {record["name"]}");
		}

		public void OnPostCreateRecord(string entityName, EntityRecord record)
		{
			Console.WriteLine($"Post create role: {record["name"]}");
		}

		public void OnPreUpdateRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			Console.WriteLine($"Pre update role: {record["name"]}");
		}

		public void OnPostUpdateRecord(string entityName, EntityRecord record)
		{
			Console.WriteLine($"Post update role: {record["name"]}");
		}

		public void OnPreDeleteRecord(string entityName, EntityRecord record, List<ErrorModel> errors)
		{
			Console.WriteLine($"Pre delete role: {record["name"]}");
		}

		public void OnPostDeleteRecord(string entityName, EntityRecord record)
		{
			Console.WriteLine($"Post delete role: {record["name"]}");
		}


	}
}
