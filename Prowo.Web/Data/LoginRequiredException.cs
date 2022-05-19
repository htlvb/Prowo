using System;
using System.Runtime.Serialization;

namespace Prowo.Web.Data
{
    [Serializable]
    internal class LoginRequiredException : Exception
    {
        public LoginRequiredException()
        {
        }

        public LoginRequiredException(string message) : base(message)
        {
        }

        public LoginRequiredException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected LoginRequiredException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}