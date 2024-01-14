using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting.Hosting;
using Microsoft.CodeAnalysis.Scripting;

namespace SkPluginLibrary.Services;

public record ScriptCommand(string Code, ScriptState<object>? State);

public class ScriptSession
{
    private readonly ScriptOptions _options = ScriptOptions.Default
        .AddReferences(CompileResources.PortableExecutableReferences)
        .AddImports("System", "System.IO", "System.Collections.Generic", "System.Collections", "System.Console", "System.Diagnostics", "System.Dynamic", "System.Linq", "System.Linq.Expressions", "System.Net.Http", "System.Text", "System.Text.Json", "System.Net", "System.Threading.Tasks", "System.Numerics", "Microsoft.CodeAnalysis", "Microsoft.CodeAnalysis.CSharp");
    private readonly List<ScriptCommand> _history = new();

    public async Task<string> EvaluateAsync(string code)
    {
        var previousState = _history.Count > 0 ? _history[^1].State : null;
        var previousOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var newState = previousState == null
                ? await CSharpScript.RunAsync(code, _options)
                : await previousState.ContinueWithAsync(code, _options);
            _history.Add(new ScriptCommand(code, newState));
            if (newState.ReturnValue != null && !string.IsNullOrEmpty(newState.ReturnValue.ToString()))
            {
                Console.WriteLine((string?)CSharpObjectFormatter.Instance.FormatObject(newState.ReturnValue));
            }
        }
        catch (CompilationErrorException ex)
        {
            Console.WriteLine((string?)CSharpObjectFormatter.Instance.FormatException(ex));
        }
        Console.SetOut(previousOut);
        return writer.ToString();
    }

    public async Task<object> ReEvaluateAsync(int commandIndex, string newCode)
    {
        if (commandIndex < 0 || commandIndex >= _history.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(commandIndex));
        }

        // Replace the command at the specified index
        _history[commandIndex] = new ScriptCommand(newCode, null);

        // Clear the script state after the replaced command
        for (var i = commandIndex + 1; i < _history.Count; i++)
        {
            _history[i] = new ScriptCommand(_history[i].Code, null);
        }

        // Re-evaluate all commands
        ScriptState<object>? state = null;
        for (var i = 0; i < _history.Count; i++)
        {
            var command = _history[i];
            state = state == null
                ? await CSharpScript.RunAsync(command.Code, _options)
                : await state.ContinueWithAsync(command.Code, _options);
            _history[i] = command with { State = state };
        }

        return state!.ReturnValue;
    }
}

public class PokerMatch
{

    public class Player
    {
        public string Name { get; set; }
        public List<Card> Hand { get; set; }
    }

    public class Card
    {
        public string Suit { get; set; }
        public int Value { get; set; }
    }

    public Player DeterminePokerWinner(List<Player> players)
    {
        int highestScore = 0;
        Player winner = null;

        foreach (var player in players)
        {
            int playerScore = ScoreHand(player.Hand);

            if (playerScore > highestScore)
            {
                highestScore = playerScore;
                winner = player;
            }
            else if (playerScore == highestScore)
            {
                if (player.Hand.Max(c => c.Value) > winner.Hand.Max(c => c.Value))
                {
                    winner = player;
                }
            }
        }

        Console.WriteLine($"{winner.Name} is the WINNER!");
        return winner;
    }

    private int ScoreHand(List<Card> hand)
    {
        var hasQuads = hand.GroupBy(c => c.Value).Any(g => g.Count() == 4);
        var hasFlush = hand.GroupBy(c => c.Suit).Count() == 1;
        var hasStraight = hand.Select(c => c.Value).Distinct().Count() == 5 && hand.Max(c => c.Value) - hand.Min(c => c.Value) == 4;
        var hasTrips = hand.GroupBy(c => c.Value).Any(g => g.Count() == 3);
        var hasTwoPairs = hand.GroupBy(c => c.Value).Count() == 3;
        var hasOnePair = hand.GroupBy(c => c.Value).Any(g => g.Count() == 2);

        if (hasFlush && hasStraight) return 800; // Straight flush
        if (hasQuads) return 700; // Four of a kind
        if (hasOnePair && hasTrips) return 600; // Full house
        if (hasFlush) return 500; // Flush
        if (hasStraight) return 400; // Straight
        if (hasTrips) return 300; // Three of a kind
        if (hasTwoPairs) return 200; // Two pairs
        if (hasOnePair) return 100; // One pair
        return hand.Max(c => c.Value); // Highest card

    }
    public Player DetermineWinnerAfterRanks(List<Player> players)
    {
        Player winner = null;
        var highestScore = players.Max(p => ScoreHand(p.Hand));

        var potentialWinners = players.Where(p => ScoreHand(p.Hand) == highestScore).ToList();

        if (potentialWinners.Count == 1)
        {
            winner = potentialWinners.First();
        }
        else
        {
            switch (highestScore)
            {
                // Full House
                case >= 600 and < 700:
                    {
                        var highestThreeOfAKind = potentialWinners.Max(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 3).Select(g => g.Key).FirstOrDefault());
                        winner = potentialWinners.First(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 3).Select(g => g.Key).FirstOrDefault() == highestThreeOfAKind);
                        break;
                    }
                // Flush
                case >= 500 and < 600:
                    {
                        var highestCard = potentialWinners.Max(p => p.Hand.Max(c => c.Value));
                        winner = potentialWinners.First(p => p.Hand.Max(c => c.Value) == highestCard);
                        break;
                    }
                // Straight
                case >= 400 and < 500:
                    {
                        var highestCard = potentialWinners.Max(p => p.Hand.Max(c => c.Value));
                        winner = potentialWinners.First(p => p.Hand.Max(c => c.Value) == highestCard);
                        break;
                    }
                // Three of a kind
                case >= 300 and < 400:
                    {
                        var highestThreeOfAKind = potentialWinners.Max(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 3).Select(g => g.Key).FirstOrDefault());
                        winner = potentialWinners.First(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 3).Select(g => g.Key).FirstOrDefault() == highestThreeOfAKind);
                        break;
                    }
                // Two Pairs
                case >= 200 and < 300:
                    {
                        var highestPair = potentialWinners.Max(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 2).Select(g => g.Key).Max());
                        winner = potentialWinners.First(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 2).Select(g => g.Key).Max() == highestPair);
                        break;
                    }
                // One Pair
                case >= 100 and < 200:
                    {
                        var highestPair = potentialWinners.Max(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 2).Select(g => g.Key).Max());
                        winner = potentialWinners.First(p => p.Hand.GroupBy(c => c.Value).Where(g => g.Count() == 2).Select(g => g.Key).Max() == highestPair);
                        break;

                    }
                // High Card
                case >= 6 and < 100:
                    {
                        var skipCount = 1;
                        var stillTied = true;
                        int highestCard = 0;
                        while (stillTied || skipCount > 4)
                        {
                            highestCard = potentialWinners.Max(p =>
                                p.Hand.OrderByDescending(x => x).Skip(skipCount).Max(c => c.Value));
                            stillTied = potentialWinners.Count(p =>
                                p.Hand.OrderByDescending(x => x).Skip(skipCount).Max(c => c.Value) == highestCard) > 1;
                            skipCount++;
                        }
                        winner = potentialWinners.First(p => p.Hand.Max(c => c.Value) == highestCard);
                        break;
                    }
                default:
                    Console.WriteLine("Tie! No tie breaker rule defined for the current hand rank.");
                    break;
            }
        }

        Console.WriteLine($"{winner.Name} is the WINNER!");
        return winner;
    }
}