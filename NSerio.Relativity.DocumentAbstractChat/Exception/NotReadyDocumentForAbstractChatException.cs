using Relativity.Services.Exceptions;
using System.Net;

namespace NSerio.DocumentAbstractChat.Exception
{
	[ExceptionIdentifier("EE998E62-EDBC-4A46-AC33-F8FB66B61063")]
	[FaultCode(HttpStatusCode.PreconditionFailed)]
	public class NotReadyDocumentForAbstractChatException : ServiceException
	{
		public NotReadyDocumentForAbstractChatException()
			: base("Document is not ready for Abstract Chat.")
		{

		}
	}
}
