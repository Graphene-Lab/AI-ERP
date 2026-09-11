using AI.Erp.Api.Models;

namespace AI.Erp.Hooks
{
	[Hook("Provide hook for point in code after entity record is created.")]
	public interface IErpPostCreateRecordHook
	{
		void OnPostCreateRecord(string entityName, EntityRecord record);
	}
}
