using System;
using AutoMapper;

namespace AI.Erp.Api.Models.AutoMapper.Resolvers
{
    public class DateTimeTypeConverter : ITypeConverter<DateTimeOffset, DateTime>
    {
        public DateTime Convert(DateTimeOffset source, DateTime destination, ResolutionContext context)
        {
            return source.DateTime;
        }
    }
}