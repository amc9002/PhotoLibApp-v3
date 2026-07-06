using System;
using System.Collections.Generic;

namespace PhotoLibApi.Models
{
    /// <summary>
    /// Implemented by entities that can carry a set of <see cref="Tag"/>s
    /// (currently <see cref="Photo"/> and <see cref="Gallery"/>), so tag
    /// assignment can be handled once in <see cref="Services.TagResolver"/>
    /// instead of being duplicated per controller.
    /// </summary>
    public interface ITaggable
    {
        ICollection<Tag> Tags { get; set; }
        DateTime UpdatedAtUtc { get; set; }
    }
}
