using AutoMapper;
using System;
using AI.Erp.Database;

namespace AI.Erp.Api.Models.AutoMapper.Profiles
{
    internal class FieldPermissionsProfile : Profile
	{
		public FieldPermissionsProfile()
		{
			CreateMap<FieldPermissions, DbFieldPermissions>();
			CreateMap<DbFieldPermissions, FieldPermissions>();
		}
	}
}
