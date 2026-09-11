//using AutoMapper;
//using AI.Erp.Database;
//using AI.Erp;
//using AI.Erp.Api.Models;
//using AI.Erp.Storage;

//namespace AI.Erp.Database.AutoMapper.Profiles
//{
//	internal class DbRecordPermissionsProfile : Profile
//	{
//		IErpService service;

//		public DbRecordPermissionsProfile(IErpService service)
//		{
//			this.service = service;
//		}

//		protected override void Configure()
//		{
//			Mapper.CreateMap<RecordPermissions, DbRecordPermissions>();
//			Mapper.CreateMap<DbRecordPermissions, RecordPermissions>();
//			Mapper.CreateMap<DbRecordPermissions, IStorageRecordPermissions>().ConstructUsing(x => CreateEmptyRecordPermissionsObject(x));
//			Mapper.CreateMap<IStorageRecordPermissions, DbRecordPermissions>();
//		}

//		protected IStorageRecordPermissions CreateEmptyRecordPermissionsObject(DbRecordPermissions recordPermissions)
//		{
//			var storageService = service.StorageService;
//			return storageService.GetObjectFactory().CreateEmptyRecordPermissionsObject();
//		}
//	}
//}
