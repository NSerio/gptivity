using Relativity.Services.Exceptions;
using System.Net;

namespace NSerio.DocumentAbstractChat.Exception
{
	[ExceptionIdentifier("DEAC93E4-6E04-4BFF-9DAD-387DE00251C7")]
	[FaultCode(HttpStatusCode.PreconditionFailed)]
	internal class NotFoundAPIKeyException : ServiceException
	{
		public NotFoundAPIKeyException()
			: base("Key instance setting is not set.")
		{
		}
	}
}
