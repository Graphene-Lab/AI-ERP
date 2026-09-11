using AutoMapper;
using AI.Erp.Database;

namespace AI.Erp.Api.Models.AutoMapper.Profiles
{
	internal class EntityRelationProfile : Profile
	{
		public EntityRelationProfile()
		{
			CreateMap<EntityRelation, DbEntityRelation>();
			CreateMap<DbEntityRelation, EntityRelation>();
		}
	}
}