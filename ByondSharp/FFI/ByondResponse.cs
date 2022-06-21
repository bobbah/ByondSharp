using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByondSharp.FFI;

public enum ResponseCode
{
    Unknown,
    Success,
    Error,
    Deferred
}

public struct ByondResponse
{
    private static readonly JsonSerializerOptions JsonSerializerConfig = new JsonSerializerOptions() { IncludeFields = true };

    public ResponseCode ResponseCode;
    [JsonIgnore]
    public Exception _Exception;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Exception => _Exception?.ToString();
    public string Data;

    public override string ToString() => JsonSerializer.Serialize(this, JsonSerializerConfig);
}