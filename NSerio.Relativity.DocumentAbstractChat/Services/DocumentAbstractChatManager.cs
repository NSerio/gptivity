using Newtonsoft.Json.Linq;
using NSerio.DocumentAbstractChat.Exception;
using Relativity.API;
using Relativity.Services;
using Relativity.Services.Interfaces.Choice;
using Relativity.Services.Interfaces.Choice.Models;
using Relativity.Services.Interfaces.Shared.Models;
using Relativity.Services.Objects;
using Relativity.Services.Objects.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Sort = Relativity.Services.Objects.DataContracts.Sort;
using SortEnum = Relativity.Services.Objects.DataContracts.SortEnum;

namespace NSerio.DocumentAbstractChat.Services
{
	public class DocumentAbstractChatManager : IDocumentAbstractChatManager
	{
		private const string ARTIFACT_ID_FIELD_NAME = "Artifact ID";
		private const string EXTRACTED_TEXT_FIELD_NAME = "Extracted Text";
		private const string ABSTRACT_FIELD_NAME = "NDAC Abstract";
		private const int DOCUMENT_OBJECT_TYPE_ID = 10;
		private const int CHOICE_OBJECT_TYPE_ID = 7;
		private const int FIELD_OBJECT_TYPE_ID = 14;
		private const string QUESTION_ANSWER_TYPE_NAME = "NSQuAns";
		private const string DOCUMENT_FIELD_NAME = "Document";
		private const string NAME_FIELD_NAME = "Name";
		private const string FIELD_FIELD_NAME = "Field";
		private const string OBJECT_TYPE_FIELD_NAME = "Object Type";

		private readonly IObjectManager _objectManager;
		private readonly IChoiceManager _choiceManager;
		private readonly IInstanceSettingsBundle _instanceSettingsBundle;

		public DocumentAbstractChatManager(IHelper helper)
		{
			_objectManager = helper.GetServicesManager().CreateProxy<IObjectManager>(ExecutionIdentity.CurrentUser);
			_choiceManager = helper.GetServicesManager().CreateProxy<IChoiceManager>(ExecutionIdentity.CurrentUser);
			_instanceSettingsBundle = helper.GetInstanceSettingBundle();
		}

		public async Task<string> ChatWithDocumentAsync(int workspaceId, int documentArtifactId, string question)
		{
			const string TO_FIELD_SUFFIX = "- to";

			string answer, content = await GetExtractedTextAsync(workspaceId, documentArtifactId);

			string fieldName = default;
			if (question.Contains(TO_FIELD_SUFFIX))
			{
				var command = question.Split(new[] { TO_FIELD_SUFFIX }, StringSplitOptions.RemoveEmptyEntries);
				question = command.First().Trim();
				fieldName = command.Last().Trim();
			}
			answer = await AskToOpenAIAPIAsync(content, question);

			await Task.WhenAll(
				CreateDocumentQuestionAndAnswerTandemAsync(workspaceId, documentArtifactId, question, answer),
				UpdateFieldValueByFieldType(workspaceId, documentArtifactId, fieldName, answer)
			);
			return answer;
		}

		public async Task<IEnumerable<QuestionAnswerModel>> GetChatsWithDocumentAsync(int workspaceId, int documentArtifactId)
		{
			var result = await GetQueryResultAsync(workspaceId,
				new ObjectCondition(DOCUMENT_FIELD_NAME, ObjectConditionEnum.EqualTo, documentArtifactId),
				new FieldRef[]
				{
					new FieldRef { Name = nameof(QuestionAnswerModel.Question) },
					new FieldRef { Name = nameof(QuestionAnswerModel.Answer) }
				},
				length: 1000,
				objectTypeRef: new ObjectTypeRef
				{
					Name = QUESTION_ANSWER_TYPE_NAME
				});

			return result.Objects.Select(o => new QuestionAnswerModel
			{
				Question = o.Values[0] as string,
				Answer = o.Values[1] as string,
			});
		}

		public async Task<string> GenerateAbstractAsync(int workspaceId, int documentArtifactId)
		{
			const string question = "resume max 800 chars";

			string answer, content = await GetExtractedTextAsync(workspaceId, documentArtifactId);
			answer = await AskToOpenAIAPIAsync(content, question);

			var updateRequest = new UpdateRequest
			{
				Object = new RelativityObjectRef { ArtifactID = documentArtifactId },
				FieldValues = new[]
				{
					new FieldRefValuePair
					{
						Field = new FieldRef { Name = ABSTRACT_FIELD_NAME },
						Value = answer
					}
				}
			};
			await _objectManager.UpdateAsync(workspaceId, updateRequest);

			return answer;
		}

