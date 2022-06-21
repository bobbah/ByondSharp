using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ByondSharp.Samples.Models;
using Microsoft.Toolkit.HighPerformance;
using RestSharp;

namespace ByondSharp.Samples;

/// <summary>
/// This is provided as an example for external library use, with RestSharp, as well as an async method call. This is a sample adapted from the Scrubby parsing server.
/// </summary>
public class BYONDDataService
{
    private const string BaseUrl = "https://www.byond.com/";
    private readonly RestClient _client;

    public BYONDDataService()
    {
        _client = new RestClient(BaseUrl);
    }

    public async Task<BYONDUserData> GetUserData(string key, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"members/{key}").AddQueryParameter("format", "text");
        var response = await _client.ExecuteAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        if (response.Content.Contains("not found.</div>"))
        {
            throw new BYONDUserNotFoundException($"User {key} not found.");
        }

        if (response.Content.Contains("is not active.</div>"))
        {
            throw new BYONDUserInactiveException($"User {key} is not active.");
        }

        return ParseResponse(response.Content.AsSpan());
    }

    private static BYONDUserData ParseResponse(ReadOnlySpan<char> data)
    {
        var toReturn = new BYONDUserData();

        foreach(var line in data.Tokenize('\n'))
        {
            if (line.StartsWith("general"))
            {
                continue;
            }
                
            var lineContext = line.Trim("\r\t ");
            var equalsLoc = lineContext.IndexOf('=');
            if (equalsLoc == -1)
                continue;

            var key = lineContext[..equalsLoc].Trim();
            var value = lineContext[(equalsLoc + 1)..].Trim("\" ");
            if (key.Equals("key", StringComparison.OrdinalIgnoreCase))
            {
                toReturn.Key = value.ToString();
            }
            else if (key.Equals("ckey", StringComparison.OrdinalIgnoreCase))
            {
                toReturn.CKey = value.ToString();
            }
            else if (key.Equals("gender", StringComparison.OrdinalIgnoreCase))
            {
                toReturn.Gender = value.ToString();
            }
            else if (key.Equals("joined", StringComparison.OrdinalIgnoreCase))
            {
                toReturn.Joined = DateTime.SpecifyKind(DateTime.Parse(value), DateTimeKind.Utc);
            }
            else if (key.Equals("is_member", StringComparison.OrdinalIgnoreCase))
            {
                toReturn.IsMember = value.Equals("1", StringComparison.OrdinalIgnoreCase);
            }
        }

        return toReturn;
    }
}

public class BYONDUserNotFoundException : Exception
{
    public BYONDUserNotFoundException(string message) : base(message) { }
}

public class BYONDUserInactiveException : Exception
{
    public BYONDUserInactiveException(string message) : base(message) { }
}