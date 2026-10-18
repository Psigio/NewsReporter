using System;
using System.Text.Json;
using NewsReporter.Dto;
using NewsReporter.Utilities;

namespace NewsReporter.Tests.Utilities;

public class UnixTimeConverterTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public UnixTimeConverterTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions();
        _jsonSerializerOptions.Converters.Add(new UnixTimeConverter());
    }

    [Fact]
    public void Numeric_Field_Handled()
    {
        // Create the input Json with the time as a number
        var inputJson = FakeInput.Replace("#TIME#", _now.ToUnixTimeSeconds().ToString());

        // Act
        var result = JsonSerializer.Deserialize<StoryResponse>(inputJson, _jsonSerializerOptions);

        // Assert time handled - note we lose the sub-second accuracy during the roundtrip as the API only seconds unix time in seconds
        Assert.NotNull(result);
        Assert.Equal(_now, result.Time, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void String_Field_Handled()
    {
        // Create the input Json with the time as a string
        var inputJson = FakeInput.Replace("#TIME#", $"\"{_now.ToUnixTimeSeconds()}\"");

        // Act
        var result = JsonSerializer.Deserialize<StoryResponse>(inputJson, _jsonSerializerOptions);

        // Assert time handled - note we lose the sub-second accuracy during the roundtrip as the API only seconds unix time in seconds
        Assert.NotNull(result);
        Assert.Equal(_now, result.Time, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Invalid_Field_Throws()
    {
        // Create the input Json with the time as a boolean
        var inputJson = FakeInput.Replace("#TIME#", "true");

        // Act / Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StoryResponse>(inputJson, _jsonSerializerOptions));
    }

    [Fact]
    public void String_Field_With_Non_Numeric_Value_Throws()
    {
        // Create the input Json with a non-unix timestamp as the string
        var inputJson = FakeInput.Replace("#TIME#", "\"not-a-timestamp\"");

        // Act / Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StoryResponse>(inputJson, _jsonSerializerOptions));
    }

    [Fact]
    public void Write_Remains_Unimplemented()
    {
        var input = new StoryResponse { By = "A", Descendants = 0, Score = 0, Time = _now, Title = "A-A", Url = "https://www.example.com/a" };

        // Act / Assert
        Assert.Throws<NotImplementedException>(() => JsonSerializer.Serialize(input, _jsonSerializerOptions));
    }

    private static string FakeInput => """
        {
            "title": "A-A",
            "url": "https://www.example.com",
            "by": "A",
            "time" : #TIME#,
            "score": 2,
            "descendants": 5
        }
        """;
}
