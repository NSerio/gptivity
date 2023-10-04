using NSerio.DocumentAbstractChat.Exception;
using Relativity.Kepler.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NSerio.DocumentAbstractChat
{
	/// <summary>
	/// Document Abstract Chat Manager
	/// </summary>
	[WebService("DocumentAbstractChatManager")]
	[ServiceAudience(Audience.Private)]
	[RoutePrefix("dac")]
	public interface IDocumentAbstractChatManager
	{
		/// <summary>
		/// Get the abstract of a document stored in NDAC Abstract field
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <exception cref="NotReadyDocumentForAbstractChatException">412 - Thrown when the document has no extracted text or abstract set</exception>
		/// <returns>Abstract of the document</returns>
		[HttpGet]
		[Route("{workspaceId}/{documentArtifactId}/abstract")]
		Task<string> GetAbstractAsync(int workspaceId, int documentArtifactId);

		/// <summary>
		/// Generate the abstract of a document and store it in NDAC Abstract field
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <exception cref="NotReadyDocumentForAbstractChatException">412 - Thrown when the document has no extracted text or abstract is already set</exception>
		/// <returns>Abstract of the document</returns>
		[HttpPost]
		[Route("{workspaceId}/{documentArtifactId}/abstract")]
		Task<string> GenerateAbstractAsync(int workspaceId, int documentArtifactId);

		/// <summary>
		/// Ask questions to the extracted text of a document
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <param name="question">Question to ask</param>
		/// <exception cref="NotReadyDocumentForAbstractChatException">412 - Thrown when the document has no extracted text</exception>
		/// <returns>Answer to the question</returns>
		[HttpPost]
		[Route("{workspaceId}/{documentArtifactId}/chat")]
		Task<string> ChatWithDocumentAsync(int workspaceId, int documentArtifactId, string question);

		/// <summary>
		/// Get the chat history of a document
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <returns></returns>
		[HttpGet]
		[Route("{workspaceId}/{documentArtifactId}/chat")]
		Task<IEnumerable<QuestionAnswerModel>> GetChatsWithDocumentAsync(int workspaceId, int documentArtifactId);
	}
}
