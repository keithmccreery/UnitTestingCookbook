namespace UnitTestingCookbook.MinimalApi;

public record AnimalRecord(int Id, string Species);

public class AnimalStore
{
    private readonly List<AnimalRecord> animals = [];
    private int nextId = 1;

    public AnimalStore()
    {
        Add(new AnimalRecord(0, "Blue Whale")); // Id is assigned by Add(), so this seeds Id 1
    }

    public bool TryGet(int id, out AnimalRecord? animal)
    {
        animal = animals.FirstOrDefault(a => a.Id == id);
        return animal is not null;
    }

    public AnimalRecord Add(AnimalRecord animal)
    {
        AnimalRecord created = animal with { Id = nextId++ };
        animals.Add(created);
        return created;
    }
}
