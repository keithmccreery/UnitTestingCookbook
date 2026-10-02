using System.ComponentModel.DataAnnotations;

namespace UnitTestingCookbook.MinimalApi;

// begin-snippet: AnimalApiOptions
public class AnimalApiOptions
{
    public const string SectionName = "AnimalApi";

    [Range(1, 1000)]
    public int MaxAnimals { get; set; }

    [Required]
    public string DefaultSpecies { get; set; } = default!;
}
// end-snippet
