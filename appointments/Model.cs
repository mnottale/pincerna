using System;
using Newtonsoft.Json;

namespace Appointments;

public class Interval
{
    public DateTime begin { get; set; }
    public DateTime end { get; set; }
}
public class SlotSpec
{
    public TimeSpan slotDuration { get; set; }
    public List<Interval> openings { get; set; }
}
public class BlockedSlot: IDisposable
{
    public AppointmentsModel Model;
    public DateTime Start;
    private bool Booked = false;
    public void Book(Dictionary<string, string> payload)
    {
        Model.Book(this, payload);
        Booked = true;
    }
    public void Dispose()
    {
        if (!Booked)
            Model.Release(this);
    }
}

public class AppointmentsModel
{
    private enum SlotState
    {
        FREE = 0,
        BLOCKED = 1,
        BOOKED = 2
    }
    private class Slot
    {
        public DateTime Begin;
        public SlotState Status;
        public Dictionary<string, string> Payload;
    }
    public AppointmentsModel(SlotSpec spec)
    {
        BuildModel(spec);
    }
    public AppointmentsModel(string specPath)
    {
        BuildModel(JsonConvert.DeserializeObject<SlotSpec>(System.IO.File.ReadAllText(specPath)));
    }
    public BlockedSlot? ReserveFirst()
    {
        return Locked(() => {
                var hit = Model.Where(x => x.Status == SlotState.FREE).FirstOrDefault();
                return Block(hit);
        });
    }
    public BlockedSlot? ReserveRandom()
    {
        return Locked(() => {
                var count = Model.Where(x => x.Status == SlotState.FREE).Count();
                if (count == 0)
                    return null;
                var idx = Rnd.Next(count);
                var hit = Model.Where(x => x.Status == SlotState.FREE).Skip(idx).FirstOrDefault();
                return Block(hit);
        });
    }
    public BlockedSlot? ReserveAt(DateTime date)
    {
        return Locked(() => {
                var hit = Model.Where(x => x.Status == SlotState.FREE && x.Begin >= date).FirstOrDefault();
                return Block(hit);
        });
    }
    
    public bool CancelWhere(string key, string value)
    {
        return Locked(() =>
            {
                var hit = Model.Where(x => x.Payload != null && x.Payload.TryGetValue(key, out var val) && val == value).FirstOrDefault();
                if (hit == null)
                    return false;
                hit.Status = SlotState.FREE;
                return true;
            });
    }
    public DateTime? Find(string key, string value)
    {
        return Locked(() =>
            {
                var hit = Model.Where(x => x.Payload != null && x.Payload.TryGetValue(key, out var val) && val == value).FirstOrDefault();
                if (hit == null)
                    return null;
                return (DateTime?)hit.Begin;
            });
    }
    public void Book(BlockedSlot slot, Dictionary<string, string> payload)
    {
        LockedV(() =>
            {
                var hit = Model.Where(x => x.Begin == slot.Start).FirstOrDefault();
                if (hit != null)
                {
                    hit.Status = SlotState.BOOKED;
                    hit.Payload = payload;
                }
                else
                    throw new Exception("Failed to locate slot");
            });
    }

    public void Release(BlockedSlot slot)
    {
        LockedV(() =>
            {
                var hit = Model.Where(x => x.Begin == slot.Start).FirstOrDefault();
                if (hit != null)
                    hit.Status = SlotState.FREE;
            });
    }

    private BlockedSlot? Block(Slot? s)
    {
        if (s == null)
            return null;
        s.Status = SlotState.BLOCKED;
        return new BlockedSlot { Model = this, Start = s.Begin};
    }
    private void BuildModel(SlotSpec spec)
    {
        var d = spec.slotDuration;
        foreach (var intv in spec.openings)
        {
            var cur = intv.begin;
            while (cur < intv.end)
            {
                Model.Add(new Slot { Begin = cur, Status = SlotState.FREE});
            }
        }
    }
    private void LockedV(Action a)
    {
        Lock.Wait();
        try
        {
            a();
        }
        finally
        {
            Lock.Release();
        }
    }
    private T Locked<T>(Func<T> a)
    {
        Lock.Wait();
        try
        {
            return a();
        }
        finally
        {
            Lock.Release();
        }
    }
    private readonly List<Slot> Model = new List<Slot>();
    private readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
    private readonly Random Rnd = new Random();
}