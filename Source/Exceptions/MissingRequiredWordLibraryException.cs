using System;

namespace Chinese_Name.Exceptions;

public class MissingRequiredWordLibraryException(string id) : Exception($"Missing required word library: \"{id}\"");