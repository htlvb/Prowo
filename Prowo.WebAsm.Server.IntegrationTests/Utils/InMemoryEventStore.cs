using Prowo.WebAsm.Server.Data;

namespace Prowo.WebAsm.Server.IntegrationTests.Utils;

public class InMemoryEventStore(IEnumerable<Event> seed) : IEventStore
{
    private readonly List<Event> events = [.. seed];

    public async IAsyncEnumerable<Event> GetAll()
    {
        foreach (var e in events)
        {
            await Task.Yield();
            yield return e;
        }
    }

    public async Task<Event?> Get(string eventId)
    {
        await Task.Yield();
        return events.Find(e => e.Id == eventId);
    }

    public async Task Create(Event e)
    {
        await Task.Yield();
        events.Add(e);
    }

    public async Task Update(Event e)
    {
        await Task.Yield();
        int index = events.FindIndex(v => v.Id == e.Id);
        if (index < 0) throw new Exception("Event not found");
        events[index] = e;
    }

    public async Task Delete(string eventId)
    {
        await Task.Yield();
        events.RemoveAll(e => e.Id == eventId);
    }
}
