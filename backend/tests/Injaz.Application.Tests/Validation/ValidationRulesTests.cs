using Injaz.Application.Validation;

namespace Injaz.Application.Tests.Validation;

public sealed class ValidationRulesTests
{
    private enum Color
    {
        Red = 1,
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("first.last+tag@sub.example.org", true)]
    [InlineData("no-at-sign", false)]
    [InlineData("user@localhost", false)]
    [InlineData("Name <user@example.com>", false)]
    [InlineData("user @example.com", false)]
    public void EmailAddress_accepts_only_plain_addresses(string value, bool valid)
    {
        var context = new ValidationContext();

        context.RuleFor("Email", value).EmailAddress();

        Assert.Equal(valid, context.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotEmpty_rejects_null_and_whitespace(string? value)
    {
        var context = new ValidationContext();

        context.RuleFor("Title", value).NotEmpty();

        Assert.Equal("required", Assert.Single(context.Errors["title"]).Code);
    }

    [Fact]
    public void Length_rules_ignore_null_so_optional_fields_stay_optional()
    {
        var context = new ValidationContext();

        context.RuleFor("Description", (string?)null).MinLength(3).MaxLength(10);

        Assert.True(context.IsValid);
    }

    [Fact]
    public void Guid_enum_and_range_rules_report_their_codes()
    {
        var context = new ValidationContext();

        context.RuleFor("BoardId", Guid.Empty).NotEmpty();
        context.RuleFor("Color", (Color)99).IsInEnum();
        context.RuleFor("Limit", 500).InclusiveBetween(1, 100);

        Assert.Equal("required", Assert.Single(context.Errors["boardId"]).Code);
        Assert.Equal("invalidValue", Assert.Single(context.Errors["color"]).Code);

        var range = Assert.Single(context.Errors["limit"]);
        Assert.Equal("outOfRange", range.Code);
        Assert.Equal(1, range.Parameters!["min"]);
        Assert.Equal(100, range.Parameters!["max"]);
    }

    [Theory]
    [InlineData("Title", "title")]
    [InlineData("Items[0].Text", "items[0].text")]
    [InlineData("$.dueAt", "dueAt")]
    [InlineData("$", "body")]
    [InlineData("", "body")]
    public void Field_names_are_normalized_to_json_paths(string input, string expected) =>
        Assert.Equal(expected, FieldName.Normalize(input));
}
