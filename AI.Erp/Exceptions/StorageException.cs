#region <--- DIRECTIVES --->

using System;

#endregion

namespace AI.Erp
{ 
    public class StorageException : Exception
	{
        public StorageException(Exception innerException = null)
           : base(innerException.Message, innerException)
        {
        }

        public StorageException( string message = null, Exception innerException = null ) 
            : base( message, innerException )
        {
        }
    }
}