using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Expressions
{
    public class MissingConverterDependencyException : Exception
    {
        public MissingConverterDependencyException(Type type)
            : base($"Type {type?.Name} is not defined in Dependency Container")
        {
        }
    }
}
