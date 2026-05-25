namespace Prowo.WebAsm.Server.Data;

public interface IEventStore
{
    IAsyncEnumerable<Event> GetAll();
    Task<Event?> Get(string eventId);
    Task Create(Event e);
    Task Update(Event e);
    Task Delete(string eventId);
}
