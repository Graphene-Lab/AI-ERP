using System.Collections.Generic;
using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code before entity record update.")]
	public interface IErpPreUpdateRecordHook
	{
		void OnPreUpdateRecord(string entityName, EntityRecord record, List<ErrorModel> errors);
	}
}
