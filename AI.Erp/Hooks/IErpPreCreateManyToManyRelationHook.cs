using System;
using System.Collections.Generic;
using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code before NN relation between 2 entity record is created.")]
	public interface IErpPreCreateManyToManyRelationHook
	{
		void OnPreCreate(string relationName, Guid originId, Guid targetId, List<ErrorModel> errors);
	}
}
