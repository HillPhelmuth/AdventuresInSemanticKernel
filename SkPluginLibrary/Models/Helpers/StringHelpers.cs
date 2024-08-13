using System.Text.RegularExpressions;
using Tiktoken;

namespace SkPluginLibrary.Models.Helpers
{
    public static class StringHelpers
    {
        private static Encoding? _tokenizer;
        static StringHelpers()
        {
            _tokenizer = Encoding.ForModel("gpt-3.5-turbo");
        }
        public static List<TokenString> EncodeDecodeWithSpaces(string? input)
        {
            if (input == null) return new List<TokenString>();
            _tokenizer ??= Encoding.ForModel("gpt-3.5-turbo");
            //encode string and keep spaces
            var encodedValues = new List<int>();
            var words = input.Split(' ');

            foreach (var word in words)
            {
                var text = $" {word}";
                var wordTokens = _tokenizer.Encode(text);
                encodedValues.AddRange(wordTokens);
                encodedValues.Add(-1);  // using -1 as the placeholder for a space
            }

            var tokenStrings = new List<TokenString>();

            foreach (var token in encodedValues)
            {
                if (token == -1)
                {
                    tokenStrings.Add(new TokenString("&nbsp;",0, token));
                }
                else
                {
                    var decodedToken = _tokenizer.Decode(new List<int> { token });
                    tokenStrings.Add(new TokenString(decodedToken, 0, token));
                }
            }
            return tokenStrings;
        }
        
        public static int GetTokens(string text)
        {
            _tokenizer ??= Encoding.ForModel("gpt-3.5-turbo");
            return _tokenizer.CountTokens(text);
        }

        public static string ExtractBase64FromDataUrl(this string dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl) || !dataUrl.StartsWith("data:"))
            {
                throw new ArgumentException("Invalid data URL.");
            }

            if (!dataUrl.Contains(";base64,"))
            {
                throw new ArgumentException("Not a base64 data URL.");
            }

            return dataUrl.Split(',').Last();
        }
        public static string ReplaceNonAsciiWithUnderscore(this string input)
        {
            return Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
        }
		public static List<string> SplitText(this string text, int maxCharLength)
		{
			var result = new List<string>();

			if (string.IsNullOrWhiteSpace(text))
			{
				return result;
			}

			var length = text.Length;
			var start = 0;

			while (start < length)
			{
				var end = start + maxCharLength;

				if (end >= length)
				{
					result.Add(text[start..]);
					break;
				}

				var lastSpace = text.LastIndexOf(' ', end, end - start);

				if (lastSpace > start)
				{
					result.Add(text.Substring(start, lastSpace - start));
					start = lastSpace + 1;
				}
				else
				{
					result.Add(text.Substring(start, maxCharLength));
					start += maxCharLength;
				}
			}

			return result;
		}

	}
}
