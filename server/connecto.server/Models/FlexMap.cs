using System.Text.Json;

namespace connecto.server.Models;

public class FlexMap : Dictionary<string, object>
{
    private const string IdKey = "id";

    public bool HasId() => ContainsKey(IdKey);

    public Guid? Id()
    {
        if (!HasId())
            return null;

        var rawId = this[IdKey].ToString();
        return Guid.Parse(rawId!);
    }

    public string Serialize()
    {
        Remove(IdKey);
        return JsonSerializer.Serialize(this);
    }
}
