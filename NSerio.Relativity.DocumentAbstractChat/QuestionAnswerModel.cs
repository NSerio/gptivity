using Newtonsoft.Json;

namespace NSerio.DocumentAbstractChat
{
	public class QuestionAnswerModel
	{
		[JsonProperty("question")]
		public string Question { get; set; }
		[JsonProperty("answer")]
		public string Answer { get; set; }
	}
}
