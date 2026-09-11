using System.Collections.Generic;
using AI.Erp.Api.Models;
using AI.Erp.Eql;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code before entity search create.")]
	public interface IErpPreSearchRecordHook
	{
		void OnPreSearchRecord(string entityName, EqlSelectNode tree, List<EqlError> errors);
	}
}
