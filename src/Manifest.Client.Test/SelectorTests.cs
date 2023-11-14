using FluentAssertions;
using Manifesto.AspNet.Utilities;

public class SelectorTests
{
    [Theory]
    [InlineData("test=none", "test", "none")]
    [InlineData(" test = none ", "test", "none")]
    [InlineData("test.uri.org/key=notempty ", "test.uri.org/key", "notempty")]
    public void ParseTest_WithEqualityOperators(string selector, string key, string value)
    {
        var sel = Selectors.TryParseSelector(selector);
        if(sel is Selector.Equality s){
            s.key.Should().BeEquivalentTo(key);
            s.value.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type Equality");
        }
    }
    
    [Theory]
    [InlineData("test!=none", "test", "none")]
    [InlineData(" test != none ", "test", "none")]
    public void ParseTest_WithInEqualityOperators(string selector, string key, string value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.Inequality s){
            s.key.Should().BeEquivalentTo(key);
            s.value.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type Inequality");
        }
    }
    
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test ", "test")]
    public void ParseTest_WithExistsOperator(string selector, string key)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.Exists s){
            s.key.Should().BeEquivalentTo(key);
        } else {
            Assert.Fail("Selector was not of type Exists");
        }
    }
    
    [Theory]
    [InlineData("!test", "test")]
    [InlineData(" !test ", "test")]
    [InlineData(" ! test ", "test")]
    public void ParseTest_WithNotExistsOperator(string selector, string key)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.NotExists s){
            s.key.Should().BeEquivalentTo(key);
        } else {
            Assert.Fail("Selector was not of type NotExists");
        }
    }
    
    [Theory]
    [InlineData("test in (1,2,3)", "test", new [] {"1", "2", "3"})]
    [InlineData("test in ( 1, 2, 3 3)", "test", new [] {"1", "2", "3 3"})]
    [InlineData("test in (none, any)", "test", new [] {"none", "any"})]
    public void ParseTest_WithInSetOperator(string selector, string key, string[] value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.InSet s){
            s.key.Should().BeEquivalentTo(key);
            s.values.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type InSet");
        }
    }
    
    [Theory]
    [InlineData("test notin (1,2,3)", "test", new [] {"1","2", "3"})]
    [InlineData("test notin (none, any)", "test", new [] {"none", "any"})]
    public void ParseTest_WithNotInSetOperator(string selector, string key, string[] value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.NotInSet s){
            s.key.Should().BeEquivalentTo(key);
            s.values.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type NotInSet");
        }
    }

    [Theory]
    [InlineData("foo in (1,2,3)", "foo=bar,sna=foo", false)]
    [InlineData("foo in (bar, baz)", "foo=bar,sna=foo", true)]
    [InlineData("foo in (bar)", "foo=bar,sna=foo", true)]
    public void ParseTest_Validation(string selector, string dictionary, bool shouldMatch)
    {
        
        var dict = dictionary.Split(",").Select(x => x.Split("=")).ToDictionary(x => x[0], x => x[1]);
        var sel = Selectors.TryParse(selector);
        
        Selectors.Validate(sel, dict).Should().Be(shouldMatch);
    }

    [Fact]
    public void ParseTest_ValidationWithNullSelector()
    {
        Selectors.Validate(Enumerable.Empty<Selector>(), new Dictionary<string, string>() {{"", ""}}).Should().Be(true);
    }
}
