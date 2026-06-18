using System.Text.Json;

namespace ConnectoDb.Server.Models.Data;

public class FlexMap : Dictionary<string, object>
{
    private const string IdKey = "id";

    public FlexMap() : base() { }

    public FlexMap(IDictionary<string, object> dictionary) : base(dictionary) { }

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
        var copy = new Dictionary<string, object>(this);
        copy.Remove(IdKey);
        return JsonSerializer.Serialize(copy);
    }

    public static FlexMap Deserialize(Guid id, string data)
    {
      var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(data)!;
        deserialized.Add(IdKey, id.ToString());

        return new FlexMap(deserialized);
    }
}
