using System;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code after NN relation record is removed.")]
	public interface IErpPostDeleteManyToManyRelationHook
	{
		void OnPostDelete(string relationName, Guid? originId, Guid? targetId );
	}
}
