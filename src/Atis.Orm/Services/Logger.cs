using Atis.SqlExpressionEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Atis.Orm.Services
{
    public class Logger : ILogger
    {
        private int indentation;

        public void Indent()
        {
            this.indentation++;
        }

        public void Log(string logText)
        {
            var indent = new string('\t', this.indentation);
            Console.WriteLine($"{indent}{logText}");
        }

        public void Unindent()
        {
            this.indentation--;
        }
    }
}
