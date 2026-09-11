using AutoMapper;
using AI.Erp.Database;

namespace AI.Erp.Api.Models.AutoMapper.Profiles
{
	internal class EntityRelationOptionsProfile : Profile
	{
		public EntityRelationOptionsProfile()
		{
			CreateMap<EntityRelationOptionsItem, DbEntityRelationOptions>();
			CreateMap<DbEntityRelationOptions, EntityRelationOptionsItem>();
		}
	}
}
