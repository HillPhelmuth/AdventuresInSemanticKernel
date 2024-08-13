using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace SkPluginLibrary.Models.Helpers;

public static class LogProbExtensions
{
	public static List<TokenString> AsTokenStrings(this List<ChatTokenLogProbabilityInfo> logProbContentItems)
	{
		var result = new List<TokenString>();
		foreach (var logProb in logProbContentItems)
		{
			var tokenString = logProb.AsTokenString()/* new TokenString(logProb.Token, logProb.NormalizedLogProb())*/;
			if (logProb.TopLogProbabilities is {Count: > 0})
			{
				var innerResult = logProb.TopLogProbabilities.Select(x => x.AsTokenString()).ToList();
				tokenString.TopLogProbs = innerResult;
			}
			result.Add(tokenString);
		}            
		return result;
	}
	public static TokenString AsTokenString(this ChatTokenLogProbabilityInfo logProbContentItem)
	{
		var result = new TokenString(logProbContentItem.Token, logProbContentItem.NormalizedLogProb());
		if (logProbContentItem.TopLogProbabilities is {Count: > 0})
        {
            var innerResult = logProbContentItem.TopLogProbabilities.Select(x => x.AsTokenString()).ToList();
            result.TopLogProbs = innerResult;
        }
		return result;
	}
	public static TokenString AsTokenString(this ChatTokenTopLogProbabilityInfo logProbInfo)
	{
		return new TokenString(logProbInfo.Token, logProbInfo.NormalizedLogProb());
	}
	public static double NormalizedLogProb(this ChatTokenTopLogProbabilityInfo logProbabilityResult)
	{
		return Math.Exp(logProbabilityResult.LogProbability);
	}
	public static double NormalizedLogProb(this ChatTokenLogProbabilityInfo logProbInfo)
	{
		return Math.Exp(logProbInfo.LogProbability);
	}
	
}