using System;
using System.Collections.Generic;
using System.Linq;
using AI.Erp.Api.Models;
using AI.Erp.Eql;


//TODO develop service
namespace AI.Erp.Plugins.Project.Services
{
	public class ProjectService : BaseService
	{
		public EntityRecord Get(Guid projectId)
		{
			var projectRecord = new EntityRecord();
			var eqlCommand = "SELECT * from project WHERE id = @projectId";
			var eqlParams = new List<EqlParameter>() { new EqlParameter("projectId", projectId) };
			var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();
			if (!eqlResult.Any())
				throw new Exception("Error: No project was found for this ProjectId");

			return eqlResult.First();
		}

		public EntityRecordList GetProjectTimelogs(Guid projectId)
		{
			var projectRecord = new EntityRecord();
			var eqlCommand = "SELECT * from timelog WHERE l_related_records CONTAINS @projectId";
			var eqlParams = new List<EqlParameter>() { new EqlParameter("projectId", projectId) };
			var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();

			return eqlResult;
		}


	}
}
