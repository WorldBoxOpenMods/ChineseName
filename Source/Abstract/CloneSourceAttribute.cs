using System;

namespace Chinese_Name.Abstract;

public class CloneSourceAttribute : Attribute
{
    public readonly string clone_source_id;

    public CloneSourceAttribute(string id)
    {
        clone_source_id = id;
    }
}