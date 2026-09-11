using System;
using System.Collections.Generic;
using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code before delete NN relation record.")]
	public interface IErpPreDeleteManyToManyRelationHook
	{
		void OnPreDelete(string relationName, Guid? originId, Guid? targetId, List<ErrorModel> errors);
	}
}
