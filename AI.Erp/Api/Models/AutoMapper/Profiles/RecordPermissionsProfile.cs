using AutoMapper;
using AI.Erp.Database;

namespace AI.Erp.Api.Models.AutoMapper.Profiles
{
	internal class RecordPermissionsProfile : Profile
	{
		public RecordPermissionsProfile()
		{
			CreateMap<RecordPermissions, DbRecordPermissions>();
			CreateMap<DbRecordPermissions, RecordPermissions>();
		}
	}
}
