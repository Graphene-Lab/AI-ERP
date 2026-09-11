//using AutoMapper;
//using AI.Erp.Database;
//using AI.Erp;
//using AI.Erp.Api.Models;
//using AI.Erp.Storage;

//namespace AI.Erp.Database.AutoMapper.Profiles
//{
//	internal class DbEntityRelationOptionsProfile : Profile
//	{
//		IErpService service;

//		public DbEntityRelationOptionsProfile(IErpService service)
//		{
//			this.service = service;
//		}

//		protected override void Configure()
//		{
//			Mapper.CreateMap<EntityRelationOptionsItem, DbEntityRelationOptions>();
//			Mapper.CreateMap<DbEntityRelationOptions, EntityRelationOptionsItem>();
//			Mapper.CreateMap<DbEntityRelationOptions, IStorageEntityRelationOptions>().ConstructUsing(x => CreateEmptyEntityRelationOptionsObject(x)); ;
//			Mapper.CreateMap<IStorageEntityRelationOptions, DbEntityRelationOptions>();
//		}

//		protected IStorageEntityRelationOptions CreateEmptyEntityRelationOptionsObject(DbEntityRelationOptions item)
//		{
//			return service.StorageService.GetObjectFactory().CreateEmptyEntityRelationOptionsObject();
//		}
//	}
//}
