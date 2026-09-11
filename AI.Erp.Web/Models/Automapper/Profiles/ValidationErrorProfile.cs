using System;
using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI.Erp.Api.Models;
using Newtonsoft.Json.Linq;
using AI.Erp.Api;
using AI.Erp.Api.Models.AutoMapper;
using AI.Erp.Exceptions;

namespace AI.Erp.Web.Models.AutoMapper.Profiles
{
	public class ValidationErrorProfile : Profile
	{
		public ValidationErrorProfile()
		{
			CreateMap<ErrorModel, ValidationError>().ConvertUsing(source => JTokenToAppConvert(source));
		}

		private static ValidationError JTokenToAppConvert(ErrorModel data)
		{
			if (data == null)
				return null;

			return new ValidationError(data.Key,data.Message);
		}
	}
}
