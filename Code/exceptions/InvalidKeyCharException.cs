using System;

namespace Chinese_Name.exceptions;

public class InvalidKeyCharException : Exception
{
    public InvalidKeyCharException(char ch, int idx, string raw_str, string post_desc) : base($"Invalid key char '{ch}' at {idx} in \"{raw_str}\". ({post_desc})")
    {
    }
}