using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusSystem.Menu.API.Models;

public class MenuItem
{
    public int MenuItemId { get; set; }
    
    [Required]
    public int CategoryId { get; set; }
    
    [Required, StringLength(200)]
    public required string Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [StringLength(500)]
    public string? Image { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }
    
    public Category? Category { get; set; }
}

