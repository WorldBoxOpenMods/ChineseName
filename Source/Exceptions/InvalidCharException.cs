using System;

namespace Chinese_Name.Exceptions;

public class InvalidCharException : Exception
{
    public InvalidCharException(char ch, int idx, string raw_str, string post_desc="") : base($"\"{raw_str}\"中第{idx}位置存在错误字符'{ch}'. ({post_desc})")
    {
    }
}