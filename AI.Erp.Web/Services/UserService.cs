using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AI.Erp.Api.Models;
using AI.Erp.Api.Models.AutoMapper;
using AI.Erp.Database;
using AI.Erp.Eql;
using AI.Erp.Utilities;

namespace AI.Erp.Web.Services
{
	public class UserService : BaseService
	{

		public EntityRecordList GetAll()
		{
			var projectRecord = new EntityRecord();
			var eqlCommand = "SELECT * from user";
			var eqlResult = new EqlCommand(eqlCommand).Execute();

			return eqlResult;
		}

		public EntityRecord Get(Guid userId)
		{
			var projectRecord = new EntityRecord();
			var eqlCommand = "SELECT * from user WHERE id = @userId";
			var eqlParams = new List<EqlParameter>() { new EqlParameter("userId", userId)};
			var eqlResult = new EqlCommand(eqlCommand, eqlParams).Execute();
			if (!eqlResult.Any()) {
				return null;
			}
			return eqlResult[0];
		}

	}
}
