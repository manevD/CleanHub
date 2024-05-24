using System;
using System.Collections.Generic;

namespace CleanHub.Entities;

public partial class Article
{
    public int Id { get; set; }

    public string? Description { get; set; }

    public bool? PurschaceCalculation { get; set; }

    public string? ShortDescription { get; set; }
}
