using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PortManager.Services;

namespace WinXinAi.Engine;

public static class Program
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false);
        Console.OutputEncoding = new UTF8Encoding(false);
        await using var dispatcher = new Dispatcher();
        while (await Console.In.ReadLineAsync() is { } line)
        {
            long id = 0;
            try
            {
                if (line.Length > 1_048_576) throw new ArgumentException("Request exceeds 1 MiB.");
                var request = JsonSerializer.Deserialize<Request>(line, Json) ?? throw new ArgumentException("Empty request.");
                id = request.Id;
                var result = await dispatcher.ExecuteAsync(request);
                Console.WriteLine(JsonSerializer.Serialize(new { id, ok = true, result }, Json));
                if (request.Op == "shutdown") break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { id, ok = false, error = ex.Message }, Json));
            }
        }
    }
}

public sealed record Request(long Id, string Op, JsonElement Args, bool Confirmed = false, string Language = "zh-CN");
