using System;

namespace AI.Erp.Api.Models
{
    public enum QueryType
    {
        EQ,
        NOT,
        LT,
        LTE,
        GT,
        GTE,
        AND,
        OR,
        CONTAINS,
        STARTSWITH,
        REGEX,
		RELATED,
		NOTRELATED,
		FTS	
	}
}