		public async Task<string> GetAbstractAsync(int workspaceId, int documentArtifactId)
		{
			Condition condition = new WholeNumberCondition(ARTIFACT_ID_FIELD_NAME, NumericConditionEnum.EqualTo, documentArtifactId);
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(EXTRACTED_TEXT_FIELD_NAME, TextConditionEnum.IsSet));
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(ABSTRACT_FIELD_NAME, TextConditionEnum.IsSet));

			var results = await GetQueryResultAsync(workspaceId, condition, new[] { new FieldRef { Name = ABSTRACT_FIELD_NAME } });

			if (results.TotalCount == 0)
			{
				throw new NotReadyDocumentForAbstractChatException();
			}
			var abstractText = results.Objects.First().Values.First() as string;

			return abstractText;
		}

		/// <summary>
		/// Update field value by field type
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <param name="fieldName">Field Name</param>
		/// <param name="answer">Answer</param>
		/// <returns></returns>
		private async Task UpdateFieldValueByFieldType(int workspaceId, int documentArtifactId, string fieldName, string answer)
		{
			if (!string.IsNullOrWhiteSpace(fieldName))
			{
				var prevValueRequest = new QueryRequest
				{
					ObjectType = new ObjectTypeRef { ArtifactTypeID = DOCUMENT_OBJECT_TYPE_ID },
					Fields = new[] { new FieldRef { Name = fieldName } },
					Condition = new WholeNumberCondition(ARTIFACT_ID_FIELD_NAME, NumericConditionEnum.EqualTo, documentArtifactId).ToQueryString(),
				};
				var prevValueResult = await _objectManager.QuerySlimAsync(workspaceId, prevValueRequest, 1, 1);

				var fieldType = prevValueResult.Fields.First().FieldType;
				var prevValue = prevValueResult.Objects.First().Values.First();
				object valueForRelativity;
				switch (fieldType)
				{
					case Relativity.Services.Objects.DataContracts.FieldType.FixedLengthText:
						valueForRelativity = answer;
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.LongText:
						valueForRelativity = string.Join(prevValue as string, Environment.NewLine, answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.WholeNumber:
						valueForRelativity = Convert.ToInt32(answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.Date:
						valueForRelativity = Convert.ToDateTime(answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.YesNo:
						valueForRelativity = answer.Contains("Yes") || Convert.ToBoolean(answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.Decimal:
					case Relativity.Services.Objects.DataContracts.FieldType.Currency:
						valueForRelativity = Convert.ToDecimal(answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.SingleChoice:
						valueForRelativity = await GetOrCreateChoiceInDocumentFieldAsync(workspaceId, fieldName, answer);
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.MultipleChoice:
						var appendtoList = (prevValue as List<Choice>) ?? new List<Choice>();
						var newItems = answer.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
							.Select(async name => await GetOrCreateChoiceInDocumentFieldAsync(workspaceId, fieldName, name))
							.Select(p => p.Result);
						appendtoList.AddRange(newItems);
						valueForRelativity = appendtoList;
						break;
					case Relativity.Services.Objects.DataContracts.FieldType.Empty:
					case Relativity.Services.Objects.DataContracts.FieldType.File:
					case Relativity.Services.Objects.DataContracts.FieldType.SingleObject:
					case Relativity.Services.Objects.DataContracts.FieldType.User:
					case Relativity.Services.Objects.DataContracts.FieldType.MultipleObject:
					default:
						valueForRelativity = null;
						break;
				}

				if (valueForRelativity != null)
				{
					var updateRequest = new UpdateRequest
					{
						Object = new RelativityObjectRef { ArtifactID = documentArtifactId },
						FieldValues = new[]
						{
							new FieldRefValuePair
							{
								Field = new FieldRef { Name = fieldName },
								Value = valueForRelativity
							}
						}
					};
					await _objectManager.UpdateAsync(workspaceId, updateRequest);
				}
			}
		}

		/// <summary>
		/// Get or create a choice in a field of document object type
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="fieldName">Field Name</param>
		/// <param name="choiceName">Choice Name</param>
		/// <returns></returns>
		private async Task<Choice> GetOrCreateChoiceInDocumentFieldAsync(int workspaceId, string fieldName, string choiceName)
		{
			Condition condition = new TextCondition(NAME_FIELD_NAME, TextConditionEnum.EqualTo, PrepareForRelativityCondition(choiceName));
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(OBJECT_TYPE_FIELD_NAME, TextConditionEnum.EqualTo, DOCUMENT_FIELD_NAME));
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(FIELD_FIELD_NAME, TextConditionEnum.EqualTo, fieldName));

			var choiceRequest = new QueryRequest
			{
				ObjectType = new ObjectTypeRef { ArtifactTypeID = CHOICE_OBJECT_TYPE_ID },
				Fields = new FieldRef[0],
				Condition = condition.ToQueryString(),
			};
			var choiceResponse = await _objectManager.QuerySlimAsync(workspaceId, choiceRequest, 1, 1);
			Choice choice = new Choice { Name = choiceName };
			if (choiceResponse.TotalCount == 0)
			{
				choice.ArtifactID = await CreateChoiceInDocumentFieldAsync(workspaceId, fieldName, choiceName);
			}
			else
			{
				choice.ArtifactID = choiceResponse.Objects.First().ArtifactID;
			}
			return choice;
		}

		/// <summary>
		/// Create a choice in a field of document object type
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="fieldName">Field Name</param>
		/// <param name="choiceName">Choice Name</param>
		/// <returns></returns>
		private async Task<int> CreateChoiceInDocumentFieldAsync(int workspaceId, string fieldName, string choiceName)
		{
			Condition condition = new TextCondition(OBJECT_TYPE_FIELD_NAME, TextConditionEnum.EqualTo, DOCUMENT_FIELD_NAME);
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(NAME_FIELD_NAME, TextConditionEnum.EqualTo, PrepareForRelativityCondition(fieldName)));
			var fieldRequest = new QueryRequest
			{
				ObjectType = new ObjectTypeRef { ArtifactTypeID = FIELD_OBJECT_TYPE_ID },
				Fields = new FieldRef[0],
				Condition = condition.ToQueryString(),
			};
			var fieldResponse = await _objectManager.QuerySlimAsync(workspaceId, fieldRequest, 1, 1);

			return await _choiceManager.CreateAsync(workspaceId, new ChoiceRequest
			{
				Name = choiceName,
				Order = 0,
				Field = new ObjectIdentifier { ArtifactID = fieldResponse.Objects.First().ArtifactID },
			});
		}

		/// <summary>
		/// Create NsQuAns object with a Question and Answer
		/// </summary>
		/// <param name="workspaceId">Workspace Artifact ID</param>
		/// <param name="documentArtifactId">Document Artifact ID</param>
		/// <param name="question">Question</param>
		/// <param name="answer">Answer</param>
		/// <returns></returns>
		private Task CreateDocumentQuestionAndAnswerTandemAsync(int workspaceId, int documentArtifactId, string question, string answer)
		{
			const string QUESTION_FIELD_NAME = "Question";
			const string ANSWER_FIELD_NAME = "Answer";

			var createRequest = new CreateRequest
			{
				ParentObject = new RelativityObjectRef { ArtifactID = documentArtifactId },
				ObjectType = new ObjectTypeRef { Name = QUESTION_ANSWER_TYPE_NAME },
				FieldValues = new[]
				{
					new FieldRefValuePair { Field = new FieldRef { Name = QUESTION_FIELD_NAME }, Value = question },
					new FieldRefValuePair { Field = new FieldRef { Name = ANSWER_FIELD_NAME }, Value = answer },
				}
			};
			return _objectManager.CreateAsync(workspaceId, createRequest);
		}

		/// <summary>
		/// Get QueryResultSlim for given condition
		/// </summary>
		/// <param name="workspaceID">Workspace Artifact ID</param>
		/// <param name="condition">Condition for the query
		/// <param name="fields">Fields to be returned</param>
		/// <param name="start">Starting index base 1</param>
		/// <param name="length">Number of records to be returned</param>
		/// <returns></returns>
		private Task<QueryResultSlim> GetQueryResultAsync(int workspaceID, Condition condition, FieldRef[] fields = null, int start = 1, int length = 1, ObjectTypeRef objectTypeRef = null)
			=> _objectManager.QuerySlimAsync(workspaceID, new QueryRequest
			{
				ObjectType = objectTypeRef ?? new ObjectTypeRef
				{
					ArtifactTypeID = DOCUMENT_OBJECT_TYPE_ID
				},
				Fields = fields ?? new FieldRef[0],
				Condition = condition.ToQueryString(),
				LongTextBehavior = LongTextBehavior.Default,
				MaxCharactersForLongTextValues = 4000,
				Sorts = new[]
				{
					new Sort
					{
						Direction = SortEnum.Ascending,
						FieldIdentifier = new FieldRef
						{
							Name = ARTIFACT_ID_FIELD_NAME
						}
					}
				}
			}, start, length);

		/// <summary>
		/// Ask OpenAI API to generate answer for given content and question
		/// </summary>
		/// <param name="content">content for contextualization</param>
		/// <param name="question">question to be answered</param>
		/// <returns></returns>
		private async Task<string> AskToOpenAIAPIAsync(string content, string question)
		{
			string answer;
			using (var httpClient = GetOpenAIHttpClient())
			{
				const string ABSTRACT_MSG = @"{{""messages"":[{{""role"":""system"",""content"":""{0}""}},{{""role"":""user"",""content"":""{1}""}}]}}";
				content = string.Format(ABSTRACT_MSG, content, question);

				var contentBody = new StringContent(content);
				contentBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");

				var response = await httpClient.PostAsync(string.Empty, contentBody);
				answer = await GetContentFromFirstChoiceAsync(response);
			}
			return answer;
		}

		/// <summary>
		/// Get HttpClient for OpenAI API
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotFoundAPIKeyException"></exception>
		private HttpClient GetOpenAIHttpClient()
		{
			string url = GetOpenAIUrl();
			string key = GetOpenAIKey();

			if (string.IsNullOrWhiteSpace(key))
			{
				throw new NotFoundAPIKeyException();
			}

			var httpClient = new HttpClient
			{
				BaseAddress = new Uri(url)
			};
			httpClient.DefaultRequestHeaders.Add("api-key", key);

			return httpClient;
		}

		/// <summary>
		/// Get the OpenAI API key from the instance setting bundle
		/// </summary>
		/// <returns></returns>
		private string GetOpenAIKey()
			=> _instanceSettingsBundle.GetString("ndac", "key");

		/// <summary>
		/// Get the OpenAI API url from the instance setting bundle
		/// </summary>
		/// <returns></returns>
		private string GetOpenAIUrl()
			=> _instanceSettingsBundle.GetString("ndac", "url");

		/// <summary>
		/// Get the first choice from OpenAI API response
		/// </summary>
		/// <param name="response">OpenAI API response</param>
		/// <returns></returns>
		private async Task<string> GetContentFromFirstChoiceAsync(HttpResponseMessage response)
		{
			var result = await response.EnsureSuccessStatusCode()
				.Content.ReadAsStringAsync();
			var jsonResult = JObject.Parse(result);

			return jsonResult["choices"].First()["message"]["content"].Value<string>();
		}

		/// <summary>
		/// Get the extracted text from the document
		/// </summary>
		/// <param name="workspaceId">workspace artifact id</param>
		/// <param name="documentArtifactId">document artifact id</param>
		/// <returns></returns>
		/// <exception cref="NotReadyDocumentForAbstractChatException">document has no extracted text</exception>
		private async Task<string> GetExtractedTextAsync(int workspaceId, int documentArtifactId)
		{
			Condition condition = new WholeNumberCondition(ARTIFACT_ID_FIELD_NAME, NumericConditionEnum.EqualTo, documentArtifactId);
			condition = new CompositeCondition(condition, CompositeConditionEnum.And, new TextCondition(EXTRACTED_TEXT_FIELD_NAME, TextConditionEnum.IsSet));

			var results = await GetQueryResultAsync(workspaceId, condition, new[] { new FieldRef { Name = EXTRACTED_TEXT_FIELD_NAME } });

			if (results.TotalCount == 0)
			{
				throw new NotReadyDocumentForAbstractChatException();
			}

			return PrepareTextForContent(results.Objects.First().Values.First() as string);
		}

		/// <summary>
		/// Sanitize double quote for JSON string
		/// </summary>
		/// <param name="str">string to sanitize</param>
		/// <returns></returns>
		private string SanitizeDoubleQuote(string str)
			=> str.Replace("\"", "\\\"");

		/// <summary>
		/// Reduce breakline for JSON string
		/// </summary>
		/// <param name="str">string to reduce breakline</param>
		/// <returns></returns>
		private string ReduceBreakline(string str)
			=> str.Replace("\r\n", "\\n");

		/// <summary>
		/// Prepare text for OpenAI API content
		/// </summary>
		/// <param name="str">string to prepare</param>
		/// <returns></returns>
		private string PrepareTextForContent(string str)
			=> ReduceBreakline(SanitizeDoubleQuote(str));

		/// <summary>
		/// Prepare text for Relativity Condition
		/// </summary>
		/// <param name="str">string to prepare</param>
		/// <returns></returns>
		private string PrepareForRelativityCondition(string str)
			=> str.Replace("\\", "\\\\").Replace("'", "\\'");

	}
}
