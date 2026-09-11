//using AutoMapper;
//using AI.Erp.Database;
//using AI.Erp;
//using AI.Erp.Storage;

//namespace AI.Erp.Database.AutoMapper.Profiles
//{
//	internal class DbFieldPermissionsProfile : Profile
//	{
//		IErpService service;

//		public DbFieldPermissionsProfile(IErpService service)
//		{
//			this.service = service;
//		}

//		protected override void Configure()
//		{
//			Mapper.CreateMap<FieldPermissions, DbFieldPermissions>();
//			Mapper.CreateMap<DbFieldPermissions, FieldPermissions>();
//			Mapper.CreateMap<DbFieldPermissions, IStorageFieldPermissions>().ConstructUsing(x => CreateEmptyFieldPermissionsObject(x));
//			Mapper.CreateMap<IStorageFieldPermissions, DbFieldPermissions>();
//		}

//		protected IStorageFieldPermissions CreateEmptyFieldPermissionsObject(DbFieldPermissions permissions)
//		{
//			var storageService = service.StorageService;
//			return storageService.GetObjectFactory().CreateEmptyFieldPermissionsObject();
//		}
//	}
//}
