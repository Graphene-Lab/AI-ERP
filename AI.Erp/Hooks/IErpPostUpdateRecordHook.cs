using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code after entity record is updated.")]
	public interface IErpPostUpdateRecordHook
	{
		void OnPostUpdateRecord(string entityName, EntityRecord record); 
	}
}
