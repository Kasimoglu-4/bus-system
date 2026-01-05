using System.ComponentModel.DataAnnotations;

namespace BusSystem.Menu.API.Models;

public class Category
{
    public int CategoryId { get; set; }
    
    [Required]
    public int BusId { get; set; } // Reference to Bus Service
    
    [Required, StringLength(100)]
    public required string Name { get; set; }
    
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

