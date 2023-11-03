namespace Manifesto.AspNet.Utilities;

using System.Text.RegularExpressions;

public record Selector
{
    public record Equality(string key, string value) : Selector();
    public record Inequality(string key, string value) : Selector();
    public record InSet(string key, IEnumerable<string> values) : Selector();
    public record NotInSet(string key, IEnumerable<string> values) : Selector();
    public record Exists(string key) : Selector();
    public record NotExists(string key) : Selector();
    public record None() : Selector();
}


public static class Selectors
{
    private static Regex unequalRegex = new Regex(@"(?<key>\S+)\s*!=\s*(?<value>\w+)");
    private static Regex equalRegex = new Regex(@"(?<key>\S+)\s*=\s*(?<value>\w+)");
    private static Regex inRegex = new Regex(@"(?<key>\S+)\s+in\s*\((?<value>[\w\s,]*)\)");
    private static Regex notinRegex = new Regex(@"(?<key>\S+)\s*notin\s*\((?<value>[\w\s,]*)\)");
    private static Regex existsRegex = new Regex(@"^\s*(?<key>[^\r\n\t\f\v\s!]+)");
    private static Regex notexistsRegex = new Regex(@"!\s*(?<key>\S+)");
    private static Regex splitRegex = new Regex(@"(?![^)(]*\([^)(]*?\)\)),(?![^\(]*\))");

    public static Selector TryParseSelector(string selector)
    {
        var unequalMatch = unequalRegex.Match(selector);
        if (unequalMatch.Success)
        {
            return new Selector.Inequality(
                unequalMatch.Groups["key"].Value,
                unequalMatch.Groups["value"].Value);
        }

        var equalMatch = equalRegex.Match(selector);
        if (equalMatch.Success)
        {
            return new Selector.Equality(
                equalMatch.Groups["key"].Value,
                equalMatch.Groups["value"].Value);
        }

        var inMatch = inRegex.Match(selector);
        if (inMatch.Success)
        {
            var values = inMatch.Groups["value"].Value.Split(",").Select(x => x.Trim());
            return new Selector.InSet(
                inMatch.Groups["key"].Value,
                values);
        }

        var notinMatch = notinRegex.Match(selector);
        if (notinMatch.Success)
        {
            var values = notinMatch.Groups["value"].Value.Split(",").Select(x => x.Trim());
            return new Selector.NotInSet(
                notinMatch.Groups["key"].Value,
                values);
        }

        var existsMatch = existsRegex.Match(selector);
        if (existsMatch.Success)
        {
            return new Selector.Exists(existsMatch.Groups["key"].Value);
        }

        var notExistsMatch = notexistsRegex.Match(selector);
        if (notExistsMatch.Success)
        {
            return new Selector.NotExists(notExistsMatch.Groups["key"].Value);
        }

        return new Selector.None();
    }
    public static IEnumerable<Selector> TryParse(string input)
    {
        return splitRegex
            .Split(input)
            .Select(x => x.Trim())
            .Select(x => TryParseSelector(x))
            .Where(x => x is not Selector.None);
    }

    public static bool Validate(IEnumerable<Selector> selectors, IDictionary<string, string> labels)
    {
        return selectors.Any() && labels is not null
        ? selectors.Select(x => x switch
            {
                Selector.Equality sel => labels.ContainsKey(sel.key) && labels[sel.key] == sel.value,
                Selector.Inequality sel => labels.ContainsKey(sel.key) && labels[sel.key] != sel.value,
                Selector.InSet sel => labels.ContainsKey(sel.key) && sel.values.Any(x => x == labels[sel.key]),
                Selector.NotInSet sel => labels.ContainsKey(sel.key) && sel.values.All(x => x != labels[sel.key]),
                Selector.Exists sel => labels.ContainsKey(sel.key),
                Selector.NotExists sel => !labels.ContainsKey(sel.key),
                Selector.None => true,
                _ => false
            }).All(x => x == true)
        : false;
    }
}
