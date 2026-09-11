using AI.Erp.Api;
using AI.Erp.Database;

namespace AI.Erp.Web.Services
{
	public class BaseService
	{
		protected RecordManager RecMan { get; private set; }
		protected EntityManager EntMan { get; private set; }
		protected SecurityManager SecMan { get; private set; }
		protected EntityRelationManager RelMan { get; private set; }
		protected DbFileRepository Fs { get; private set; }

		public BaseService(DbContext currentContext = null)
		{
			RecMan = new RecordManager(currentContext);
			EntMan = new EntityManager(currentContext);
			SecMan = new SecurityManager(currentContext);
			RelMan = new EntityRelationManager(currentContext);
			Fs = new DbFileRepository(currentContext);

		}

	}
}
