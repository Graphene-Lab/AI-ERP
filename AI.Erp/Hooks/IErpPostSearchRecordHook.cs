using System.Collections.Generic;
using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code before entity record search.")]
	public interface IErpPostSearchRecordHook
	{
		void OnPostSearchRecord(string entityName, List<EntityRecord> record);
	}
}
